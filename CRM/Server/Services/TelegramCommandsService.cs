using CRM.Server.Data;
using Microsoft.EntityFrameworkCore;
using TL;

namespace CRM.Server.Services
{
    public class TelegramCommandsService
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;

        private readonly WTelegramService _wTelegramService;
        public TelegramCommandsService(ILogEventService logEventService, WTelegramService wTelegramService, ApplicationDbContext context)
        {
            _logEventService = logEventService;
            _wTelegramService = wTelegramService;
            _context = context;
        }
        public async Task SendMessage(string phoneNumber, string message)
        {
            try
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings == null || !settings.Telegram) 
                {
                    await _logEventService.RegisterAsync(nameof(WTelegramService), nameof(SendMessage), Shared.LogEvent.EventsTypes.Warning, "Telegram Disabled in Global Settings");
                    return;
                }
                var contacts = await _wTelegramService.Client.Contacts_ImportContacts(new[] { new InputPhoneContact { phone = phoneNumber } });
                if (contacts.imported.Length > 0)
                {
                    var resp = await _wTelegramService.Client.SendMessageAsync(contacts.users[contacts.imported[0].user_id], message);

                    await _logEventService.RegisterAsync(nameof(WTelegramService), nameof(SendMessage), Shared.LogEvent.EventsTypes.Info, $"{phoneNumber}: {resp.message}");
                }
                else
                    await _logEventService.RegisterAsync(nameof(WTelegramService), nameof(SendMessage), Shared.LogEvent.EventsTypes.Error, $"Numero Telefono {phoneNumber} non presente tra i contatti");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(WTelegramService), nameof(SendMessage), Shared.LogEvent.EventsTypes.Error, ex);
            }
        }

    }
}
