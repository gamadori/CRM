using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    public enum TicketReminderKind
    {
        Appointment,
        Expiry
    }

    public sealed class TicketReminderNotification
    {
        public required int IdTicket { get; init; }
        public required TicketReminderKind Kind { get; init; }
        public required string Title { get; init; }
        public required string Body { get; init; }
        public required string Url { get; init; }
        public bool TicketIsExpired { get; init; }
    }

    public sealed class TicketReminderNotificationResult
    {
        public int Recipients { get; init; }
        public int PushDeliveries { get; init; }
        public int EmailDeliveries { get; init; }
        public int TelegramDeliveries { get; init; }
        public bool HasRecipients => Recipients > 0;
    }

    public interface ITicketReminderNotificationService
    {
        Task<TicketReminderNotificationResult> NotifyAsync(TicketReminderNotification notification, CancellationToken ct = default);
    }

    /// <summary>
    /// Canali e destinatari dei preavvisi ticket. Il background service decide solo quando un
    /// preavviso e' maturo; questo servizio decide chi avvisare e con quali canali.
    /// </summary>
    public class TicketReminderNotificationService : ITicketReminderNotificationService
    {
        private const bool NotifyAssignedUsers = true;
        private const bool NotifyGroupUsersWhenQueued = true;
        private const bool NotifyAdminsForExpiredTickets = true;
        private const bool SendPush = true;
        private const bool SendEmailWhenPushMissing = true;
        private const bool SendTelegram = true;

        private readonly ApplicationDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IEmailSenderPlus _emailSender;
        private readonly TelegramCommandsService _telegramService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TicketReminderNotificationService(
            ApplicationDbContext context,
            IPushNotificationService pushNotificationService,
            IEmailSenderPlus emailSender,
            TelegramCommandsService telegramService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _emailSender = emailSender;
            _telegramService = telegramService;
            _userManager = userManager;
        }

        public async Task<TicketReminderNotificationResult> NotifyAsync(TicketReminderNotification notification, CancellationToken ct = default)
        {
            var ticket = await _context.Tickets
                .AsNoTracking()
                .Include(x => x.AssignedUsers)
                .Include(x => x.GroupAssigned)
                .FirstOrDefaultAsync(x => x.Id == notification.IdTicket, ct);

            if (ticket == null)
                return new TicketReminderNotificationResult();

            var recipients = await BuildRecipientsAsync(ticket, notification, ct);
            if (recipients.Count == 0)
                return new TicketReminderNotificationResult();

            var pushDeliveries = 0;
            var emailDeliveries = 0;
            var telegramDeliveries = 0;

            foreach (var recipient in recipients)
            {
                var pushSent = 0;
                if (SendPush)
                    pushSent = await _pushNotificationService.SendToUserAsync(recipient.User.Id, new
                    {
                        title = notification.Title,
                        body = notification.Body,
                        url = notification.Url
                    });

                pushDeliveries += pushSent;

                if (SendEmailWhenPushMissing && pushSent <= 0 && !string.IsNullOrWhiteSpace(recipient.User.Email))
                {
                    await _emailSender.SendEmailAsync(
                        new List<string> { recipient.User.Email },
                        EmailsTypes.NoticeReminder,
                        null,
                        notification.Title,
                        notification.Body,
                        null);

                    emailDeliveries++;
                }

                if (SendTelegram && !string.IsNullOrWhiteSpace(recipient.User.PhoneNumber))
                {
                    await _telegramService.SendMessage(recipient.User.PhoneNumber, $"{notification.Title}\n{notification.Body}");
                    telegramDeliveries++;
                }
            }

            return new TicketReminderNotificationResult
            {
                Recipients = recipients.Count,
                PushDeliveries = pushDeliveries,
                EmailDeliveries = emailDeliveries,
                TelegramDeliveries = telegramDeliveries
            };
        }

        private async Task<List<TicketReminderRecipient>> BuildRecipientsAsync(Ticket ticket, TicketReminderNotification notification, CancellationToken ct)
        {
            var users = new List<ApplicationUser>();

            if (NotifyAssignedUsers)
                users.AddRange(await GetAssignedUsersAsync(ticket, ct));

            if (NotifyGroupUsersWhenQueued && ticket.IdGroupAssigned != null && !HasAssignedUsers(ticket))
                users.AddRange(await GetGroupUsersAsync(ticket.IdGroupAssigned.Value, ct));

            if (NotifyAdminsForExpiredTickets && notification.Kind == TicketReminderKind.Expiry && notification.TicketIsExpired)
                users.AddRange(await GetRoleUsersAsync(new[] { eRoles.Admin, eRoles.SuperUser }, ct));

            return users
                .Where(IsActiveUser)
                .GroupBy(x => x.Id)
                .Select(x => new TicketReminderRecipient(x.First()))
                .ToList();
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

        private static bool HasAssignedUsers(Ticket ticket)
        {
            if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned))
                return true;

            return ticket.AssignedUsers?.Any(x => !string.IsNullOrWhiteSpace(x.IdUser)) == true;
        }

        private async Task<List<ApplicationUser>> GetGroupUsersAsync(int idGroup, CancellationToken ct)
        {
            return await _context.Users
                .Where(x => !x.IsDeleted && x.Groups.Any(g => g.Id == idGroup))
                .ToListAsync(ct);
        }

        private async Task<List<ApplicationUser>> GetRoleUsersAsync(IEnumerable<eRoles> roles, CancellationToken ct)
        {
            var ids = new HashSet<string>();

            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.ToString());
                foreach (var user in usersInRole.Where(IsActiveUser))
                    ids.Add(user.Id);
            }

            if (ids.Count == 0)
                return new List<ApplicationUser>();

            return await _context.Users
                .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);
        }

        private static bool IsActiveUser(ApplicationUser? user)
            => user != null && !user.IsDeleted;

        private sealed record TicketReminderRecipient(ApplicationUser User);
    }
}
