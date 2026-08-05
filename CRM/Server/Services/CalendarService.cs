using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;
using ClientLogEventService = CRM.Client.Services.ILogEventService;

namespace CRM.Server.Services
{
    public class CalendarService : ICalendarService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ClientLogEventService _logEventService;

        public CalendarService(ApplicationDbContext context, IPermitsService permitsService, ClientLogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<CalendarAgendaDTO> GetAgendaAsync(CalendarFilter filter)
        {
            try
            {
                filter ??= new CalendarFilter();

                var currentUserId = await SafeCurrentUserAsync();
                var canViewTeam = await SafeCanViewTeamAsync();
                var allowedCompanyIds = await SafeAllowedCompanyIdsAsync();
                var teamMembers = await LoadAllowedUsersAsync(allowedCompanyIds, currentUserId);
                var allowedUserIds = teamMembers.Select(u => u.Id).ToList();
                var canEditTickets = await SafeCanEditTicketsAsync();
                var effectiveScope = ResolveScope(filter, canViewTeam, allowedUserIds);
                var requestedUserId = effectiveScope switch
                {
                    CalendarScope.Team => null,
                    CalendarScope.User => filter.IdUser,
                    _ => currentUserId
                };

                var items = new List<CalendarItemDTO>();

                if (ShouldInclude(filter, CalendarItemSource.Activity))
                    items.AddRange(await LoadActivitiesAsync(filter, requestedUserId, currentUserId, allowedUserIds));

                if (ShouldInclude(filter, CalendarItemSource.Ticket))
                    items.AddRange(await LoadTicketsAsync(filter, requestedUserId, allowedCompanyIds, canEditTickets));

                if (ShouldInclude(filter, CalendarItemSource.InitiativeSchedule))
                    items.AddRange(await LoadInitiativeSchedulesAsync(filter, requestedUserId, allowedUserIds));

                items = items
                    .OrderBy(i => i.Start)
                    .ThenBy(i => i.Source)
                    .ThenBy(i => i.SourceId)
                    .ToList();

                return new CalendarAgendaDTO
                {
                    Items = items,
                    Summary = BuildSummary(items),
                    CanViewTeam = canViewTeam,
                    EffectiveScope = effectiveScope,
                    TeamMembers = canViewTeam ? teamMembers : new List<CalendarTeamMemberDTO>()
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CalendarService), nameof(GetAgendaAsync), EventsTypes.Error, ex);
                return new CalendarAgendaDTO
                {
                    ErrorMessage = "Impossibile caricare l'Agenda. Controllare il registro errori del server."
                };
            }
        }

