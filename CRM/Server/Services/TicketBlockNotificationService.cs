using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public interface ITicketBlockNotificationService
    {
        Task NotifyBlockedAsync(int idTicket, CancellationToken ct = default);
        Task NotifyUnblockedAsync(int idTicket, CancellationToken ct = default);
    }

    public class TicketBlockNotificationService : ITicketBlockNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderPlus _emailSender;
        private readonly TelegramCommandsService _telegramService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHubContext<SignalRHub> _hubContext;
        private readonly ILogEventService _logEventService;

        public TicketBlockNotificationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSenderPlus emailSender,
            TelegramCommandsService telegramService,
            IHttpContextAccessor httpContextAccessor,
            IHubContext<SignalRHub> hubContext,
            ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _telegramService = telegramService;
            _httpContextAccessor = httpContextAccessor;
            _hubContext = hubContext;
            _logEventService = logEventService;
        }

        public Task NotifyBlockedAsync(int idTicket, CancellationToken ct = default)
            => NotifyAsync(idTicket, resolved: false, ct);

        public Task NotifyUnblockedAsync(int idTicket, CancellationToken ct = default)
            => NotifyAsync(idTicket, resolved: true, ct);

        private async Task NotifyAsync(int idTicket, bool resolved, CancellationToken ct)
        {
            try
            {
                var ticket = await LoadTicketAsync(idTicket, ct);
                if (ticket == null)
                    return;

                var recipients = await BuildRecipientsAsync(ticket, ct);
                if (recipients.Count == 0)
                    return;

                var title = resolved ? $"Ticket #{TicketNumber(ticket)} sbloccato" : $"Ticket #{TicketNumber(ticket)} bloccato";
                var url = AbsoluteUrl($"/Tickets/{ticket.Id}/Info");
                var reason = resolved
                    ? FirstNotEmpty(ticket.BlockResolutionNote, "Il blocco e' stato risolto.")
                    : FirstNotEmpty(ticket.BlockReason, "Blocco senza dettaglio.");
                var company = ticket.Company?.RagioneSociale ?? string.Empty;
                var phase = ticket.CommessaFase?.Name ?? string.Empty;
                var commessa = ticket.CommessaFase?.Commessa?.Code ?? string.Empty;

                var bodyLines = new List<string>
                {
                    title,
                    $"Cliente: {company}",
                    $"Motivo: {reason}"
                };

                if (!string.IsNullOrWhiteSpace(commessa))
                    bodyLines.Add($"Commessa: {commessa}");

                if (!string.IsNullOrWhiteSpace(phase))
                    bodyLines.Add($"Fase: {phase}");

                if (!string.IsNullOrWhiteSpace(url))
                    bodyLines.Add(url);

                var body = string.Join(Environment.NewLine, bodyLines);
                await SendLiveNotificationsAsync(recipients, ticket.Id, title, ct);
                await SendEmailAndTelegramAsync(recipients, resolved, ticket, title, body, reason, company, commessa, phase, url, ct);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketBlockNotificationService), nameof(NotifyAsync), EventsTypes.Error, ex);
            }
        }

        private async Task<Ticket?> LoadTicketAsync(int idTicket, CancellationToken ct)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Include(x => x.Company)
                .Include(x => x.AssignedUsers)
                .Include(x => x.CommessaFase)
                    .ThenInclude(x => x!.Commessa)
                .FirstOrDefaultAsync(x => x.Id == idTicket, ct);
        }

        private async Task<List<ApplicationUser>> BuildRecipientsAsync(Ticket ticket, CancellationToken ct)
        {
            var users = new List<ApplicationUser>();

            users.AddRange(await GetRoleUsersAsync(new[] { eRoles.Admin, eRoles.SuperUser }, ct));
            users.AddRange(await GetAssignedUsersAsync(ticket, ct));
            users.AddRange(await GetGroupUsersAsync(ticket.IdGroupAssigned, ct));

            if (!string.IsNullOrWhiteSpace(ticket.CommessaFase?.Commessa?.IdUserResponsible))
            {
                var responsible = await _context.Users
                    .FirstOrDefaultAsync(x => x.Id == ticket.CommessaFase.Commessa.IdUserResponsible, ct);

                if (responsible != null)
                    users.Add(responsible);
            }

            return users
                .Where(IsActiveUser)
                .GroupBy(x => x!.Id)
                .Select(x => x.First()!)
                .ToList();
        }

        private async Task<List<ApplicationUser>> GetRoleUsersAsync(IEnumerable<eRoles> roles, CancellationToken ct)
        {
            var ids = new HashSet<string>();

            foreach (var role in roles)
            {
                var users = await _userManager.GetUsersInRoleAsync(role.ToString());
                foreach (var user in users.Where(IsActiveUser))
                    ids.Add(user.Id);
            }

            if (ids.Count == 0)
                return new List<ApplicationUser>();

            return await _context.Users
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);
        }

        private async Task<List<ApplicationUser>> GetAssignedUsersAsync(Ticket ticket, CancellationToken ct)
        {
            var assigned = ticket.AssignedUsers?
                .Select(x => x.IdUser)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList() ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned))
                assigned.Add(ticket.IdUserAssigned);

            assigned = assigned.Distinct().ToList();
            if (assigned.Count == 0)
                return new List<ApplicationUser>();

            return await _context.Users
                .Where(x => assigned.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);
        }

        private async Task<List<ApplicationUser>> GetGroupUsersAsync(int? idGroup, CancellationToken ct)
        {
            if (idGroup == null)
                return new List<ApplicationUser>();

            return await _context.Users
                .Where(x => !x.IsDeleted && x.Groups.Any(g => g.Id == idGroup.Value))
                .ToListAsync(ct);
        }

        private async Task SendLiveNotificationsAsync(IEnumerable<ApplicationUser> recipients, int idTicket, string title, CancellationToken ct)
        {
            foreach (var user in recipients)
            {
                if (!string.IsNullOrWhiteSpace(user.UserName)
                    && SignalRHub.Connections.TryGetValue(user.UserName, out var connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync(
                        "Notification",
                        new MsgNotify { Id = idTicket, Sender = title },
                        ct);
                }
            }
        }

        private async Task SendEmailAndTelegramAsync(
            IEnumerable<ApplicationUser> recipients,
            bool resolved,
            Ticket ticket,
            string title,
            string body,
            string reason,
            string company,
            string commessa,
            string phase,
            string? url,
            CancellationToken ct)
        {
            var emailType = resolved ? EmailsTypes.TicketUnblocked : EmailsTypes.TicketBlocked;

            foreach (var user in recipients)
            {
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    await _emailSender.SendEmailAsync(
                        new List<string> { user.Email },
                        emailType,
                        null,
                        BuildTemplateValues(user, ticket, reason, company, commessa, phase, url),
                        culture: user.LanguageCode);
                }

                if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
                    await _telegramService.SendMessage(user.PhoneNumber, body);
            }
        }

        private static Dictionary<string, string> BuildTemplateValues(
            ApplicationUser user,
            Ticket ticket,
            string reason,
            string company,
            string commessa,
            string phase,
            string? url)
        {
            return new Dictionary<string, string>
            {
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Name), user.NameComplete ?? user.UserName ?? string.Empty },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g") },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Ticket), TicketNumber(ticket) },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Company), company },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Url), url ?? string.Empty },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Reason), reason },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Commessa), commessa },
                { EmailHelper.KeyWord(EmailHelper.KeyWords.Phase), phase }
            };
        }

        private string? AbsoluteUrl(string relativeUrl)
            => _httpContextAccessor.HttpContext?.AbsoluteUrl(relativeUrl);

        private static bool IsActiveUser(ApplicationUser? user)
            => user != null && !user.IsDeleted;

        private static string FirstNotEmpty(params string?[] values)
            => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

        private static string TicketNumber(Ticket ticket)
            => string.IsNullOrWhiteSpace(ticket.Numero) ? ticket.Id.ToString() : ticket.Numero;
    }
}
