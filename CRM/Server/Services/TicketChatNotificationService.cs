using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public interface ITicketChatNotificationService
    {
        Task NotifyNewMessageAsync(int idTicketChat, CancellationToken ct = default);
    }

    /// <summary>
    /// Gestisce tutti gli avvisi generati da un nuovo messaggio nella chat del ticket.
    /// La scelta dei destinatari e quella dei canali restano qui, cosi' controller e import email
    /// non duplicano regole delicate.
    /// </summary>
    public class TicketChatNotificationService : ITicketChatNotificationService
    {
        private const bool NotifyAssignedUsersOnInternalMessage = true;
        private const bool EmailAssignedUsersOnInternalMessage = false;
        private const bool TelegramAssignedUsersOnInternalMessage = false;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly TelegramCommandsService _telegramService;
        private readonly ILogEventService _logEventService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHubContext<SignalRHub> _hubContext;

        public TicketChatNotificationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSenderPlus emailSenderPlus,
            TelegramCommandsService telegramService,
            ILogEventService logEventService,
            IHttpContextAccessor httpContextAccessor,
            IHubContext<SignalRHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _emailSenderPlus = emailSenderPlus;
            _telegramService = telegramService;
            _logEventService = logEventService;
            _httpContextAccessor = httpContextAccessor;
            _hubContext = hubContext;
        }

        public async Task NotifyNewMessageAsync(int idTicketChat, CancellationToken ct = default)
        {
            try
            {
                var chat = await _context.TicketChats
                    .Include(x => x.User).ThenInclude(x => x.Contact)
                    .Include(x => x.Ticket).ThenInclude(x => x.Company)
                    .Include(x => x.Ticket).ThenInclude(x => x.Contact)
                    .FirstOrDefaultAsync(x => x.Id == idTicketChat, ct);

                if (chat?.Ticket == null)
                    return;

                var senderIsInternal = await IsInternalUserAsync(chat.User, ct);
                var recipients = senderIsInternal
                    ? await BuildRecipientsForInternalMessageAsync(chat, ct)
                    : await BuildRecipientsForCustomerMessageAsync(chat, ct);

                recipients = Deduplicate(recipients)
                    .Where(x => x.UserId == null || x.UserId != chat.IdUser)
                    .ToList();

                if (recipients.Count == 0)
                    return;

                await CreateUnreadDashboardMessagesAsync(chat.Id, recipients, ct);
                await _context.SaveChangesAsync(ct);

                await SendLiveNotificationsAsync(chat, recipients, ct);
                await SendEmailAndTelegramAsync(chat, recipients, ct);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChatNotificationService), nameof(NotifyNewMessageAsync), EventsTypes.Error, ex);
            }
        }

        private async Task<List<ChatRecipient>> BuildRecipientsForCustomerMessageAsync(TicketChat chat, CancellationToken ct)
        {
            var recipients = new List<ChatRecipient>();

            foreach (var user in await GetRoleUsersAsync(new[] { eRoles.Admin, eRoles.SuperUser }, ct))
                AddUser(recipients, user, email: true, telegram: true);

            foreach (var user in await GetAssignedUsersAsync(chat.Ticket, ct))
                AddUser(recipients, user, email: true, telegram: true);

            foreach (var user in await GetGroupUsersAsync(chat.Ticket.IdGroupAssigned, ct))
                AddUser(recipients, user, email: true, telegram: true);

            return recipients;
        }

        private async Task<List<ChatRecipient>> BuildRecipientsForInternalMessageAsync(TicketChat chat, CancellationToken ct)
        {
            var recipients = new List<ChatRecipient>();
            var contactUsers = await GetContactUsersAsync(chat.Ticket, ct);

            foreach (var user in contactUsers)
                AddUser(recipients, user, email: true, telegram: true);

            if (contactUsers.Count == 0)
                AddExternalContact(recipients, chat.Ticket.Contact);

            if (NotifyAssignedUsersOnInternalMessage)
            {
                foreach (var user in await GetAssignedUsersAsync(chat.Ticket, ct))
                {
                    AddUser(
                        recipients,
                        user,
                        email: EmailAssignedUsersOnInternalMessage,
                        telegram: TelegramAssignedUsersOnInternalMessage);
                }
            }

            return recipients;
        }

        private async Task<List<ApplicationUser>> GetRoleUsersAsync(IEnumerable<eRoles> roles, CancellationToken ct)
        {
            var userIds = new HashSet<string>();

            foreach (var role in roles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role.ToString());
                foreach (var user in usersInRole.Where(IsActiveUser))
                    userIds.Add(user.Id);
            }

            if (userIds.Count == 0)
                return new List<ApplicationUser>();

            return await _context.Users
                .Include(x => x.Contact)
                .Where(x => userIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);
        }

        private async Task<List<ApplicationUser>> GetAssignedUsersAsync(Ticket ticket, CancellationToken ct)
        {
            var assigned = await _context.TicketUserAssignments
                .AsNoTracking()
                .Where(x => x.IdTicket == ticket.Id)
                .Select(x => x.IdUser)
                .ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(ticket.IdUserAssigned))
                assigned.Add(ticket.IdUserAssigned);

            assigned = assigned.Distinct().ToList();

            if (assigned.Count == 0)
                return new List<ApplicationUser>();

            return await _context.Users
                .Include(x => x.Contact)
                .Where(x => assigned.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);
        }

        private async Task<List<ApplicationUser>> GetGroupUsersAsync(int? idGroup, CancellationToken ct)
        {
            if (idGroup == null)
                return new List<ApplicationUser>();

            return await _context.Users
                .Include(x => x.Contact)
                .Where(x => !x.IsDeleted && x.Groups.Any(g => g.Id == idGroup.Value))
                .ToListAsync(ct);
        }

        private async Task<List<ApplicationUser>> GetContactUsersAsync(Ticket ticket, CancellationToken ct)
        {
            if (ticket.IdContact == null)
                return new List<ApplicationUser>();

            return await _context.Users
                .Include(x => x.Contact)
                .Where(x => !x.IsDeleted && x.IdContact == ticket.IdContact)
                .ToListAsync(ct);
        }

        private async Task<bool> IsInternalUserAsync(ApplicationUser? user, CancellationToken ct)
        {
            if (user?.IdCompany == null || user.IsDeleted)
                return false;

            var headCompanyId = await _context.GetHeadCompanyIdAsync(ct);
            return headCompanyId != null && user.IdCompany == headCompanyId;
        }

        private async Task CreateUnreadDashboardMessagesAsync(int idTicketChat, IEnumerable<ChatRecipient> recipients, CancellationToken ct)
        {
            foreach (var recipient in recipients.Where(x => x.Dashboard && !string.IsNullOrWhiteSpace(x.UserId)))
            {
                var chatRead = await _context.TicketChatReads
                    .FirstOrDefaultAsync(x => x.IdTicketChat == idTicketChat && x.IdUser == recipient.UserId, ct);

                if (chatRead == null)
                {
                    chatRead = new TicketChatRead
                    {
                        IdTicketChat = idTicketChat,
                        IdUser = recipient.UserId
                    };
                    _context.TicketChatReads.Add(chatRead);
                }

                chatRead.Displayed = false;
            }
        }

        private async Task SendLiveNotificationsAsync(TicketChat chat, IEnumerable<ChatRecipient> recipients, CancellationToken ct)
        {
            var connections = SignalRHub.Connections;
            var sender = SenderName(chat);

            foreach (var recipient in recipients.Where(x => x.Live && x.User != null))
            {
                var userName = recipient.User!.UserName;
                if (!string.IsNullOrWhiteSpace(userName) && connections.TryGetValue(userName, out var connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync(
                        "Notification",
                        new MsgNotify { Id = chat.Id, Sender = sender },
                        ct);
                }
            }
        }

        private async Task SendEmailAndTelegramAsync(TicketChat chat, IEnumerable<ChatRecipient> recipients, CancellationToken ct)
        {
            var emailRecipients = recipients
                .Where(x => x.SendEmail && !string.IsNullOrWhiteSpace(x.Email))
                .Select(x => x.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            MimeMessage? message = null;

            if (emailRecipients.Count > 0)
            {
                var keyValues = new Dictionary<string, string>
                {
                    { EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g") },
                    { EmailHelper.KeyWord(EmailHelper.KeyWords.Ticket), chat.IdTicket.ToString() },
                    { EmailHelper.KeyWord(EmailHelper.KeyWords.Name), SenderName(chat) }
                };

                var callbackUrl = AbsoluteUrl($"/Tickets/Info/{chat.IdTicket}");
                if (!string.IsNullOrWhiteSpace(callbackUrl))
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                if (!string.IsNullOrWhiteSpace(chat.Ticket.Company?.RagioneSociale))
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), chat.Ticket.Company.RagioneSociale);

                message = await _emailSenderPlus.SendEmailAsync(emailRecipients, EmailsTypes.NoticeNewChatMessage, null, keyValues);
            }

            var phones = recipients
                .Where(x => x.SendTelegram && !string.IsNullOrWhiteSpace(x.Phone))
                .Select(x => x.Phone!)
                .Distinct()
                .ToList();

            var telegramText = message?.TextBody ?? $"Nuovo messaggio sul ticket #{chat.IdTicket} da {SenderName(chat)}.";

            foreach (var phone in phones)
                await _telegramService.SendMessage(phone, telegramText);
        }

        private static void AddUser(List<ChatRecipient> recipients, ApplicationUser? user, bool email, bool telegram)
        {
            if (!IsActiveUser(user))
                return;

            recipients.Add(new ChatRecipient
            {
                UserId = user!.Id,
                User = user,
                Email = user.Email,
                Phone = user.PhoneNumber,
                Dashboard = true,
                Live = true,
                SendEmail = email,
                SendTelegram = telegram
            });
        }

        private static void AddExternalContact(List<ChatRecipient> recipients, Contact? contact)
        {
            if (contact == null)
                return;

            if (string.IsNullOrWhiteSpace(contact.Email) && string.IsNullOrWhiteSpace(contact.Mobile) && string.IsNullOrWhiteSpace(contact.Phone))
                return;

            recipients.Add(new ChatRecipient
            {
                Name = contact.NameComplete,
                Email = contact.Email,
                Phone = FirstNotEmpty(contact.Mobile, contact.Phone),
                Dashboard = false,
                Live = false,
                SendEmail = true,
                SendTelegram = true
            });
        }

        private static List<ChatRecipient> Deduplicate(IEnumerable<ChatRecipient> recipients)
        {
            var byUser = new Dictionary<string, ChatRecipient>(StringComparer.OrdinalIgnoreCase);
            var byAddress = new Dictionary<string, ChatRecipient>(StringComparer.OrdinalIgnoreCase);

            foreach (var recipient in recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient.UserId))
                {
                    if (byUser.TryGetValue(recipient.UserId, out var existing))
                        existing.Merge(recipient);
                    else
                        byUser.Add(recipient.UserId, recipient);

                    continue;
                }

                var key = FirstNotEmpty(recipient.Email, recipient.Phone);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (byAddress.TryGetValue(key, out var existingExternal))
                    existingExternal.Merge(recipient);
                else
                    byAddress.Add(key, recipient);
            }

            var userAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var userRecipient in byUser.Values)
            {
                if (!string.IsNullOrWhiteSpace(userRecipient.Email))
                    userAddresses.Add(userRecipient.Email);
                if (!string.IsNullOrWhiteSpace(userRecipient.Phone))
                    userAddresses.Add(userRecipient.Phone);
            }

            return byUser.Values
                .Concat(byAddress.Where(x => !userAddresses.Contains(x.Key)).Select(x => x.Value))
                .ToList();
        }

        private static bool IsActiveUser(ApplicationUser? user)
            => user != null && !user.IsDeleted;

        private static string SenderName(TicketChat chat)
        {
            if (chat.User != null)
                return chat.User.NameComplete;

            return string.IsNullOrWhiteSpace(chat.ExternalSender)
                ? "Cliente"
                : chat.ExternalSender!;
        }

        private string? AbsoluteUrl(string relativeUrl)
            => _httpContextAccessor.HttpContext?.AbsoluteUrl(relativeUrl);

        private static string? FirstNotEmpty(params string?[] values)
            => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        private sealed class ChatRecipient
        {
            public string? UserId { get; set; }
            public ApplicationUser? User { get; set; }
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public bool Dashboard { get; set; }
            public bool Live { get; set; }
            public bool SendEmail { get; set; }
            public bool SendTelegram { get; set; }

            public void Merge(ChatRecipient other)
            {
                User ??= other.User;
                Name ??= other.Name;
                Email ??= other.Email;
                Phone ??= other.Phone;
                Dashboard |= other.Dashboard;
                Live |= other.Live;
                SendEmail |= other.SendEmail;
                SendTelegram |= other.SendTelegram;
            }
        }
    }
}