        private async Task<List<CalendarItemDTO>> LoadActivitiesAsync(
            CalendarFilter filter,
            string? assigneeId,
            string? currentUserId,
            List<string> allowedUserIds)
        {
            var now = DateTime.Now;

            var query = _context.Activities
                .Include(a => a.User)
                .Include(a => a.Assignee)
                .Where(a => a.DueDate != null && a.State == ActivityState.Planned)
                .AsQueryable();

            query = query.Where(a =>
                (a.IdAssignee != null && allowedUserIds.Contains(a.IdAssignee))
                || (a.IdAssignee == null && a.IdUser != null && allowedUserIds.Contains(a.IdUser)));

            if (!string.IsNullOrWhiteSpace(assigneeId))
                query = query.Where(a => a.IdAssignee == assigneeId
                    || (a.IdAssignee == null && a.IdUser == assigneeId));

            if (filter.DateFrom.HasValue)
                query = query.Where(a => a.DueDate >= filter.DateFrom);

            if (filter.DateTo.HasValue)
                query = query.Where(a => a.DueDate < NormalizeDateTo(filter.DateTo.Value));

            var activities = await query
                .OrderBy(a => a.DueDate)
                .AsNoTracking()
                .ToListAsync();

            var entityNames = await ResolveActivityEntityNamesAsync(activities);

            return activities.Select(activity =>
            {
                var entityKey = (activity.EntityType, activity.EntityId);
                var entityName = entityNames.GetValueOrDefault(entityKey, string.Empty);
                var permits = ComputeActivityPermits(activity.IdUser, activity.IdAssignee, currentUserId);
                var start = activity.DueDate!.Value;

                return new CalendarItemDTO
                {
                    Id = $"{CalendarItemSource.Activity}:{activity.Id}",
                    Source = CalendarItemSource.Activity,
                    SourceId = activity.Id,
                    ActivityKind = activity.Kind,
                    EntityType = activity.EntityType,
                    EntityId = activity.EntityId,
                    Title = activity.Subject,
                    Subtitle = entityName,
                    Start = start,
                    End = start.AddMinutes(30),
                    Color = ActivityColor(activity.Kind, start < now),
                    // L'elemento e' l'attivita': si apre la sua scheda. Il cliente resta
                    // raggiungibile, ma da un campo suo.
                    Url = $"/Activities/{activity.Id}/Info",
                    EntityUrl = EntityUrl(activity.EntityType, activity.EntityId),
                    IdAssignee = activity.IdAssignee,
                    AssigneeName = activity.Assignee?.NameComplete ?? string.Empty,
                    StateText = ActivityKindText(activity.Kind),
                    IsOverdue = start < now,
                    IsCompleted = activity.State == ActivityState.Done,
                    CanComplete = permits.Edit(),
                    Permits = permits
                };
            }).ToList();
        }

        private async Task<List<CalendarItemDTO>> LoadTicketsAsync(
            CalendarFilter filter,
            string? assigneeId,
            List<int> allowedCompanyIds,
            bool canEditTickets)
        {
            var now = DateTime.Now;
            var query = _context.Tickets
                .Include(t => t.Company)
                .Include(t => t.State)
                .Include(t => t.AssignedUsers)
                    .ThenInclude(a => a.User)
                .AsQueryable();

            query = query.Where(t => allowedCompanyIds.Contains(t.IdCompany));

            if (!string.IsNullOrWhiteSpace(assigneeId))
                query = query.Where(t => t.IdUserAssigned == assigneeId || t.AssignedUsers.Any(a => a.IdUser == assigneeId));

            if (filter.DateFrom.HasValue)
                query = query.Where(t => t.Date >= filter.DateFrom || t.DateEnd >= filter.DateFrom);

            if (filter.DateTo.HasValue)
            {
                var dateTo = NormalizeDateTo(filter.DateTo.Value);
                query = query.Where(t => t.Date < dateTo || t.DateEnd < dateTo);
            }

            var tickets = await query
                .Where(t => t.Date != null)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Time)
                .AsNoTracking()
                .ToListAsync();

