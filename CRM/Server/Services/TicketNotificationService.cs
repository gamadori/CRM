using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public interface ITicketNotificationService
    {
        /// <summary>
        /// Notifiche complete di un ticket appena creato: avviso all'utente che lo ha aperto,
        /// poi "da assegnare" (agli utenti abilitati) oppure "assegnato" (all'assegnatario).
        /// Non solleva mai eccezioni: gli errori vengono solo registrati nel log eventi.
        /// </summary>
        Task NotifyNewTicketAsync(Ticket ticket);

        /// <summary>Notifica all'utente assegnatario del ticket (email + Telegram se disponibile).</summary>
        Task<bool> NotifyTicketAssignedAsync(Ticket ticket);
    }

    /// <summary>
    /// Notifiche email/Telegram legate al ciclo di vita del ticket, estratte da TicketsController
    /// per poter essere riutilizzate anche fuori dalle action HTTP (es. assistente AI).
    /// </summary>
    public class TicketNotificationService : ITicketNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permits;
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly TelegramCommandsService _telegramService;
        private readonly ILogEventService _logEventService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TicketNotificationService(
            ApplicationDbContext context,
            IPermitsService permits,
            IEmailSenderPlus emailSenderPlus,
            TelegramCommandsService telegramService,
            ILogEventService logEventService,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _permits = permits;
            _emailSenderPlus = emailSenderPlus;
            _telegramService = telegramService;
            _logEventService = logEventService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task NotifyNewTicketAsync(Ticket ticket)
        {
            await SendEmailNoticeNewTicketAsync(ticket.Id);

            if (ticket.IdUserAssigned == null)
                await SendEmailNewTicketToBeAssignedAsync(ticket.Id);
            else
                await NotifyTicketAssignedAsync(ticket);
        }

        private async Task SendEmailNoticeNewTicketAsync(int idTicket)
        {
            try
            {
                var ticket = await _context.Tickets.Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == idTicket);

                if (ticket != null)
                {
                    var userOpened = await _context.Users.FindAsync(ticket.IdUserOpened);

                    var callbackUrl = AbsoluteUrl($"/Tickets/Info/{idTicket}");

                    var keyValues = new Dictionary<string, string>();
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));

                    if (ticket.Company != null)
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticket.Company.RagioneSociale);

                    if (callbackUrl != null)
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                    if (userOpened != null)
                    {
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), userOpened.NameComplete);

                        var msg = await _emailSenderPlus.SendEmailAsync(new List<string>() { userOpened.Email }, EmailsTypes.NoticeNewTicket, null, keyValues);

                        if (userOpened.PhoneNumber != null && userOpened.PhoneNumber.Length > 0 && msg != null)
                        {
                            await _telegramService.SendMessage(userOpened.PhoneNumber, msg.TextBody);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketNotificationService), nameof(SendEmailNoticeNewTicketAsync), EventsTypes.Error, ex);
            }
        }

        private async Task SendEmailNewTicketToBeAssignedAsync(int idTicket)
        {
            try
            {
                List<string> phones = new List<string>();

                var ticket = await _context.Tickets.Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == idTicket);

                if (ticket == null)
                    return;

                List<string> to = new List<string>();

                var users = _context.Users.Where(x => x.Groups.Where(x => x.TicketTypes.Where(y => y.Id == ticket.IdType).Any() || x.TicketTypes.Where(y => y.Id == ticket.IdType).Any()).Any());

                foreach (var user in users)
                {
                    if (await _permits.CanAssignTicket(user.Id))
                    {
                        if (!to.Contains(user.Email))
                            to.Add(user.Email);

                        if (user.PhoneNumber != null && user.PhoneNumber.Length > 0 && phones.Contains(user.PhoneNumber))
                        {
                            phones.Add(user.PhoneNumber);
                        }
                    }
                }

                var admins = await _permits.GetAdmins();

                foreach (var user in admins)
                {
                    if (await _permits.CanAssignTicket(user.Id))
                    {
                        if (!to.Contains(user.Email))
                            to.Add(user.Email);

                        if (user.PhoneNumber != null && user.PhoneNumber.Length > 0 && phones.Contains(user.PhoneNumber))
                        {
                            phones.Add(user.PhoneNumber);
                        }
                    }
                }
                var callbackUrl = AbsoluteUrl($"/Tickets/Info/{idTicket}");

                var keyValues = new Dictionary<string, string>();
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticket.Company.RagioneSociale);

                if (callbackUrl != null)
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                var msg = await _emailSenderPlus.SendEmailAsync(to, EmailsTypes.NoticeNewTicketToBeAssigned, null, keyValues);

                if (msg != null)
                {
                    foreach (var phone in phones)
                    {
                        await _telegramService.SendMessage(phone, msg.TextBody);
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketNotificationService), nameof(SendEmailNewTicketToBeAssignedAsync), EventsTypes.Error, ex);
            }
        }

        public async Task<bool> NotifyTicketAssignedAsync(Ticket ticket)
        {
            try
            {
                if (ticket.IdUserAssigned != null)
                {
                    var userAssigned = await _context.Users.FindAsync(ticket.IdUserAssigned);

                    // La ragione sociale serve al template: se la navigation non è caricata la recupera.
                    var ragioneSociale = ticket.Company?.RagioneSociale
                        ?? await _context.Companies.Where(x => x.Id == ticket.IdCompany).Select(x => x.RagioneSociale).FirstOrDefaultAsync();

                    if (userAssigned != null)
                    {
                        var keyValues = new Dictionary<string, string>();
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ragioneSociale ?? string.Empty);
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), userAssigned.NameComplete);

                        var callbackUrl = AbsoluteUrl($"/Tickets/Info/{ticket.Id}");

                        if (callbackUrl != null)
                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);
                        var msg = await _emailSenderPlus.SendEmailAsync(new List<string>() { userAssigned.UserName }, EmailsTypes.NoticeTicketAssigned, null, keyValues);

                        if (userAssigned.PhoneNumber != null && userAssigned.PhoneNumber.Length > 0 && msg != null)
                        {
                            await _telegramService.SendMessage(userAssigned.PhoneNumber, msg.TextBody);
                        }

                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketNotificationService), nameof(NotifyTicketAssignedAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private string? AbsoluteUrl(string relativeUrl)
            => _httpContextAccessor.HttpContext?.AbsoluteUrl(relativeUrl);
    }
}
