using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Extensions;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class TicketsService : ITicketsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        
        public TicketsService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService, UserManager<ApplicationUser> userManager,
            ILogEventService logEventService, ILanguagesService languagesService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _userManager = userManager;
            _logEventService = logEventService;
            _languagesService = languagesService;
        }

        #region Existing methods

        public async Task<List<(string?, string?)>> GetEmails(int idTicket)
        {
            List<(string?, string?)> addresses = new List<(string?, string?)>();

            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
                var contact = await _context.Contacts.FindAsync(ticket.IdContact);

                if (contact != null)
                {
                    addresses.Add(new(contact.Email, contact.Mobile));
                }

                var user = await _context.Users.Where(x => x.Id == ticket.IdUserOpened).FirstOrDefaultAsync();

                if (user != null)
                {
                    addresses.Add(new(user.Email, user.PhoneNumber));
                }

                var listAdmin = await _permitsService.GetAdmins();

                foreach (var admin in listAdmin)
                {
                    if (!addresses.Where(x => x.Item1 == admin.Email).Any())
                        addresses.Add((admin.Email, admin.PhoneNumber));
                }

                if (!addresses.Any())
                {
                    var users = await _context.Users.Where(x => x.IdCompany == ticket.IdCompany).ToListAsync();

                    foreach (var item in users)
                    {
                        addresses.Add((item.Email, item.PhoneNumber));
                    }
                }
            }

            return addresses;
        }

        public async Task<List<UserModel>> GetUsersCanAssignTicketAsync(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
            {
                return await GetUsersCanAssignTicketTypeAsync(ticket.IdType);
            }
            else
                return new List<UserModel>();
        }

        public async Task<List<UserModel>> GetUsersCanAssignTicketTypeAsync(int idType)
        {
            List<UserModel> usersToAssign = new List<UserModel>();
            List<int> groups;
            List<string> users;
            List<int>? idCompanies = await _permitsService.GetIdCompanies();

            if (idCompanies == null || !idCompanies.Any())
                return new List<UserModel>();

            groups = _context.Groups.Where(x => x.TicketTypes.Where(y => y.Id == idType).Any()).Select(x => x.Id).ToList();
            users = _context.Users
                .Where(x => x.TicketTypes.Where(y => y.Id == idType
                    && x.IdCompany != null && idCompanies.Contains(x.IdCompany.Value)).Any())
                .Select(x => x.Id).ToList();

            if (groups.Any() || users.Any())
            {
                if (groups.Any())
                {
                    var list = await _context.Users.Where(x => x.Groups.Where(y => groups.Contains(y.Id)).Any()).ToListAsync();
                    usersToAssign.AddRange(list.Select(x => x.ToUserModel()));
                }

                if (users.Any())
                {
                    var list = await _userManager.Users.Where(x => users.Contains(x.Id)).Select(x => x.ToUserModel()).ToListAsync();
                    list = list.Where(x => !usersToAssign.Contains(x)).ToList();
                    usersToAssign.AddRange(list);
                }
            }
            else
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings != null && await _permitsService.BelongsToHeadCompany())
                {
                    usersToAssign = await _userManager.Users.Where(x => x.IdCompany != null && idCompanies.Contains(x.IdCompany.Value)).Select(x => x.ToUserModel()).ToListAsync();
                }
            }

            return usersToAssign.ToList();
        }

        #endregion

        #region CRUD

        public async Task<Ticket?> GetItemAsync(int id)
        {
            try
            {
                var ticket = await _context.Tickets.Include(x => x.Company).Include(x => x.TicketType)
                    .Include(x => x.Article).ThenInclude(x => x.Product).Where(x => x.Id == id).FirstOrDefaultAsync();

                if (ticket != null)
                {
                    await SetTicketStateAsync(ticket);
                }

                return ticket;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetItemAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<TicketDTO?> GetDetailsAsync(int id)
        {
            try
            {
                var idLanguage = await _languagesService.GetIdLanguage();
                

                var tickets = _context.Tickets.Where(x => x.Id == id).Include(x => x.UserOpened).AsQueryable();

                var ticketModel = await tickets.Select(x => new TicketDTO()
                {
                    Id = x.Id,
                    Date = x.Date,
                    DateEnd = x.DateEnd,
                    DateOpened = x.DateOpened,
                    DateClosed = x.DateClosed,
                    Time = x.Time,
                    Company = x.Company.RagioneSociale,
                    Product = (x.Product != null) ? x.Product.Name : "",
                    Article = (x.Article != null) ? x.Article.SerialNumber : "",
                    Project = (x.Project != null) ? x.Project.Name : "",
                    IdDeal = x.IdDeal,
                    DealName = x.Deal != null ? x.Deal.Name : "",
                    IdOrder = x.IdOrder,
                    OrderNumber = x.Order != null ? (x.Order.Number ?? x.Order.Id.ToString()) : "",
                    IdUserAssigned = x.IdUserAssigned,
                    IdCompany = x.IdCompany,
                    IdState = x.IdState,
                    IdUserOpened = x.IdUserOpened,
                    UserOpened = (x.UserOpened != null) ? x.UserOpened.NameComplete : "",
                    UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                    UserClosed = (x.UserClosed != null) ? x.UserClosed.NameComplete : "",
                    MinuteWork = x.TicketInterventions.Sum(y => y.Minute),
                    Description = x.Description,
                    OperationalSummary = x.OperationalSummary,
                    OperationalSummaryUpdatedAt = x.OperationalSummaryUpdatedAt,
                    OperationalSummaryUpdatedBy = x.OperationalSummaryUpdatedBy,
                    OperationalSummaryUpdatedByName = x.OperationalSummaryUpdatedByUser != null ? x.OperationalSummaryUpdatedByUser.NameComplete : "",
                    IdType = x.IdType,
                    DescType = (x.TicketType.Languages.Where(l => l.IdLanguage == idLanguage).Any()) ? x.TicketType.Languages.Where(l => l.IdLanguage == idLanguage).FirstOrDefault().Name : "",
                    TicketType = x.TicketType,
                    ContactName = x.Contact != null ? x.Contact.NameComplete : "",
                    CloseDescription = x.CloseDescription,
                    Closed = x.Closed,
                }).FirstOrDefaultAsync();

                if (ticketModel != null)
                {
                    await SetTicketStateAsync(ticketModel);
                }

                return ticketModel;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetDetailsAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<ObjectView<TicketDTO, string>> GetPagingAsync(TicketFilter args)
        {
            try
            {
                string idUser = await _permitsService.IdUser();
                DateTime dateTo;

                IQueryable<Ticket> tickets = _context.Tickets;

                if (args.OrderBy != null)
                {
                    tickets = tickets.OrderBy(args.OrderBy);
                }
                else
                {
                    tickets = tickets.OrderByDescending(x => x.Date);
                }

                if (!await _permitsService.CanAccessOtherCompany())
                {
                    var idCompany = await _permitsService.GetIdCompany();
                    tickets = tickets.Where(x => x.IdCompany == idCompany);
                }

                if (args.DateFrom != null)
                {
                    tickets = tickets.Where(x => x.Date >= args.DateFrom || x.DateEnd >= args.DateFrom);
                }

                if (args.DateTo != null)
                {
                    dateTo = args.DateTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.Date < dateTo || x.DateEnd < dateTo);
                }

                if (args.DateClosedFrom != null)
                {
                    tickets = tickets.Where(x => x.DateClosed >= args.DateClosedFrom);
                }

                if (args.DateClosedTo != null)
                {
                    dateTo = args.DateClosedTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.DateClosed < dateTo);
                }

                if (args.DateExpiredFrom != null)
                {
                    tickets = tickets.Where(x => x.DateExpired >= args.DateExpiredFrom);
                }

                if (args.DateClosedTo != null)
                {
                    dateTo = args.DateClosedTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.DateClosed < dateTo);
                }

                if (args.IdCompany != null)
                {
                    tickets = tickets.Where(x => x.IdCompany == args.IdCompany);
                }

                if (args.IdArticle != null)
                    tickets = tickets.Where(x => x.IdArticle == args.IdArticle);

                if (args.IdUserOpened != null)
                {
                    tickets = tickets.Where(x => x.IdUserOpened == args.IdUserOpened);
                }

                if (args.IdUserAssigned != null && args.TypeSearch != (int)TicketTypeSearch.NotAssigned && args.TypeSearch != (int)TicketTypeSearch.NewMessage)
                {
                    if (args.ViewNotAssigned)
                        tickets = tickets.Where(x => (x.IdUserAssigned == args.IdUserAssigned || x.IdUserAssigned == null));
                    else
                        tickets = tickets.Where(x => x.IdUserAssigned == args.IdUserAssigned || x.AssignedUsers.Where(y => y.IdUser == args.IdUserAssigned).Any());
                }

                if (args.IdProject != null)
                {
                    tickets = tickets.Where(x => x.IdProject == args.IdProject);
                }

                if (args.IdDeal != null)
                {
                    tickets = tickets.Where(x => x.IdDeal == args.IdDeal);
                }

                if (args.IdOrder != null)
                {
                    tickets = tickets.Where(x => x.IdOrder == args.IdOrder);
                }

                tickets = FilterByType(tickets, (TicketTypeSearch)args.TypeSearch, idUser);

                if (args.Filter != null && args.Filter.Length > 0)
                {
                    tickets = tickets.Where(args.Filter);
                }

                var totalWork = _context.TicketsInterventions
                    .Where(x => tickets.Contains(x.Ticket))
                    .SelectMany(y => y.TicketInterventionTime)
                    .Where(x => x.TimeType == InterventionTimeType.Work)
                    .Sum(z => (int)EF.Functions.DateDiffMinute(z.StartDateTime, z.EndDateTime));

                int count = tickets != null ? tickets.Count() : 0;

                if (tickets != null && args?.Skip != null && args.Top != null)
                {
                    tickets = tickets.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var ticketModel = tickets.Select(x => new TicketDTO()
                {
                    Id = x.Id,
                    Date = x.Date,
                    DateOpened = x.DateOpened,
                    DateEnd = x.DateEnd,
                    DateClosed = x.DateClosed,
                    Company = x.Company!.RagioneSociale,
                    Product = (x.Product != null) ? x.Product.Name : "",
                    Article = (x.Article != null) ? x.Article.SerialNumber : "",
                    Project = (x.Project != null) ? x.Project.Name : "",
                    IdDeal = x.IdDeal,
                    DealName = x.Deal != null ? x.Deal.Name : "",
                    IdOrder = x.IdOrder,
                    OrderNumber = x.Order != null ? (x.Order.Number ?? x.Order.Id.ToString()) : "",
                    IdUserAssigned = x.IdUserAssigned,
                    IdCompany = x.IdCompany,
                    IdState = x.IdState,
                    IdUserOpened = x.IdUserOpened,
                    UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                    MinuteWork = x.TicketInterventions
                         .SelectMany(y => y.TicketInterventionTime.Where(z => z.TimeType == InterventionTimeType.Work))
                            .Sum(z => (int)EF.Functions.DateDiffMinute(z.StartDateTime, z.EndDateTime)),
                    MinuteTravel = x.TicketInterventions
                        .SelectMany(y => y.TicketInterventionTime.Where(z => z.TimeType == InterventionTimeType.Travel))
                            .Sum(z => (int)EF.Functions.DateDiffMinute(z.StartDateTime, z.EndDateTime)),
                    Invoiced = x.Invoiced,
                    Description = x.Description,
                    OperationalSummary = x.OperationalSummary,
                    OperationalSummaryUpdatedAt = x.OperationalSummaryUpdatedAt,
                    OperationalSummaryUpdatedBy = x.OperationalSummaryUpdatedBy,
                    OperationalSummaryUpdatedByName = x.OperationalSummaryUpdatedByUser != null ? x.OperationalSummaryUpdatedByUser.NameComplete : "",
                    ContactName = x.Contact != null ? x.Contact.NameComplete : "",
                    Time = x.Time,
                    Closed = x.Closed,
                });

                var items = ticketModel.ToList();

                foreach (var t in items)
                {
                    t.MinuteWorkFormatted = DateTimeHelper.MinuteFormat(t.MinuteWork);
                    await SetTicketStateAsync(t);
                }

                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                };

                ObjectView<TicketDTO, string> ticketView = new ObjectView<TicketDTO, string>();
                ticketView.Total = DateTimeHelper.MinuteFormat(totalWork);
                ticketView.Items = items;

                return ticketView;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetPagingAsync), EventsTypes.Error, ex);
                return new ObjectView<TicketDTO, string>();
            }
        }

        public async Task<Ticket> PostAsync(Ticket ticket)
        {

            try
            {
                string idUserOpened = await _permitsService.IdUser();
                int day = await GetDayBeforeExpired(ticket.Id);

                ticket.DateOpened = DateTime.Now;

                if (ticket.Date == null)
                {
                    ticket.Date = ticket.DateOpened;
                }

                ticket.IdUserOpened = idUserOpened;
                ticket.DateExpired = ticket.Date?.AddWorkdays(day);

                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                return ticket;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(PostAsync), EventsTypes.Error, ex);
                throw;
            }
        }

        public async Task<bool> PutAsync(int id, Ticket ticket)
        {
            try
            {
                var currentSummary = await _context.Tickets
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new
                    {
                        x.OperationalSummary,
                        x.OperationalSummaryUpdatedAt,
                        x.OperationalSummaryUpdatedBy
                    })
                    .FirstOrDefaultAsync();

                if (currentSummary == null)
                    return false;

                ticket.OperationalSummary = currentSummary.OperationalSummary;
                ticket.OperationalSummaryUpdatedAt = currentSummary.OperationalSummaryUpdatedAt;
                ticket.OperationalSummaryUpdatedBy = currentSummary.OperationalSummaryUpdatedBy;

                ticket.IdCompanyAssigned =
                    (ticket.IdUserAssigned != null) ? _context.Users.Where(x => x.Id == ticket.IdUserAssigned).Select(x => x.IdCompany).FirstOrDefault() : null;

                _context.Entry(ticket).State = EntityState.Modified;

                if (!await _permitsService.IsAdmin())
                    _context.Entry(ticket).Property(x => x.Invoiced).IsModified = false;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(PutAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null)
                    return false;

                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        #endregion

        #region Close / ReOpen

        public async Task<bool> CloseAsync(int id, TicketClose model)
        {
            try
            {
                var ticketState = await GetIdState(eTicketStates.Closed);

                Ticket? ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null) return false;

                ticket.DateClosed = DateTime.Now;
                ticket.CloseDescription = model.Description;
                ticket.CloseNote = model.Note;
                ticket.IdUserClosed = await _permitsService.IdUser();
                ticket.Support = model.Support;
                ticket.Closed = true;
                ticket.IdState = ticketState?.Id;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(CloseAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<bool> ReOpenAsync(int id)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null) return false;

                ticket.Closed = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(ReOpenAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        #endregion

        #region State

        public async Task SetTicketStateAsync(TicketDTO ticket)
        {
            TicketState? ticketState = await GetTicketIdState(ticket);
            ticket.IdState = ticketState?.Id;
            ticket.State = (ticketState?.idState)?.ToString();
            ticket.StateColor = ticketState?.Color;

            ticket.Permits = await _permitsService.TicketPermits(ticket.Id, ticket.IdCompany, ticket.IdUserAssigned);

            if (!await _permitsService.CanViewInternalData())
                ticket.CloseNote = "";
        }

        public async Task SetTicketStateAsync(Ticket ticket)
        {
            TicketState? ticketState = await GetTicketIdState(ticket);
            ticket.IdState = ticketState?.Id;
            ticket.StateDesc = (ticketState?.idState)?.ToString();
            ticket.StateColor = ticketState?.Color;

            ticket.Permits = await _permitsService.TicketPermits(ticket.Id, ticket.IdCompany, ticket.IdUserAssigned);

            if (!await _permitsService.CanViewInternalData())
                ticket.CloseNote = "";
        }

        private async Task<TicketState?> GetTicketIdState(Ticket ticket)
        {
            if (ticket != null)
            {
                if (ticket.Closed)
                    return await GetIdState(eTicketStates.Closed);
                else
                {
                    await CheckTicketExpired(ticket.Id);
                    var processingState = await GetIdState(eTicketStates.Processing);
                    var hasAssignedUser = await HasAssignedUserAsync(ticket.Id, ticket.IdUserAssigned);

                    if (await _permitsService.IsClient())
                    {
                        return processingState;
                    }
                    else if (ticket.IdState == processingState?.Id && hasAssignedUser)
                        return processingState;
                    else if (ticket.DateExpired != null && DateTime.Now.Date > ticket.DateExpired)
                        return await GetIdState(eTicketStates.Expired);

                    else if (ticket.IdUserAssigned != null || ticket.IdGroupAssigned != null)
                        return await GetIdState(eTicketStates.Assigned);
                    else
                        return await GetIdState(eTicketStates.Created);
                }
            }
            return null;
        }

        private async Task<TicketState?> GetTicketIdState(TicketDTO ticketModel)
        {
            var ticket = await _context.Tickets.FindAsync(ticketModel.Id);

            if (ticket != null)
            {
                if (ticket.Closed)
                    return await GetIdState(eTicketStates.Closed);
                else
                {
                    await CheckTicketExpired(ticket.Id);
                    var processingState = await GetIdState(eTicketStates.Processing);
                    var hasAssignedUser = await HasAssignedUserAsync(ticket.Id, ticket.IdUserAssigned);

                    if (await _permitsService.IsClient())
                    {
                        return processingState;
                    }
                    else if (ticket.IdState == processingState?.Id && hasAssignedUser)
                        return processingState;
                    else if (DateTime.Now.Date > ticket.DateExpired)
                        return await GetIdState(eTicketStates.Expired);

                    else if (ticket.IdUserAssigned != null || ticket.IdGroupAssigned != null)
                        return await GetIdState(eTicketStates.Assigned);
                    else
                        return await GetIdState(eTicketStates.Created);
                }
            }
            return null;
        }

        private async Task<TicketState?> GetIdState(eTicketStates state)
        {
            var ticketState = await _context.TicketStates.Where(x => x.State == (int)state).FirstOrDefaultAsync();
            if (ticketState != null)
                ticketState.idState = state;
            return ticketState;
        }

        private async Task<bool> HasAssignedUserAsync(int ticketId, string? legacyAssignedUserId)
        {
            return !string.IsNullOrWhiteSpace(legacyAssignedUserId)
                || await _context.TicketUserAssignments.AnyAsync(a => a.IdTicket == ticketId);
        }

        #endregion

        #region Helpers

        public async Task<bool> TicketChangeAssigned(int id, string? idAssigned)
        {
            bool state = false;
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                state = (ticket.IdUserAssigned != idAssigned);
                _context.Entry(ticket).State = EntityState.Detached;
            }

            return state;
        }

        public async Task CheckTicketExpired(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                int day = await GetDayBeforeExpired(id);

                if (day > 0 && ticket.Date != null)
                {
                    ticket.DateExpired = ticket.Date.Value.AddDays(day);
                }
                else if (ticket.DateExpired != null)
                    ticket.DateExpired = null;

                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetDayBeforeExpired(int id)
        {
            int day = 3;

            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                var ticketType = await _context.TicketTypes.FindAsync(ticket.IdType);

                if (ticketType != null)
                {
                    if (ticketType.ExpiredDate > 0)
                    {
                        day = ticketType.ExpiredDate;
                    }
                    else
                    {
                        var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                        if (settings != null)
                        {
                            day = settings.TicketDaysExpired;
                        }
                    }
                }
            }
            return day;
        }

        private IQueryable<Ticket> FilterByType(IQueryable<Ticket> tickets, TicketTypeSearch filter, string idUser)
        {
            switch (filter)
            {
                case TicketTypeSearch.Assigned:
                    tickets = tickets.Where(x => !x.Closed);
                    tickets = tickets.Where(x => x.IdUserAssigned != null);
                    break;

                case TicketTypeSearch.NotAssigned:
                    tickets = tickets.Where(x => !x.Closed);
                    tickets = tickets.Where(x => x.IdUserAssigned == null);
                    break;

                case TicketTypeSearch.Expired:
                    tickets = tickets.Where(x => !x.Closed);
                    DateTime date = DateTime.Now.Date;
                    tickets = tickets.Where(x => date > x.DateExpired);
                    break;

                case TicketTypeSearch.Working:
                    tickets = tickets.Where(x => !x.Closed);
                    break;

                case TicketTypeSearch.NewMessage:
                    tickets = tickets.Where(x => x.TicketsChats.Where(y => y.TicketChatReads.Where(z => z.Displayed == false && z.IdUser == idUser).Any()).Any());
                    break;

                case TicketTypeSearch.ToBeInvoiced:
                    tickets = tickets.Where(x => x.Invoiced == false);
                    break;

                case TicketTypeSearch.Closed:
                    tickets = tickets.Where(x => x.Closed);
                    break;
            }

            return tickets;
        }

        #endregion

        #region Assignment

        public async Task<List<string>> GetAssignedUserIdsAsync(int idTicket)
        {
            try
            {
                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null)
                    return new List<string>();

                return ticket.AssignedUsers
                    .Select(au => au.IdUser)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(GetAssignedUserIdsAsync), EventsTypes.Error, ex);
                return new List<string>();
            }
        }

        public async Task<AssignUsersResult> AssignUsersAsync(int idTicket, AssignUsersRequest request, string? currentUserId)
        {
            try
            {
                var ticket = await _context.Tickets
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == idTicket);

                if (ticket == null)
                {
                    return new AssignUsersResult { Success = false, ErrorMessage = $"Ticket con ID {idTicket} non trovato" };
                }

                var previouslyAssignedUserIds = ticket.AssignedUsers
                    .Select(au => au.IdUser)
                    .ToHashSet();

                _context.TicketUserAssignments.RemoveRange(ticket.AssignedUsers);

                var newlyAssignedUserIds = new HashSet<string>();

                if (request.UserIds != null && request.UserIds.Any())
                {
                    foreach (var userId in request.UserIds)
                    {
                        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                        if (!userExists)
                        {
                            return new AssignUsersResult { Success = false, ErrorMessage = $"Utente con ID {userId} non trovato" };
                        }

                        var assignment = new TicketUserAssignment
                        {
                            IdTicket = idTicket,
                            IdUser = userId,
                            AssignedDate = DateTime.Now,
                            AssignedBy = currentUserId
                        };

                        _context.TicketUserAssignments.Add(assignment);
                        newlyAssignedUserIds.Add(userId);
                    }

                    ticket.IdUserAssigned = request.UserIds.First();
                }
                else
                {
                    ticket.IdUserAssigned = null;
                }

                await _context.SaveChangesAsync();

                var addedUsers = newlyAssignedUserIds.Except(previouslyAssignedUserIds).ToList();
                var removedUsers = previouslyAssignedUserIds.Except(newlyAssignedUserIds).ToList();

                await _logEventService.RegisterAsync(
                    nameof(TicketsService),
                    nameof(AssignUsersAsync),
                    EventsTypes.Info,
                    $"Ticket #{idTicket}: {(request.UserIds?.Any() == true ? $"Assegnati {request.UserIds.Count} utenti" : "Rimosse tutte le assegnazioni")}");

                return new AssignUsersResult
                {
                    Success = true,
                    AddedUserIds = addedUsers,
                    RemovedUserIds = removedUsers,
                    AssignedCount = request.UserIds?.Count ?? 0,
                    Ticket = ticket
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsService), nameof(AssignUsersAsync), EventsTypes.Error, ex);
                return new AssignUsersResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        #endregion
    }
}