            return tickets.Select(ticket =>
            {
                var permits = PermitsHelper.SetRead(0);
                if (canEditTickets)
                    permits = PermitsHelper.SetEdit(permits);

                var start = ticket.Date!.Value.Date + (ticket.Time?.ToTimeSpan() ?? TimeSpan.Zero);
                var end = ticket.DateEnd ?? start.AddHours(1);
                if (end <= start)
                    end = start.AddHours(1);

                var assignees = ticket.AssignedUsers
                    .OrderBy(a => a.AssignedDate)
                    .Select(a => a.User?.NameComplete)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                var mainAssignee = ticket.AssignedUsers
                    .OrderBy(a => a.AssignedDate)
                    .FirstOrDefault();

                return new CalendarItemDTO
                {
                    Id = $"{CalendarItemSource.Ticket}:{ticket.Id}",
                    Source = CalendarItemSource.Ticket,
                    SourceId = ticket.Id,
                    EntityType = ActivityEntityType.Ticket,
                    EntityId = ticket.Id,
                    Title = $"Ticket {ticket.Numero}",
                    Subtitle = ticket.Company?.RagioneSociale ?? string.Empty,
                    Start = start,
                    End = end,
                    Color = string.IsNullOrWhiteSpace(ticket.State?.Color) ? "#455a64" : ticket.State.Color,
                    Url = $"/Tickets/{ticket.Id}/Details",
                    IdAssignee = mainAssignee?.IdUser ?? ticket.IdUserAssigned,
                    AssigneeName = assignees.Count > 0 ? string.Join(", ", assignees) : string.Empty,
                    StateText = ticket.State?.Description ?? string.Empty,
                    IsOverdue = ticket.DateExpired.HasValue && ticket.DateExpired.Value < now,
                    IsCompleted = ticket.Closed,
                    CanComplete = false,
                    Permits = permits
                };
            }).ToList();
        }

        private async Task<List<CalendarItemDTO>> LoadInitiativeSchedulesAsync(
            CalendarFilter filter,
            string? userId,
            List<string> allowedUserIds)
        {
            var query = _context.InitiativeSchedules
                .Include(s => s.Initiative)
                .Include(s => s.User)
                .AsQueryable();

            query = query.Where(s => allowedUserIds.Contains(s.IdUser));

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(s => s.IdUser == userId);

            if (filter.DateFrom.HasValue)
                query = query.Where(s => s.End >= filter.DateFrom);

            if (filter.DateTo.HasValue)
            {
                var dateTo = NormalizeDateTo(filter.DateTo.Value);
                query = query.Where(s => s.Start < dateTo);
            }

            var rows = await query
                .OrderBy(s => s.Start)
                .AsNoTracking()
                .ToListAsync();

            return rows.Select(row => new CalendarItemDTO
            {
                Id = $"{CalendarItemSource.InitiativeSchedule}:{row.Id}",
                Source = CalendarItemSource.InitiativeSchedule,
                SourceId = row.Id,
                Title = row.Initiative == null ? "Iniziativa" : row.Initiative.Name,
                Subtitle = ScheduleTypeText(row.Type),
                Start = row.Start,
                End = row.End <= row.Start ? row.Start.AddHours(1) : row.End,
                Color = ScheduleColor(row.Type),
                Url = row.Initiative == null ? string.Empty : $"/Initiatives/{row.IdInitiative}/Info",
                IdAssignee = row.IdUser,
                AssigneeName = row.User?.NameComplete ?? string.Empty,
                StateText = ScheduleTypeText(row.Type),
                IsOverdue = false,
                IsCompleted = row.End < DateTime.Now,
                CanComplete = false,
                Permits = PermitsHelper.SetRead(0)
            }).ToList();
        }

        private async Task<Dictionary<(ActivityEntityType, int), string>> ResolveActivityEntityNamesAsync(List<Activity> activities)
        {
            var result = new Dictionary<(ActivityEntityType, int), string>();
            if (activities.Count == 0)
                return result;

            var companyIds = IdsOf(activities, ActivityEntityType.Company);
            var contactIds = IdsOf(activities, ActivityEntityType.Contact);
            var leadIds = IdsOf(activities, ActivityEntityType.Lead);
            var dealIds = IdsOf(activities, ActivityEntityType.Deal);
            var ticketIds = IdsOf(activities, ActivityEntityType.Ticket);

            foreach (var company in await _context.Companies.Where(c => companyIds.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.RagioneSociale }).ToListAsync())
                result[(ActivityEntityType.Company, company.Id)] = company.Name ?? string.Empty;

            foreach (var contact in await _context.Contacts.Where(c => contactIds.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.Name + " " + c.Surname }).ToListAsync())
                result[(ActivityEntityType.Contact, contact.Id)] = contact.Name ?? string.Empty;

            foreach (var lead in await _context.Leads.Where(l => leadIds.Contains(l.Id))
                .Select(l => new { l.Id, l.Name }).ToListAsync())
                result[(ActivityEntityType.Lead, lead.Id)] = lead.Name ?? string.Empty;

            foreach (var deal in await _context.Deals.Where(d => dealIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Name }).ToListAsync())
                result[(ActivityEntityType.Deal, deal.Id)] = deal.Name ?? string.Empty;

            foreach (var ticket in await _context.Tickets.Where(t => ticketIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Numero }).ToListAsync())
                result[(ActivityEntityType.Ticket, ticket.Id)] = ticket.Numero ?? string.Empty;

            return result;
        }

        private static int[] IdsOf(List<Activity> activities, ActivityEntityType type)
        {
            return activities
                .Where(a => a.EntityType == type)
                .Select(a => a.EntityId)
                .Distinct()
                .ToArray();
        }

        private static bool ShouldInclude(CalendarFilter filter, CalendarItemSource source)
        {
            if (filter.Source.HasValue)
                return filter.Source.Value == source;

            return source switch
            {
                CalendarItemSource.Activity => filter.IncludeActivities,
                CalendarItemSource.Ticket => filter.IncludeTickets,
                CalendarItemSource.InitiativeSchedule => filter.IncludeInitiatives,
                _ => false
            };
        }

        private static CalendarSummaryDTO BuildSummary(List<CalendarItemDTO> items)
        {
            var now = DateTime.Now;
            var todayEnd = DateTime.Today.AddDays(1);
            var weekEnd = DateTime.Today.AddDays(8);

            return new CalendarSummaryDTO
            {
                Overdue = items.Count(i => !i.IsCompleted && i.Start < now),
                Today = items.Count(i => !i.IsCompleted && i.Start >= now && i.Start < todayEnd),
                NextSevenDays = items.Count(i => !i.IsCompleted && i.Start >= todayEnd && i.Start < weekEnd)
            };
        }

        private static DateTime NormalizeDateTo(DateTime dateTo)
        {
            return dateTo.Date == dateTo ? dateTo.AddDays(1) : dateTo;
        }

        private static int ComputeActivityPermits(string? ownerId, string? assigneeId, string? currentUserId)
        {
            var permits = PermitsHelper.SetRead(0);
            permits = PermitsHelper.SetInsert(permits);

            if (string.IsNullOrEmpty(currentUserId)
                || ownerId == currentUserId
                || assigneeId == currentUserId
                || (string.IsNullOrEmpty(ownerId) && string.IsNullOrEmpty(assigneeId)))
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetDelete(permits);
            }

            return permits;
        }

        private static string EntityUrl(ActivityEntityType entityType, int entityId) => entityType switch
        {
            ActivityEntityType.Deal => $"/Deals/{entityId}",
            ActivityEntityType.Company => $"/Companies/{entityId}",
            ActivityEntityType.Contact => $"/Contacts/{entityId}",
            ActivityEntityType.Lead => $"/Leads/{entityId}/Edit",
            ActivityEntityType.Ticket => $"/Tickets/{entityId}/Details",
            _ => string.Empty
        };

        private static string ActivityColor(ActivityKind kind, bool isOverdue)
        {
            if (isOverdue)
                return "#c62828";

            return kind switch
            {
                ActivityKind.Call => "#1976d2",
                ActivityKind.Email => "#0288d1",
                ActivityKind.Meeting => "#6a1b9a",
                ActivityKind.Task => "#f9a825",
                _ => "#607d8b"
            };
        }

        private static string ActivityKindText(ActivityKind kind) => kind switch
        {
            ActivityKind.Call => "Chiamata",
            ActivityKind.Email => "Email",
            ActivityKind.Meeting => "Meeting",
            ActivityKind.Task => "Task",
            _ => "Nota"
        };

        private static string ScheduleTypeText(InitiativeScheduleType type) => type switch
        {
            InitiativeScheduleType.Travel => "Viaggio",
            InitiativeScheduleType.Stand => "Presidio stand",
            InitiativeScheduleType.Setup => "Allestimento",
            InitiativeScheduleType.InternalTask => "Attivita interna",
            InitiativeScheduleType.Other => "Altro",
            _ => "Presenza"
        };

        private static string ScheduleColor(InitiativeScheduleType type) => type switch
        {
            InitiativeScheduleType.Travel => "#0f766e",
            InitiativeScheduleType.Stand => "#7c3aed",
            InitiativeScheduleType.Setup => "#b45309",
            InitiativeScheduleType.InternalTask => "#475569",
            InitiativeScheduleType.Other => "#64748b",
            _ => "#2563eb"
        };

        private async Task<string?> SafeCurrentUserAsync()
        {
            try { return await _permitsService.IdUser(); }
            catch { return null; }
        }

        private async Task<bool> SafeCanViewTeamAsync()
        {
            try
            {
                return await _permitsService.IsAdmin()
                    || await _permitsService.IsSuperUser();
            }
            catch { return false; }
        }

        private async Task<bool> SafeCanEditTicketsAsync()
        {
            try { return await _permitsService.CanEditTicket(); }
            catch { return false; }
        }

        private async Task<List<int>> SafeAllowedCompanyIdsAsync()
        {
            try
            {
                var companyIds = await _permitsService.GetIdCompanies();
                if (companyIds?.Count > 0)
                    return companyIds.Distinct().ToList();

                var companyId = await _permitsService.GetIdCompany();
                return companyId.HasValue
                    ? new List<int> { companyId.Value }
                    : new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private async Task<List<CalendarTeamMemberDTO>> LoadAllowedUsersAsync(
            List<int> allowedCompanyIds,
            string? currentUserId)
        {
            var users = allowedCompanyIds.Count == 0
                ? new List<CalendarTeamMemberDTO>()
                : (await _context.Users
                    .Where(u => !u.IsDeleted
                        && u.IdCompany.HasValue
                        && allowedCompanyIds.Contains(u.IdCompany.Value))
                    .OrderBy(u => u.Contact!.Surname)
                    .ThenBy(u => u.Contact!.Name)
                    .Select(u => new
                    {
                        u.Id,
                        Name = u.Contact != null ? u.Contact.Name : null,
                        Surname = u.Contact != null ? u.Contact.Surname : null
                    })
                    .ToListAsync())
                    .Select(u => new CalendarTeamMemberDTO
                    {
                        Id = u.Id,
                        Name = $"{u.Name} {u.Surname}".Trim()
                    })
                    .ToList();

            if (!string.IsNullOrWhiteSpace(currentUserId)
                && users.All(u => u.Id != currentUserId))
            {
                var currentUserData = await _context.Users
                    .Where(u => u.Id == currentUserId && !u.IsDeleted)
                    .Select(u => new
                    {
                        u.Id,
                        Name = u.Contact != null ? u.Contact.Name : null,
                        Surname = u.Contact != null ? u.Contact.Surname : null
                    })
                    .FirstOrDefaultAsync();

                if (currentUserData != null)
                {
                    users.Add(new CalendarTeamMemberDTO
                    {
                        Id = currentUserData.Id,
                        Name = $"{currentUserData.Name} {currentUserData.Surname}".Trim()
                    });
                }
            }

            return users
                .OrderBy(u => u.Name)
                .ToList();
        }

        private static CalendarScope ResolveScope(
            CalendarFilter filter,
            bool canViewTeam,
            List<string> allowedUserIds)
        {
            if (!canViewTeam)
                return CalendarScope.Mine;

            if (filter.Scope == CalendarScope.Team)
                return CalendarScope.Team;

            if (filter.Scope == CalendarScope.User
                && !string.IsNullOrWhiteSpace(filter.IdUser)
                && allowedUserIds.Contains(filter.IdUser))
            {
                return CalendarScope.User;
            }

            return CalendarScope.Mine;
        }
    }
}
