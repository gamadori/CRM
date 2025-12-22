using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;

namespace CRM.Server.Services
{
    public class NoticeService: INoticeService
    {
        private readonly ApplicationDbContext _context;

        private readonly IEmailSenderPlus _emailSenderPlus;

        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;
        private readonly TelegramCommandsService _telegramService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NoticeService(ApplicationDbContext context, IEmailSenderPlus emailSenderPlus,  TelegramCommandsService telegramService, ILogEventService logEventService, IPermitsService permitsService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permitsService;
            _emailSenderPlus = emailSenderPlus;
            _telegramService = telegramService;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task SendNoticeNewUserToAdmins(string idUser)
        {
            List<string> phones = new List<string>();
            List<string> to = new List<string>();

            try
            {

                var admins = await _permits.GetAdmins();

                foreach (var user in admins)
                {
                    
                    if (!to.Contains(user.Email))
                        to.Add(user.Email);

                    if (user.PhoneNumber != null && user.PhoneNumber.Length > 0 && !phones.Contains(user.PhoneNumber))
                    {
                        phones.Add(user.PhoneNumber);
                    }
                    
                }
                var curruser = await _context.Users.FindAsync(idUser);

                if (curruser != null)
                {


                    var callbackUrl = _httpContextAccessor?.HttpContext?.AbsoluteUrl($"/Settings/Users/Details/{idUser}");
                    var keyValues = new Dictionary<string, string>();

                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g"));
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), $"{curruser.Name} {curruser.Surname}");
                    

                    if (callbackUrl != null)
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                    var msg = await _emailSenderPlus.SendEmailAsync(to, EmailsTypes.NoticeNewUser, null, keyValues);

                    if (msg != null)
                    {
                        foreach (var phone in phones)
                        {

                            await _telegramService.SendMessage(phone, msg.TextBody);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(NoticeService), nameof(SendNoticeNewUserToAdmins), LogEvent.EventsTypes.Error, ex);
            }
        }
             

    }
}
