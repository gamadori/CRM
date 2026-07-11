using System.Text.RegularExpressions;
using CRM.Client.Services;
using CRM.Server;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services.Email
{
    public class InboundEmailRouter : IInboundEmailRouter
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IEmailSenderPlus _emailSender;
        private readonly IHubContext<SignalRHub> _hub;
        private readonly IInboundEmailAiService _ai;
        private readonly IArchiveService _archiveService;

        private const int MaxBodyLength = 8000;

        public InboundEmailRouter(ApplicationDbContext context, ILogEventService logEventService, IEmailSenderPlus emailSender, IHubContext<SignalRHub> hub, IInboundEmailAiService ai, IArchiveService archiveService)
        {
            _context = context;
            _logEventService = logEventService;
            _emailSender = emailSender;
            _hub = hub;
            _ai = ai;
            _archiveService = archiveService;
        }

        public async Task<bool> IngestAsync(InboundMessage message, CancellationToken ct = default)
        {
            try
            {
                // Deduplica: stessa casella + stesso Message-Id (o UID se il Message-Id manca).
                bool exists = await _context.InboundEmails.AsNoTracking().AnyAsync(x =>
                    x.IdInbox == message.InboxId &&
                    ((message.MessageId != null && x.MessageId == message.MessageId) ||
                     (message.MessageId == null && message.Uid != null && x.Uid == message.Uid)), ct);

                if (exists)
                    return false;

                var inbox = await _context.EmailInboxes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == message.InboxId, ct);

                var body = Truncate(StripQuoted(message.Body), MaxBodyLength);
                var subject = string.IsNullOrWhiteSpace(message.Subject) ? "Email ricevuta" : message.Subject!;

                // Risoluzione mittente → contatto → azienda.
                var from = message.FromAddress?.Trim().ToLowerInvariant();
                Contact? contact = null;
                if (!string.IsNullOrEmpty(from))
                    contact = await _context.Contacts.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == from, ct);

                var idCompany = contact?.IdCompany;
                var now = DateTime.Now;

                int? idTicket = null;
                string activitySubject = subject;

                var ticketRef = ExtractTicketToken(subject);

                // Triage AI (opzionale, non bloccante): riassume l'email e valuta se è una richiesta di
                // assistenza. Non serve sulle risposte a ticket esistenti, che sono già instradate.
                InboundEmailTriage? triage = null;
                if (inbox?.UseAiTriage == true && ticketRef == null)
                    triage = await _ai.AnalyzeAsync(subject, body, ct);

                // 1) Threading: la risposta a un ticket esistente ([#T{id}] nell'oggetto) si aggancia a quel ticket.
                if (ticketRef != null)
                {
                    var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketRef.Value, ct);
                    if (ticket != null)
                    {
                        idTicket = ticket.Id;
                        idCompany ??= ticket.IdCompany;
                        activitySubject = $"Risposta ticket #{ticket.Id}: {subject}";

                        // La risposta del cliente entra nella conversazione del ticket, con i suoi allegati.
                        AddInboundChatMessage(ticket.Id, message.FromAddress, body, message.ReceivedAt);
                        await AttachToTicketAsync(ticket.Id, subject, message.Attachments, inbox?.IdDefaultOwner, ct);
                    }
                }
                // 2) Nuovo ticket: solo se la casella lo richiede, il mittente è associato e i default sono configurati.
                //    Se il triage AI ha stabilito che NON è una richiesta di assistenza, il ticket automatico
                //    non viene aperto: l'email resta comunque registrata, non letta e con la motivazione visibile,
                //    e l'operatore può crearlo a mano dal dettaglio.
                else if (inbox?.DefaultAction == InboundAction.NewTicket && idCompany != null
                         && triage is not { IsSupportRequest: false })
                {
                    var ticket = await CreateTicketAsync(inbox, idCompany.Value, contact?.Id, subject, body, triage, message.FromName, ct);
                    if (ticket != null)
                    {
                        idTicket = ticket.Id;
                        activitySubject = $"Nuovo ticket #{ticket.Id}: {subject}";

                        // Il testo originale dell'email diventa il primo messaggio della conversazione:
                        // la Description resta il riassunto, la chat conserva il testo integrale del cliente.
                        AddInboundChatMessage(ticket.Id, message.FromAddress, body, message.ReceivedAt);
                        await AttachToTicketAsync(ticket.Id, subject, message.Attachments, inbox.IdDefaultOwner, ct);

                        await SendAcknowledgementAsync(message.FromAddress, ticket.Id, subject, triage?.Language);
                    }
                }

                // Attività sulla timeline dell'azienda (visibile in Azienda/Deal). Se l'azienda non è nota
                // resta solo la registrazione "non associata" in InboundEmail.
                Activity? activity = null;
                if (idCompany != null)
                {
                    activity = new Activity
                    {
                        Kind = ActivityKind.Email,
                        Subject = activitySubject,
                        Description = body,
                        EntityType = ActivityEntityType.Company,
                        EntityId = idCompany.Value,
                        State = ActivityState.Done,
                        DoneDate = message.ReceivedAt,
                        CreatedAt = now
                    };
                    _context.Activities.Add(activity);
                }

                var inbound = new InboundEmail
                {
                    IdInbox = message.InboxId,
                    MessageId = message.MessageId,
                    Uid = message.Uid,
                    FromAddress = message.FromAddress,
                    FromName = message.FromName,
                    ToAddress = message.ToAddress,
                    Subject = message.Subject,
                    Body = body,
                    ReceivedAt = message.ReceivedAt,
                    IdContact = contact?.Id,
                    IdCompany = idCompany,
                    IdTicket = idTicket,
                    IsMatched = idCompany != null,
                    AiIsSupportRequest = triage?.IsSupportRequest,
                    AiConfidence = triage?.Confidence,
                    AiSummary = triage?.Summary,
                    AiReason = triage?.Reason,
                    CreatedAt = now
                };
                _context.InboundEmails.Add(inbound);

                await _context.SaveChangesAsync(ct);

                // Allegati (contenuto già scaricato dalla sorgente).
                foreach (var att in message.Attachments)
                {
                    _context.InboundEmailAttachments.Add(new InboundEmailAttachment
                    {
                        IdInboundEmail = inbound.Id,
                        FileName = att.FileName,
                        ContentType = att.ContentType,
                        Size = att.Content.LongLength,
                        Content = att.Content
                    });
                }

                if (activity != null)
                {
                    inbound.IdActivity = activity.Id;
                    activity.IdInboundEmail = inbound.Id;
                }

                if (activity != null || message.Attachments.Count > 0)
                    await _context.SaveChangesAsync(ct);

                // Avviso in tempo reale agli operatori (toast + suono + contatore dashboard).
                await _hub.Clients.All.SendAsync("Notification",
                    new InboundEmailNotify { Id = inbound.Id, From = message.FromAddress, Subject = subject }, ct);

                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InboundEmailRouter), nameof(IngestAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        /// <summary>
        /// Copia gli allegati dell'email negli allegati del ticket, così chi lavora la pratica li trova
        /// direttamente lì. I file finiscono nell'archivio con lo stesso meccanismo degli upload manuali.
        /// </summary>
        private async Task AttachToTicketAsync(int idTicket, string subject, IReadOnlyList<InboundAttachment> attachments, string? idUser, CancellationToken ct)
        {
            if (attachments.Count == 0)
                return;

            var attachment = new Attachment
            {
                IdParent = idTicket,
                AttchmentType = AttachmentTypes.Ticket,
                Name = string.IsNullOrWhiteSpace(subject) ? "Allegati email" : subject,
                Description = "Allegati ricevuti via email",
                CreatedOn = DateTime.Now,
                IdUser = idUser,
                CanEdit = true,
                CanDelete = true
            };
            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync(ct);

            _archiveService.TypeArchive = ArchiveTypes.Attachments;

            foreach (var att in attachments)
            {
                var ext = Path.GetExtension(att.FileName);

                var file = new AttachmentFile
                {
                    IdAttachment = attachment.Id,
                    Name = att.FileName,
                    ContentType = ext,
                    FileType = ext,
                    Size = att.Content.LongLength
                };
                _context.AttachmentFiles.Add(file);
                await _context.SaveChangesAsync(ct);

                _archiveService.SaveAttachments(file.Id, ext, att.Content);
            }
        }

        /// <summary>
        /// Aggiunge alla conversazione del ticket un messaggio proveniente dall'esterno (email del cliente):
        /// nessun utente interno (IdUser null) e mittente conservato in ExternalSender, così la chat lo
        /// mostra come "ricevuto" senza attribuirlo a un operatore.
        /// </summary>
        private void AddInboundChatMessage(int idTicket, string? fromAddress, string? body, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;

            _context.TicketChats.Add(new TicketChat
            {
                IdTicket = idTicket,
                IdUser = null!,
                ExternalSender = fromAddress,
                Date = date,
                Message = body,
                Deleted = false
            });
        }

        /// <summary>Crea un ticket dai default della casella; null se i default (tipo/apritore) non sono configurati.</summary>
        private async Task<Ticket?> CreateTicketAsync(EmailInbox inbox, int idCompany, int? idContact, string subject, string? body, InboundEmailTriage? triage, string? senderName, CancellationToken ct)
        {
            if (inbox.IdDefaultType == null || string.IsNullOrWhiteSpace(inbox.IdDefaultOwner))
                return null;

            var now = DateTime.Now;
            var ticket = new Ticket
            {
                IdCompany = idCompany,
                IdType = inbox.IdDefaultType.Value,
                IdUserOpened = inbox.IdDefaultOwner!,
                IdContact = idContact,
                Description = BuildTicketDescription(subject, body, triage, senderName),
                DateOpened = now,
                Date = now,
                Numero = string.Empty,
                CloseDescription = string.Empty,
                CloseNote = string.Empty
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync(ct);
            return ticket;
        }

        /// <summary>
        /// Descrizione del ticket: il riassunto AI se disponibile (evita di riversare l'email intera),
        /// altrimenti oggetto + corpo. Il testo integrale resta comunque su InboundEmail.Body.
        /// </summary>
        private static string BuildTicketDescription(string subject, string? body, InboundEmailTriage? triage, string? senderName)
        {
            if (triage != null && !string.IsNullOrWhiteSpace(triage.Summary))
            {
                var title = string.IsNullOrWhiteSpace(triage.Title) ? subject : triage.Title!;
                return $"{title}\n\n{triage.Summary}";
            }

            var clean = StripSignature(body, senderName);
            return string.IsNullOrWhiteSpace(clean) ? subject : $"{subject}\n\n{clean}";
        }

        /// <summary>
        /// Pulizia conservativa della firma in coda al testo (solo per la descrizione fallback, quando
        /// l'AI non è attiva). Rimuove dal fondo: righe che coincidono col nome del mittente, il
        /// delimitatore standard "--", chiusure di cortesia e righe "Inviato da/Sent from". Si ferma al
        /// primo contenuto reale, così non intacca mai il testo del problema.
        /// </summary>
        private static string? StripSignature(string? body, string? senderName)
        {
            if (string.IsNullOrWhiteSpace(body))
                return body;

            var lines = body.Replace("\r\n", "\n").Split('\n').ToList();

            // Taglio netto al delimitatore di firma RFC 3676 ("-- " o "--").
            var delim = lines.FindLastIndex(l => l.Trim() == "--" || l.Trim() == "-- ");
            if (delim >= 0)
                lines = lines.Take(delim).ToList();

            var closings = new[] { "cordiali saluti", "distinti saluti", "saluti", "grazie", "grazie mille",
                "buona giornata", "a presto", "cordialmente", "un saluto" };

            bool IsSignatureLine(string line)
            {
                var t = line.Trim().TrimEnd(',', '.', '!').Trim();
                if (t.Length == 0) return true; // riga vuota in coda
                if (!string.IsNullOrWhiteSpace(senderName) && string.Equals(t, senderName.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
                if (closings.Contains(t.ToLowerInvariant())) return true;
                if (Regex.IsMatch(t, @"^(inviato da|sent from|get outlook|ottieni outlook)\b", RegexOptions.IgnoreCase)) return true;
                return false;
            }

            int last = lines.Count - 1;
            while (last >= 0 && IsSignatureLine(lines[last]))
                last--;

            return string.Join("\n", lines.Take(last + 1)).Trim();
        }

        /// <summary>
        /// Invia al mittente la conferma di apertura con il token [#T{id}] nell'oggetto (per il threading
        /// delle risposte), nella lingua rilevata dall'AI quando disponibile.
        /// </summary>
        private async Task SendAcknowledgementAsync(string? to, int ticketId, string subject, string? language)
        {
            if (string.IsNullOrWhiteSpace(to))
                return;

            var ackSubject = $"[#T{ticketId}] {subject}";
            var ackBody = AckBody(ticketId, language);

            await _emailSender.SendEmailAsync(to!, ackSubject, ackBody);
        }

        /// <summary>Testo di conferma localizzato per lingua (fallback italiano).</summary>
        private static string AckBody(int ticketId, string? language)
        {
            var lang = (language ?? "it").Trim().ToLowerInvariant();
            if (lang.Length > 2) lang = lang.Substring(0, 2);

            return lang switch
            {
                "en" => $"We have received your request and opened ticket #{ticketId}. To update it, reply to this email keeping the subject unchanged.",
                "es" => $"Hemos recibido su solicitud y abierto el ticket #{ticketId}. Para actualizarla, responda a este correo manteniendo el asunto sin cambios.",
                "fr" => $"Nous avons bien reçu votre demande et ouvert le ticket #{ticketId}. Pour la mettre à jour, répondez à cet e-mail en conservant l'objet inchangé.",
                _ => $"Abbiamo ricevuto la tua richiesta e aperto il ticket #{ticketId}. Per aggiornare la pratica rispondi a questa email mantenendo invariato l'oggetto.",
            };
        }

        private static int? ExtractTicketToken(string? subject)
        {
            if (string.IsNullOrEmpty(subject))
                return null;

            var m = Regex.Match(subject, @"\[#T(\d+)\]", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
        }

        /// <summary>
        /// Rimuove la cronologia citata da una risposta, tenendo solo il testo nuovo in cima.
        /// Euristica sui marcatori comuni (righe "&gt;", "On ... wrote:", "Il giorno ... ha scritto:", ...).
        /// </summary>
        private static string? StripQuoted(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return body;

            var lines = body.Replace("\r\n", "\n").Split('\n');
            var kept = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith(">"))
                    break;
                if (Regex.IsMatch(trimmed, @"^-+\s*Original Message\s*-+", RegexOptions.IgnoreCase))
                    break;
                if (Regex.IsMatch(trimmed, @"^(On|Il giorno|Le|Am|El)\b.*\b(wrote|ha scritto|a écrit|schrieb|escribió):?\s*$", RegexOptions.IgnoreCase))
                    break;
                if (Regex.IsMatch(trimmed, @"^From:\s", RegexOptions.IgnoreCase) && kept.Count > 0)
                    break;

                kept.Add(line);
            }

            return string.Join("\n", kept).Trim();
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
