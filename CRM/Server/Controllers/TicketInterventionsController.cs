using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Extensions;
using CRM.Server.Helpers;
using CRM.Server.Models;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Extensions;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client; // ✅ AGGIUNTO
using CRM.Server.Services.Sms;
using Newtonsoft.Json;
using SelectPdf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketInterventionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly IArchiveService _archiveService;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogEventService _logEventService;
        private readonly IEmailSenderPlus _emailSender;
        private readonly IInterventionPdfGenerator _pdfGenerator;
        private readonly ISignatureOtpService _otpService;
        private readonly IStringLocalizer<TicketInterventionsController> _localizer;
        private readonly ISmsSender _smsSender;
        private readonly SmsOptions _smsOptions;

        public TicketInterventionsController(ApplicationDbContext context, IPermitsService permitsService, IArchiveService archiveService,
            IWebHostEnvironment hostEnvironment, ILogEventService logEventService, IEmailSenderPlus emailSender, IInterventionPdfGenerator pdfGenerator,
            ISignatureOtpService otpService, IStringLocalizer<TicketInterventionsController> localizer,
            ISmsSender smsSender, IOptions<SmsOptions> smsOptions)
        {
            _context = context;
            _permitsService = permitsService;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Interventions;
            _hostEnvironment = hostEnvironment;
            _logEventService = logEventService;
            _emailSender = emailSender;
            _pdfGenerator = pdfGenerator;
            _otpService = otpService;
            _localizer = localizer;
            _smsSender = smsSender;
            _smsOptions = smsOptions.Value;
        }

        // GET: api/TicketInterventions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketIntervention>>> GetTicketIntervention([FromQuery] TicketInterventionFilter? args = null)
        {
            try
            {
                
                var items = await Filter(args);

                if (items == null)
                    return new List<TicketIntervention>();

                
                foreach (var item in items)
                {
                    int? idCompany = await TicketGetIdCompany(item.IdTicket);

                    if (idCompany != null)
                    {
                       
                        var mainUserId = item.AssignedUsers.FirstOrDefault()?.IdUser;
                        item.Permits = await _permitsService.ObjectPermits(idCompany, item.IdUser);
                        
                       
                    }
                }

                return Ok(items);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(GetTicketIntervention), LogEvent.EventsTypes.Error, ex);
                return new List<TicketIntervention>();
            }
        }

        [HttpGet("signature-pendig")]
        public async Task<ActionResult> PendingSignatures(TicketInterventionFilter? args)
        {
            var items = await Filter(args);
            
            if (items == null)
                return Problem("Empty");

            var list = await items
                .Where(x => x.SignatureStatus == CRM.Shared.SignatureStatus.Pending)
                .ToListAsync();
            
            return Ok(list);

        }
        // GET: api/TicketInterventions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketIntervention>> GetTicketIntervention(int id)
        {
            var ticketIntervention = await _context.TicketsInterventions
                .Include(x => x.TicketInterventionsTypes)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (ticketIntervention == null)
            {
                return NotFound();
            }
            else if (!await _permitsService.CanGetObject(await TicketGetIdCompany(ticketIntervention.IdTicket)))
            {
                return BadRequest();
            }

            foreach (var item in ticketIntervention.TicketInterventionsTypes)
            {
                ticketIntervention.InterventionsTypesId.Add(item.Id);
            }

            ticketIntervention.AttachmentExist = ExistReport(id);
          
            ticketIntervention.Permits = await _permitsService.ObjectPermits(ticketIntervention.Ticket.IdCompany, ticketIntervention.IdUser);
            
            ticketIntervention.InterventionArticles = await _context.TicketInterventionArticles
                .Where(x => x.IdTicketIntervention == id)
                .Select(x => new TicketInterventionArticleModel()
                {
                    Id = Guid.NewGuid(),
                    IdArticle = x.IdArticle,
                    IdProduct = x.IdProduct,
                    Description = x.Description,
                    IdTicketIntervention = x.IdTicketIntervention,
                    IdLink = x.Id,
                    Product = x.Product.Name,
                    Article = x.Article.SerialNumber
                }).ToListAsync();

            ticketIntervention.Users = await _context.TicketInterventionUser
                .Where(x => x.IdIntervention == id)
                .Select(x => new UserModel
                {
                    Id = x.IdUser,
                    Name = x.User.Name,
                    Surname = x.User.Surname,
                    Email = x.User.Email


                    // Add other properties as needed
                }).ToListAsync();

            return ticketIntervention;
        }

        [HttpGet("CompanyEmailAdresses/{Id}")]
        public async Task<ActionResult<List<string>>> GetCompanyAddressEmail(int id)
        {
            List<string> emailAddresses = new List<string>();

            var ticket = await _context.Tickets.Where(x => x.TicketInterventions.Where(x => x.Id == id).Any()).FirstOrDefaultAsync();

            if (ticket != null)
            {
                var company = await _context.Companies.Include(x => x.ApplicationUsers).FirstOrDefaultAsync(x => x.Id == ticket.IdCompany);

                if (company != null)
                {
                    if (company.Email != null && company.Email.Length > 0)
                        emailAddresses.Add(company.Email);

                    foreach (var user in company.ApplicationUsers)
                    {
                        emailAddresses.Add(user.Email);
                    }

                }

            }
            return emailAddresses;
        }
        // PUT: api/TicketInterventions/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketIntervention(int id, TicketIntervention ticketIntervention)
        {
            if (id != ticketIntervention.Id)
            {
                return BadRequest();
            }

            

            _context.Entry(ticketIntervention).State = EntityState.Modified;

            await UpdateArticles(ticketIntervention);

            try
            {
                

              
                
                await _context.SaveChangesAsync();

                await InterventionType(id, ticketIntervention.InterventionsTypesId);
             
                
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketInterventionExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }


        // POST: api/TicketInterventions
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult> PostTicketIntervention(TicketIntervention ticketIntervention)
        {
            try
            {
                // I service si registrano solo sui ticket assegnati a se stessi (Admin e SuperUser
                // esclusi dal vincolo). Finora l'endpoint non faceva alcun controllo.
                if (!await _permitsService.CanAddTicketIntervention(ticketIntervention.IdTicket))
                    return StatusCode(StatusCodes.Status403Forbidden, "Puoi registrare interventi solo sui ticket assegnati a te.");

                ticketIntervention.IdUser = await _permitsService.IdUser();

                _context.TicketsInterventions.Add(ticketIntervention);
                await _context.SaveChangesAsync();
                await InterventionType(ticketIntervention.Id, ticketIntervention.InterventionsTypesId);
                await UpdateArticles(ticketIntervention);

                return CreatedAtAction("GetTicketIntervention", new { id = ticketIntervention.Id }, ticketIntervention);
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(PostTicketIntervention), LogEvent.EventsTypes.Error, ex);
                return NoContent();
            }
        }

        // DELETE: api/TicketInterventions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketIntervention(int id)
        {
            var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
            if (ticketIntervention == null)
            {
                return NotFound();
            }

            _context.TicketsInterventions.Remove(ticketIntervention);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ==========================================
        // POST: api/Tickets/{id}/assign-users
        // Assegna multipli utenti a un ticket
        // ==========================================
        [HttpPost("{id}/assign-users")]
        public async Task<IActionResult> AssignUsers(int id, [FromBody] List<string> userIds)
        {
            try
            {
                var ticket = await _context.TicketsInterventions
                    .Include(t => t.AssignedUsers)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (ticket == null)
                {
                    return NotFound($"Ticket con ID {id} non trovato");
                }

                var currentUserId = await _permitsService.IdUser();

                // ✅ NUOVO: Memorizza utenti attualmente assegnati (PRIMA della rimozione)
                var previouslyAssignedUserIds = ticket.AssignedUsers
                    .Select(au => au.IdUser)
                    .ToHashSet();

                // Rimuovi tutte le assegnazioni esistenti
                _context.TicketInterventionUser.RemoveRange(ticket.AssignedUsers);

                // Nuovo set di utenti assegnati
                var newlyAssignedUserIds = new HashSet<string>();

                // ✅ NUOVO: Gestisci il caso di lista vuota (rimozione totale assegnazioni)
                if (userIds != null && userIds.Any())
                {
                    // Aggiungi le nuove assegnazioni
                    foreach (var userId in userIds)
                    {
                        // Verifica che l'utente esista
                        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                        if (!userExists)
                        {
                            return BadRequest($"Utente con ID {userId} non trovato");
                        }

                        var assignment = new TicketInterventionUser
                        {
                            IdIntervention = id,                           
                            IdUser = userId
                           
                        };

                        _context.TicketInterventionUser.Add(assignment);
                        newlyAssignedUserIds.Add(userId);
                    }
                    
                }
                else
                {
                    // ✅ CASO LISTA VUOTA: Rimuovi tutte le assegnazioni
                    ticket.AssignedUsers = null;

                    await _logEventService.RegisterAsync(
                        nameof(TicketInterventionsController),
                        nameof(AssignUsers),
                        LogEvent.EventsTypes.Info,
                        $"Ticket #{id}: tutte le assegnazioni rimosse da utente {currentUserId}");
                }

                await _context.SaveChangesAsync();

                // Log operazione
                var action = userIds?.Any() == true
                    ? $"Assegnati {userIds.Count} utenti"
                    : "Rimosse tutte le assegnazioni";

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(AssignUsers),
                    LogEvent.EventsTypes.Info,
                    $"Ticket #{id}: {action}");

                // ✅ NUOVO: Calcola utenti aggiunti e rimossi
                var addedUsers = newlyAssignedUserIds.Except(previouslyAssignedUserIds).ToList();
                var removedUsers = previouslyAssignedUserIds.Except(newlyAssignedUserIds).ToList();


                return Ok(new
                {
                    message = userIds?.Any() == true
                        ? "Utenti assegnati con successo"
                        : "Tutte le assegnazioni rimosse con successo",
                    assignedCount = userIds?.Count ?? 0,
                    addedCount = addedUsers.Count,
                    removedCount = removedUsers.Count
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController),
                    nameof(AssignUsers),
                    LogEvent.EventsTypes.Error,
                    $"Errore assegnazione utenti ticket #{id}: {ex.Message}");

                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }
        [HttpGet("Report/{id}")]
        public async Task<bool> CreateDocPdf(int id, [FromQuery] string? languageCode = null)
        {

           

            
                if ((await CreatePdf(id, languageCode)) != null)
                {
                    var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                    ticketIntervention.HasAttachments = true;
                    _context.Entry(ticketIntervention).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    return true;
                }
                else
                    return false;
            


        }

        [HttpPost("UploadReport/{id}")]
        public async Task<bool> PostReport(int id, UploadFilesModel item)
        {
            try
            {
                var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                var file = item.Files.FirstOrDefault();

                if (ticketIntervention != null && file != null)
                {
                    // Passa "pdf" come estensione, non il ContentType
                    _archiveService.SaveAttachments(id, "pdf", file.Content);
                    
                    _context.Entry(ticketIntervention).State = EntityState.Modified;
                    ticketIntervention.HasAttachments = true;

                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(PostReport), LogEvent.EventsTypes.Error, ex.Message);
                return false;
            }
        }

        [HttpGet("Download/{id}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                if (await _permitsService.CanDownloadInterventionReport(id))
                {
                    var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                    if (ticketIntervention != null && ticketIntervention.HasAttachments)
                    {
                        byte[] bytes = _archiveService.GetAttachmentByExt(id, "pdf");

                        AttachmentResponse header = new AttachmentResponse();
                        var name = $"{id}.pdf";
                        header.ContentType = MimeKit.MimeTypes.GetMimeType(name);
                        header.Name = name;
                        HttpContext.Response.Headers.Add(ConstHelper.FileHeader,
                            JsonConvert.SerializeObject(header));

                        MemoryStream ms = new MemoryStream(bytes);
                        return new FileStreamResult(ms, header.ContentType);
                    }
                    else
                        return NotFound();
                }
                else
                    await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(DownloadFile), LogEvent.EventsTypes.Permits, "Not Permits");

                return NotFound();
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsDashboardController), nameof(DownloadFile), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        [HttpGet("getreport/{id}")]
        public async Task<string?> GetReport(int id)
        {
            try
            {
                if (await _permitsService.CanDownloadInterventionReport(id))
                {
                    var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                    if (ticketIntervention != null && ticketIntervention.HasAttachments)
                    {
                        byte[] bytes = _archiveService.GetAttachmentByExt(id, "pdf");

                        // Restituisci SOLO la stringa Base-64 senza il prefisso data URI
                        return Convert.ToBase64String(bytes);
                    }
                    else
                        return null;
                }
                else
                    await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(GetReport), LogEvent.EventsTypes.Permits, "Not Permits");

                return null;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsDashboardController), nameof(GetReport), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }


        [HttpPost("Email/{Id}")]
        public async Task<ActionResult<bool>> SendReportIntervention(int id, [FromBody] EmailViewModel email)
        {
            var intervention = await _context.TicketsInterventions.FindAsync(id);

            
            if (intervention == null)
                return NotFound();

            if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                return Problem("Not Permits");

            string path = _archiveService.GetPath(id, "pdf");

            List<string> listAttachment = new List<string>() { { path } };

            // Aggancia la comunicazione alla scheda dell'azienda del ticket: comparirà nella timeline Attività.
            var ticket = await _context.Tickets.FindAsync(intervention.IdTicket);
            EmailContext? context = ticket != null && ticket.IdCompany > 0
                ? new EmailContext(ActivityEntityType.Company, ticket.IdCompany)
                : null;

            var state = await _emailSender.SendEmailAsync(email.To.ToList(";"), EmailsTypes.InvioDocumento, new List<string>() { { path } }, email.Subject, email.Message, null, email.CC, context);

            if (state)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(SendReportIntervention), LogEvent.EventsTypes.Info, "Email inviata correttamente");
            }
            else
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(SendReportIntervention), LogEvent.EventsTypes.Error, "Errore durante l'invio dell'email");

            return state;
        }

        /// <summary>
        /// Richiede OTP per verifica firma digitale
        /// </summary>
        [HttpPost("RequestSignatureOtp/{id}")]
        public async Task<ActionResult<OtpRequestResponse>> RequestSignatureOtp(int id, [FromBody] SignaturePendingData signatureData)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                        .ThenInclude(t => t.Company)
                    .FirstOrDefaultAsync(x => x.Id == id);
                
                if (intervention == null)
                    return NotFound();

                if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                    return Problem("Not Permits");

                const int OtpTtlMinutes = 5;
                const int OtpResendCooldownSeconds = 60;

                // Rate limiting: max 1 OTP ogni 60 secondi, basato sull'istante di
                // GENERAZIONE (= scadenza - TTL), non sulla scadenza stessa (che
                // altrimenti bloccava le richieste per ~6 minuti).
                if (intervention.SignatureOtpExpiry.HasValue)
                {
                    var generatedAt = intervention.SignatureOtpExpiry.Value.AddMinutes(-OtpTtlMinutes);
                    if (generatedAt > DateTime.Now.AddSeconds(-OtpResendCooldownSeconds))
                    {
                        var wait = (int)(generatedAt.AddSeconds(OtpResendCooldownSeconds) - DateTime.Now).TotalSeconds;
                        return StatusCode(429, new { error = $"Troppo presto. Riprova tra {Math.Max(1, wait)} secondi" });
                    }
                }

                // Genera OTP e challenge
                var otp = _otpService.GenerateOtp();
                var challengeId = _otpService.GenerateChallengeId();
                var otpHash = _otpService.HashOtp(otp, challengeId);

                // Salva stato temporaneo
                intervention.SignatureOtpHash = otpHash;
                intervention.SignatureOtpChallengeId = challengeId;
                intervention.SignatureOtpExpiry = DateTime.Now.AddMinutes(OtpTtlMinutes);
                intervention.SignatureOtpAttempts = 0;
                intervention.PendingSignature = signatureData.Signature;
                intervention.PendingSignatureName = signatureData.SignerName;
                // Firma acquisita ma non ancora confermata: in attesa dell'OTP.
                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Pending;

                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Destinatario dell'OTP: preferisci un SMS al cellulare del firmatario,
                // poi la sua email; solo come ultimo fallback l'email dell'azienda.
                var signerPhone = NormalizePhone(signatureData.SignerPhone);
                if (string.IsNullOrWhiteSpace(signerPhone) && !string.IsNullOrWhiteSpace(signatureData.SignerEmail))
                    signerPhone = NormalizePhone(await ResolvePhoneByEmail(signatureData.SignerEmail));

                var signerEmail = !string.IsNullOrWhiteSpace(signatureData.SignerEmail)
                    ? signatureData.SignerEmail
                    : intervention.Ticket?.Company?.Email;

                string channel;
                string sentTo;

                if (!string.IsNullOrWhiteSpace(signerPhone) && _smsSender.IsConfigured &&
                    await _smsSender.SendAsync(signerPhone,
                        $"Codice OTP per la firma dell'intervento #{id}: {otp} (valido 5 minuti). Non condividerlo con nessuno."))
                {
                    channel = "sms";
                    sentTo = MaskPhone(signerPhone);
                }
                else if (!string.IsNullOrWhiteSpace(signerEmail))
                {
                    var subject = $"Codice OTP per firma intervento #{id}";
                    var message = $@"
                        <h2>Verifica Firma Digitale</h2>
                        <p>Il tuo codice OTP per confermare la firma è:</p>
                        <h1 style='color: #0066cc; font-size: 32px; letter-spacing: 5px;'>{otp}</h1>
                        <p><small>Valido per 5 minuti. Non condividere questo codice.</small></p>
                        <p>Intervento: #{id}<br/>
                        Firmatario: {signatureData.SignerName}</p>
                    ";

                    await _emailSender.SendEmailAsync(signerEmail, subject, message);
                    channel = "email";
                    sentTo = MaskEmail(signerEmail);
                }
                else
                {
                    // Nessun recapito utilizzabile: annulla lo stato OTP appena creato
                    ClearOtpState(intervention);
                    await _context.SaveChangesAsync();
                    return StatusCode(422, new { error = "Nessun recapito disponibile per l'invio dell'OTP" });
                }

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RequestSignatureOtp),
                    LogEvent.EventsTypes.Info,
                    $"OTP generato per intervention #{id} - Canale: {channel} - Dest: {sentTo} - Challenge: {challengeId.Substring(0, 8)}...");

                return Ok(new OtpRequestResponse
                {
                    Success = true,
                    ChallengeId = challengeId,
                    ExpiresAt = intervention.SignatureOtpExpiry.Value,
                    SentTo = sentTo,
                    Channel = channel
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RequestSignatureOtp),
                    LogEvent.EventsTypes.Error,
                    $"Errore richiesta OTP: {ex.Message}");
                return StatusCode(500, new { error = "Errore generazione OTP" });
            }
        }

        /// <summary>
        /// Verifica OTP e completa salvataggio firma
        /// </summary>
        [HttpPost("VerifySignatureOtp/{id}")]
        public async Task<ActionResult<OtpVerifyResponse>> VerifySignatureOtp(int id, [FromBody] OtpVerifyRequest request)
        {
            try
            {
                var intervention = await _context.TicketsInterventions.FindAsync(id);
                
                if (intervention == null)
                    return NotFound();

                if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                    return Problem("Not Permits");

                // Verifica challenge
                if (intervention.SignatureOtpChallengeId != request.ChallengeId)
                    return BadRequest(new { error = "Challenge non valido" });

                // Verifica scadenza
                if (!intervention.SignatureOtpExpiry.HasValue || intervention.SignatureOtpExpiry < DateTime.Now)
                {
                    ClearOtpState(intervention);
                    await _context.SaveChangesAsync();
                    return StatusCode(401, new { error = "OTP scaduto" });
                }

                // Verifica tentativi
                if (intervention.SignatureOtpAttempts >= 3)
                {
                    ClearOtpState(intervention);
                    await _context.SaveChangesAsync();
                    return StatusCode(423, new { error = "Troppi tentativi. Richiedi nuovo OTP" });
                }

                // ✅ VERIFICA OTP
                bool isValid = _otpService.VerifyOtp(
                    request.Otp,
                    intervention.SignatureOtpHash,
                    intervention.SignatureOtpChallengeId
                );

                if (!isValid)
                {
                    intervention.SignatureOtpAttempts++;
                    await _context.SaveChangesAsync();

                    return StatusCode(401, new
                    {
                        error = "OTP non valido",
                        attemptsRemaining = 3 - intervention.SignatureOtpAttempts
                    });
                }

                // ✅ OTP VALIDO: Promuovi firma da pending a definitiva
                intervention.CustomerSignature = intervention.PendingSignature;
                intervention.SignatureName = intervention.PendingSignatureName;
                intervention.SignatureDate = DateTime.Now;
                // Firma confermata: aggiorna lo stato (altrimenti resta "Pending" e
                // Details/PDF mostrano "in attesa di conferma email").
                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Verified;
                // Rimuove eventuale stato residuo del vecchio flusso email-link.
                intervention.SignatureConfirmationToken = null;

                // Pulisci stato OTP
                ClearOtpState(intervention);

                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(VerifySignatureOtp),
                    LogEvent.EventsTypes.Info,
                    $"OTP verificato con successo per intervention #{id} - Firmatario: {intervention.SignatureName}");

                return Ok(new OtpVerifyResponse
                {
                    Success = true,
                    Message = "Firma verificata e salvata con successo"
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(VerifySignatureOtp),
                    LogEvent.EventsTypes.Error,
                    $"Errore verifica OTP: {ex.Message}");
                return StatusCode(500, new { error = "Errore verifica OTP" });
            }
        }

        private async Task<IQueryable<TicketIntervention>?> Filter(TicketInterventionFilter? args = null)
        {
            try
            {
                var items = _context.TicketsInterventions.Include(x => x.TicketInterventionsTypes).AsQueryable();


                List<int>? companies = null;

                if (!await _permitsService.BelongsToHeadCompany())
                {
                    companies = await _permitsService.GetIdCompanies() ?? new();

                    items = items.Where(x => companies.Contains(x.Ticket.IdCompany));
                }

                if (args?.SignaturePending == true)
                {
                    items = items.Where(x => x.SignatureStatus == CRM.Shared.SignatureStatus.Pending);
                
                }

                if (args != null)
                {
                    if (args.OrderBy != null)
                        items = items.OrderBy(args.OrderBy);

                    if (args.IdTicket != null)
                        items = items.Where(x => x.IdTicket == args.IdTicket);

                    if (args.Filter != null)
                        items = items.Where(args.Filter);

                    PagingHelper.ResponsePaging<TicketIntervention, TicketInterventionFilter>(HttpContext, items, args);

                }
                


                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(GetTicketIntervention), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        private void ClearOtpState(TicketIntervention intervention)
        {
            intervention.SignatureOtpHash = null;
            intervention.SignatureOtpChallengeId = null;
            intervention.SignatureOtpExpiry = null;
            intervention.SignatureOtpAttempts = 0;
            intervention.PendingSignature = null;
            intervention.PendingSignatureName = null;
        }

        /// <summary>
        /// (Tecnico) Genera un link di firma remota e lo invia al cliente via SMS/email.
        /// </summary>
        [HttpPost("RequestRemoteSignature/{id}")]
        public async Task<ActionResult<RemoteSignatureRequestResponse>> RequestRemoteSignature(int id)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                        .ThenInclude(t => t.Company)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (intervention == null)
                    return NotFound();

                if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                    return Problem("Not Permits");

                // Token monouso per la pagina pubblica di firma
                var token = Guid.NewGuid().ToString("N");
                intervention.SignatureConfirmationToken = token;
                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Pending;
                await _context.SaveChangesAsync();

                var link = $"{Request.Scheme}://{Request.Host}/RemoteSignature?token={token}&id={id}";

                // Destinatario: recapiti dell'azienda del ticket
                var company = intervention.Ticket?.Company;
                var phone = NormalizePhone(company?.Mobile ?? company?.Telefono);
                var email = company?.Email;

                string channel;
                string sentTo;

                if (!string.IsNullOrWhiteSpace(phone) && _smsSender.IsConfigured &&
                    await _smsSender.SendAsync(phone,
                        $"Firma il verbale dell'intervento #{intervention.Ticket?.Id}: {link}"))
                {
                    channel = "sms";
                    sentTo = MaskPhone(phone);
                }
                else if (!string.IsNullOrWhiteSpace(email))
                {
                    var subject = $"Firma verbale intervento #{intervention.Ticket?.Id}";
                    var message = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                            <h2 style='color: #0066cc;'>Firma del verbale di intervento</h2>
                            <p>Gentile cliente,</p>
                            <p>per firmare il verbale dell'intervento <strong>#{intervention.Ticket?.Id}</strong>, clicchi sul pulsante qui sotto:</p>
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{link}'
                                   style='background: #0066cc; color: white; padding: 15px 40px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>
                                    ✍️ FIRMA IL DOCUMENTO
                                </a>
                            </div>
                            <p style='color: #6c757d; font-size: 12px;'>Se il pulsante non funziona, copi e incolli questo indirizzo: {link}</p>
                        </div>";

                    await _emailSender.SendEmailAsync(email, subject, message);
                    channel = "email";
                    sentTo = MaskEmail(email);
                }
                else
                {
                    return StatusCode(422, new { error = "Nessun recapito disponibile per l'invio del link" });
                }

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RequestRemoteSignature),
                    LogEvent.EventsTypes.Info,
                    $"Link firma remota inviato per intervention #{id} - Canale: {channel} - Dest: {sentTo}");

                return Ok(new RemoteSignatureRequestResponse { Success = true, Channel = channel, SentTo = sentTo });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RequestRemoteSignature),
                    LogEvent.EventsTypes.Error,
                    $"Errore invio link firma remota: {ex.Message}");
                return StatusCode(500, new { error = "Errore invio link di firma" });
            }
        }

        /// <summary>
        /// (Pubblico) Valida il token e restituisce le info per la pagina di firma.
        /// </summary>
        [HttpGet("RemoteSignatureInfo")]
        [AllowAnonymous]
        public async Task<ActionResult<RemoteSignatureInfoResponse>> RemoteSignatureInfo([FromQuery] string token, [FromQuery] int id)
        {
            var intervention = await _context.TicketsInterventions
                .Include(x => x.Ticket)
                    .ThenInclude(t => t.Company)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (intervention == null || string.IsNullOrWhiteSpace(token) ||
                intervention.SignatureConfirmationToken != token)
            {
                return Ok(new RemoteSignatureInfoResponse { Valid = false });
            }

            return Ok(new RemoteSignatureInfoResponse
            {
                Valid = true,
                TicketId = intervention.Ticket?.Id ?? 0,
                Company = intervention.Ticket?.Company?.RagioneSociale ?? string.Empty,
                AlreadySigned = intervention.SignatureStatus == CRM.Shared.SignatureStatus.Verified
            });
        }

        /// <summary>
        /// (Pubblico) Il cliente invia la firma tracciata dalla pagina remota.
        /// </summary>
        [HttpPost("SubmitRemoteSignature")]
        [AllowAnonymous]
        public async Task<IActionResult> SubmitRemoteSignature([FromBody] RemoteSignatureSubmit data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Token))
                return BadRequest(new { error = "Richiesta non valida" });

            var intervention = await _context.TicketsInterventions.FindAsync(data.InterventionId);
            if (intervention == null)
                return NotFound();

            if (intervention.SignatureConfirmationToken != data.Token)
                return StatusCode(401, new { error = "Link non valido o già utilizzato" });

            if (string.IsNullOrWhiteSpace(data.Signature) || string.IsNullOrWhiteSpace(data.SignerName))
                return BadRequest(new { error = "Firma e nome del firmatario sono obbligatori" });

            intervention.CustomerSignature = data.Signature;
            intervention.SignatureName = data.SignerName;
            intervention.SignatureDate = DateTime.Now;
            intervention.SignatureStatus = CRM.Shared.SignatureStatus.Verified;
            intervention.SignatureConfirmationToken = null; // monouso
            await _context.SaveChangesAsync();

            try
            {
                await CreatePdf(data.InterventionId);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(SubmitRemoteSignature),
                    LogEvent.EventsTypes.Error,
                    $"Firma remota salvata ma errore rigenerazione PDF #{data.InterventionId}: {ex.Message}");
            }

            await _logEventService.RegisterAsync(
                nameof(TicketInterventionsController),
                nameof(SubmitRemoteSignature),
                LogEvent.EventsTypes.Info,
                $"Firma remota acquisita per intervention #{data.InterventionId} - Firmatario: {data.SignerName}");

            return Ok(new { success = true });
        }

        private string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "***";

            var parts = email.Split('@');
            if (parts.Length != 2) return "***";

            var localPart = parts[0];
            var domain = parts[1];

            if (localPart.Length <= 3)
                return $"***@{domain}";

            return $"{localPart[0]}***{localPart[^1]}@{domain}";
        }

        /// <summary>Maschera un numero mostrando solo le ultime 3 cifre (es. "•••567").</summary>
        private static string MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "***";
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 3) return "***";
            return "•••" + digits[^3..];
        }

        /// <summary>
        /// Normalizza un numero al formato E.164 (+prefisso). I numeri senza prefisso
        /// ricevono <see cref="SmsOptions.DefaultCountryPrefix"/>.
        /// </summary>
        private string NormalizePhone(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            raw = raw.Trim();
            bool hasPlus = raw.StartsWith("+");
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return string.Empty;

            if (hasPlus) return "+" + digits;
            if (digits.StartsWith("00")) return "+" + digits[2..];

            var prefix = string.IsNullOrWhiteSpace(_smsOptions.DefaultCountryPrefix) ? "+39" : _smsOptions.DefaultCountryPrefix;
            return prefix + digits;
        }

        /// <summary>
        /// Cerca il cellulare del firmatario tra utenti e contatti che hanno la stessa email.
        /// </summary>
        private async Task<string?> ResolvePhoneByEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var userPhone = await _context.Users
                .Where(u => u.Email == email)
                .Select(u => u.PhoneNumber)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(userPhone)) return userPhone;

            var contact = await _context.Contacts
                .Where(c => c.Email == email)
                .Select(c => new { c.Mobile, c.Phone })
                .FirstOrDefaultAsync();
            if (contact != null)
                return !string.IsNullOrWhiteSpace(contact.Mobile) ? contact.Mobile : contact.Phone;

            return null;
        }

        /// <summary>
        /// Salva la firma digitale del cliente con conferma email a posteriori
        /// </summary>
        [HttpPost("SaveSignatureWithEmailConfirmation/{id}")]
        public async Task<ActionResult<SignatureSaveResponse>> SaveSignatureWithEmailConfirmation(int id, [FromBody] SignatureDataWithEmail signatureData)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                    .FirstOrDefaultAsync(x => x.Id == id);
                
                if (intervention == null)
                    return NotFound();

                if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                    return Problem("Not Permits");

                // Genera token univoco per conferma
                var confirmationToken = Guid.NewGuid().ToString("N");

                // Salva firma come PENDING
                intervention.CustomerSignature = signatureData.Signature;
                intervention.SignatureName = signatureData.SignerName;
                intervention.SignatureEmail = signatureData.SignerEmail;
                intervention.SignatureDate = DateTime.Now;
                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Pending;
                intervention.SignatureConfirmationToken = confirmationToken;
                
                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Prepara allegati (report PDF se esiste)
                List<string> attachments = new List<string>();
                string reportPath = _archiveService.GetPath(id, "pdf");
                
                if (System.IO.File.Exists(reportPath))
                {
                    attachments.Add(reportPath);
                }
                else
                {
                    // Se il report non esiste, generalo prima di inviare l'email
                    var generatedPath = await CreatePdf(id);
                    if (!string.IsNullOrEmpty(generatedPath))
                    {
                        attachments.Add(generatedPath);
                    }
                }

                // Lingua del destinatario per la pagina di conferma (link email)
                var culture = await ResolveRecipientCulture(signatureData.SignerEmail);

                // Invia email di conferma con allegato
                var confirmationUrl = $"{Request.Scheme}://{Request.Host}/ConfirmSignature?token={confirmationToken}&id={id}&action=confirm&culture={culture}";
                var rejectUrl = $"{Request.Scheme}://{Request.Host}/ConfirmSignature?token={confirmationToken}&id={id}&action=reject&culture={culture}";
                
                var subject = $"Conferma Firma Intervento #{intervention.Ticket.Id}";
                var message = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <h2 style='color: #0066cc;'>Conferma Firma Digitale</h2>
                        <p>Gentile <strong>{signatureData.SignerName}</strong>,</p>
                        <p>Hai appena firmato digitalmente il verbale di intervento <strong>#{intervention.Ticket.Id}</strong>.</p>
                        
                        <div style='background: #f8f9fa; padding: 15px; border-left: 4px solid #0066cc; margin: 20px 0;'>
                            <p style='margin: 0;'><strong>Dettagli Intervento:</strong></p>
                            <ul style='margin: 10px 0;'>
                                <li>Data: {intervention.StartDateTime:dd/MM/yyyy}</li>
                                <li>Tecnici: {string.Join(", ", intervention.AssignedUsers.Select(u => u.User.NameComplete))}</li>
                            </ul>
                        </div>

                        <p>📎 <strong>In allegato trovi il report firmato dell'intervento.</strong></p>

                        <p><strong style='color: #dc3545;'>⚠️ Azione Richiesta:</strong></p>
                        <p>Per rendere valida la firma, conferma cliccando il pulsante qui sotto:</p>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{confirmationUrl}' 
                               style='background: #28a745; color: white; padding: 15px 40px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>
                                ✅ CONFERMA FIRMA
                            </a>
                        </div>

                        <p style='color: #6c757d; font-size: 12px;'>
                            Se non hai firmato questo documento, ignora questa email o 
                            <a href='{rejectUrl}'>clicca qui per rifiutare</a>.
                        </p>

                        <p style='color: #6c757d; font-size: 12px;'>
                            Questo link è valido per 7 giorni e può essere usato una sola volta.
                        </p>
                    </div>
                ";

                // Usa l'overload con allegati e EmailsTypes.InvioDocumento
                List<string> recipients = new List<string> { signatureData.SignerEmail };
                await _emailSender.SendEmailAsync(
                    recipients,
                    EmailsTypes.SignatureConfirm,
                    attachments.Count > 0 ? attachments : null,
                    subject,
                    message,
                    null, // keyValues
                    null  // cc
                );

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(SaveSignatureWithEmailConfirmation),
                    LogEvent.EventsTypes.Info,
                    $"Firma salvata (PENDING) per intervention #{id} - Firmatario: {signatureData.SignerName} - Email: {signatureData.SignerEmail} - Allegati: {attachments.Count}");

                return Ok(new SignatureSaveResponse
                {
                    Success = true,
                    Status = "pending",
                    Message = $"Firma salvata. Email di conferma con report allegato inviata a {MaskEmail(signatureData.SignerEmail)}",
                    ConfirmationRequired = true
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(SaveSignatureWithEmailConfirmation),
                    LogEvent.EventsTypes.Error,
                    $"Errore salvataggio firma: {ex.Message}");
                return StatusCode(500, new { error = "Errore salvataggio firma" });
            }
        }

        /// <summary>
        /// Determina la lingua del destinatario per la pagina di conferma firma.
        /// Priorità: utente con la stessa email → cultura della richiesta corrente
        /// (lingua del tecnico) → italiano. Normalizzata alle culture supportate.
        /// </summary>
        private async Task<string> ResolveRecipientCulture(string? email)
        {
            var supported = new[] { "en", "it", "fr", "de", "es" };
            string? lang = null;

            if (!string.IsNullOrWhiteSpace(email))
            {
                lang = await _context.Users
                    .Where(u => u.Email == email)
                    .Select(u => u.LanguageCode)
                    .FirstOrDefaultAsync();
            }

            // Fallback: cultura della richiesta corrente (impostata da RequestLocalization)
            if (string.IsNullOrWhiteSpace(lang))
                lang = CultureInfo.CurrentUICulture.Name;

            var two = lang.Split('-', '_')[0].ToLowerInvariant();
            return supported.Contains(two) ? two : "it";
        }

        /// <summary>
        /// Salva la firma digitale del cliente (senza conferma email)
        /// </summary>
        [HttpPost("SaveSignature/{id}")]
        public async Task<ActionResult> SaveSignature(int id, [FromBody] SignatureData signatureData)
        {
            try
            {
                var intervention = await _context.TicketsInterventions.FindAsync(id);
                
                if (intervention == null)
                    return NotFound(new { error = _localizer["InterventionNotFound"].Value });

                if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                    return Problem(_localizer["NotPermits"].Value);

                // Salva firma direttamente (senza conferma)
                intervention.CustomerSignature = signatureData.Signature;
                intervention.SignatureName = signatureData.SignerName;
                intervention.SignatureDate = DateTime.Now;
                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Verified;
                
                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(SaveSignature),
                    LogEvent.EventsTypes.Info,
                    $"{_localizer["Signature saved for intervention"]} #{id} - Firmatario: {signatureData.SignerName}");

                return Ok(new { success = true, message = _localizer["SignatureSavedSuccessfully"].Value });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(SaveSignature),
                    LogEvent.EventsTypes.Error,
                    $"Errore salvataggio firma: {ex.Message}");
                return StatusCode(500, new { error = "Errore salvataggio firma" });
            }
        }

        /// <summary>
        /// Conferma firma tramite link email (API per pagina Blazor)
        /// </summary>
        [HttpGet("ConfirmSignature")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmSignature([FromQuery] string token, [FromQuery] int id)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                    .FirstOrDefaultAsync(x => x.Id == id);
                
                if (intervention == null)
                    return NotFound(new { error = "Intervento non trovato" });

                if (intervention.SignatureConfirmationToken != token)
                    return BadRequest(new { error = "Token non valido" });

                if (intervention.SignatureStatus == CRM.Shared.SignatureStatus.Verified)
                    return Ok(new { 
                        success = true, 
                        message = "Firma già confermata",
                        ticketId = intervention.Ticket.Id
                    });

                // Conferma firma
                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Verified;
                intervention.SignatureConfirmedDate = DateTime.Now;
                intervention.SignatureConfirmationToken = null; // Invalida token

                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(ConfirmSignature),
                    LogEvent.EventsTypes.Info,
                    $"Firma confermata per intervention #{id} - Email: {intervention.SignatureEmail}");

                // ✅ RIGENERA PDF con stato Verified
                try
                {
                    await _logEventService.RegisterAsync(
                        nameof(TicketInterventionsController),
                        nameof(ConfirmSignature),
                        LogEvent.EventsTypes.Info,
                        $"Rigenerazione PDF dopo conferma firma per intervention #{id}");

                    await CreatePdf(id);

                    await _logEventService.RegisterAsync(
                        nameof(TicketInterventionsController),
                        nameof(ConfirmSignature),
                        LogEvent.EventsTypes.Info,
                        $"PDF rigenerato con successo dopo conferma firma per intervention #{id}");
                }
                catch (Exception pdfEx)
                {
                    // Log errore ma non bloccare la conferma
                    await _logEventService.RegisterAsync(
                        nameof(TicketInterventionsController),
                        nameof(ConfirmSignature),
                        LogEvent.EventsTypes.Error,
                        $"Errore rigenerazione PDF per intervention #{id}: {pdfEx.Message}");
                }

                return Ok(new { 
                    success = true, 
                    message = "Firma confermata con successo",
                    ticketId = intervention.Ticket.Id,
                    confirmedDate = intervention.SignatureConfirmedDate
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(ConfirmSignature),
                    LogEvent.EventsTypes.Error,
                    $"Errore conferma firma: {ex.Message}");
                return StatusCode(500, new { error = "Errore durante la conferma" });
            }
        }

        /// <summary>
        /// Rifiuta firma tramite link email (API per pagina Blazor)
        /// </summary>
        [HttpGet("RejectSignature")]
        [AllowAnonymous]
        public async Task<IActionResult> RejectSignature([FromQuery] string token, [FromQuery] int id)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                    .FirstOrDefaultAsync(x => x.Id == id);
                
                if (intervention == null || intervention.SignatureConfirmationToken != token)
                    return BadRequest(new { error = "Token non valido" });

                intervention.SignatureStatus = CRM.Shared.SignatureStatus.Rejected;
                intervention.SignatureConfirmedDate = DateTime.Now;
                intervention.SignatureConfirmationToken = null;

                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RejectSignature),
                    LogEvent.EventsTypes.Warning,
                    $"Firma RIFIUTATA per intervention #{id} - Email: {intervention.SignatureEmail}");

                return Ok(new { 
                    success = true, 
                    message = "Firma rifiutata",
                    ticketId = intervention.Ticket.Id
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RejectSignature),
                    LogEvent.EventsTypes.Error,
                    $"Errore rifiuto firma: {ex.Message}");
                return StatusCode(500, new { error = "Errore" });
            }
        }

        /// <summary>
        /// Rinvia email di conferma firma (se l'utente non l'ha ricevuta o ha sbagliato email)
        /// </summary>
        [HttpPost("ResendSignatureConfirmation/{id}")]
        public async Task<ActionResult> ResendSignatureConfirmation(int id, [FromBody] ResendEmailRequest request)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                    .FirstOrDefaultAsync(x => x.Id == id);
                
                if (intervention == null)
                    return NotFound();

                if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                    return Problem("Not Permits");

                // Verifica che ci sia una firma in pending
                if (intervention.SignatureStatus != CRM.Shared.SignatureStatus.Pending)
                {
                    return BadRequest(new { error = "Nessuna firma in attesa di conferma" });
                }

                // Aggiorna email se diversa
                var oldEmail = intervention.SignatureEmail;
                var newEmail = request.Email?.Trim();

                if (string.IsNullOrWhiteSpace(newEmail))
                {
                    return BadRequest(new { error = "Email non valida" });
                }

                intervention.SignatureEmail = newEmail;

                // Genera nuovo token se email cambiata (per sicurezza)
                if (oldEmail != newEmail)
                {
                    intervention.SignatureConfirmationToken = Guid.NewGuid().ToString("N");
                }

                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Prepara allegati (report PDF)
                List<string> attachments = new List<string>();
                string reportPath = _archiveService.GetPath(id, "pdf");
                
                if (System.IO.File.Exists(reportPath))
                {
                    attachments.Add(reportPath);
                }
                else
                {
                    var generatedPath = await CreatePdf(id);
                    if (!string.IsNullOrEmpty(generatedPath))
                    {
                        attachments.Add(generatedPath);
                    }
                }

                // Invia email con nuovo/stesso token
                var confirmationUrl = $"{Request.Scheme}://{Request.Host}/ConfirmSignature?token={intervention.SignatureConfirmationToken}&id={id}&action=confirm";
                var rejectUrl = $"{Request.Scheme}://{Request.Host}/ConfirmSignature?token={intervention.SignatureConfirmationToken}&id={id}&action=reject";
                
                var subject = $"[RINVIO] Conferma Firma Intervento #{intervention.Ticket.Id}";
                var message = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <h2 style='color: #0066cc;'>Conferma Firma Digitale</h2>
                        <p>Gentile <strong>{intervention.SignatureName}</strong>,</p>
                        <p>Questo è un <strong>rinvio</strong> dell'email di conferma per il verbale di intervento <strong>#{intervention.Ticket.Id}</strong>.</p>
                        
                        <div style='background: #f8f9fa; padding: 15px; border-left: 4px solid #0066cc; margin: 20px 0;'>
                            <p style='margin: 0;'><strong>Dettagli Intervento:</strong></p>
                            <ul style='margin: 10px 0;'>
                                <li>Data: {intervention.StartDateTime:dd/MM/yyyy}</li>
                                <li>Tecnici: {string.Join(", ", intervention.AssignedUsers.Select(u => u.User.NameComplete))}</li>
                            </ul>
                        </div>

                        <p>📎 <strong>In allegato trovi il report firmato dell'intervento.</strong></p>

                        <p><strong style='color: #dc3545;'>⚠️ Azione Richiesta:</strong></p>
                        <p>Per rendere valida la firma, conferma cliccando il pulsante qui sotto:</p>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{confirmationUrl}' 
                               style='background: #28a745; color: white; padding: 15px 40px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>
                                ✅ CONFERMA FIRMA
                            </a>
                        </div>

                        <p style='color: #6c757d; font-size: 12px;'>
                            Se non hai firmato questo documento, ignora questa email o 
                            <a href='{rejectUrl}'>clicca qui per rifiutare</a>.
                        </p>

                        <p style='color: #6c757d; font-size: 12px;'>
                            Questo link è valido per 7 giorni e può essere usato una sola volta.
                        </p>
                    </div>
                ";

                List<string> recipients = new List<string> { newEmail };
                await _emailSender.SendEmailAsync(
                    recipients,
                    EmailsTypes.InvioDocumento,
                    attachments.Count > 0 ? attachments : null,
                    subject,
                    message,
                    null,
                    null
                );

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(ResendSignatureConfirmation),
                    LogEvent.EventsTypes.Info,
                    $"Email conferma firma RINVIATA per intervention #{id} - Email: {oldEmail} → {newEmail}");

                return Ok(new { success = true, message = $"Email rinviata a {newEmail}" });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(ResendSignatureConfirmation),
                    LogEvent.EventsTypes.Error,
                    $"Errore rinvio email: {ex.Message}");
                return StatusCode(500, new { error = "Errore durante l'invio" });
            }
        }

        private async Task InterventionType(int idTicketIntervention, List<int> interventionTypesId)
        {
            var intervention = await _context.TicketsInterventions.Include(x => x.TicketInterventionsTypes).Where(x => x.Id == idTicketIntervention).FirstOrDefaultAsync();

            if (intervention != null)
            {
                _context.Entry(intervention).State = EntityState.Modified;

                interventionTypesId = interventionTypesId ?? new List<int>();

                var list = intervention.TicketInterventionsTypes.Where(x => !interventionTypesId.Contains(x.Id)).ToList();
                foreach (var item in list)
                {
                    intervention.TicketInterventionsTypes.Remove(item);
                }

                foreach (var id in interventionTypesId)
                {
                    if (!intervention.TicketInterventionsTypes.Where(x => x.Id == id).Any())
                    {
                        var interventionType = await _context.InterventionTypes.FindAsync(id);
                        intervention.TicketInterventionsTypes.Add(interventionType);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }
      

        private async Task<string> CreatePdf(int id, string? languageCode = null)
        {
            try
            {
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.AssignedUsers)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (intervention == null)
                {
                    await _logEventService.RegisterAsync(
                        nameof(TicketInterventionsController),
                        nameof(CreatePdf),
                        LogEvent.EventsTypes.Warning,
                        $"Intervention #{id} non trovato");
                    return null;
                }

                if (string.IsNullOrEmpty(languageCode))
                {
                    var currentUser = await _permitsService.GetUser();
                    languageCode = currentUser?.LanguageCode ?? "en";
                }

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(CreatePdf),
                    LogEvent.EventsTypes.Info,
                    $"Generazione PDF per intervention #{id} con QuestPDF (lingua: {languageCode})");

                byte[] pdfBytes = await _pdfGenerator.GenerateInterventionPdfAsync(id, languageCode);

                string path = _archiveService.GetPath(id, "pdf");
                await System.IO.File.WriteAllBytesAsync(path, pdfBytes);

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(CreatePdf),
                    LogEvent.EventsTypes.Info,
                    $"PDF generato con successo per intervention #{id}: {pdfBytes.Length} bytes");

                return path;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(CreatePdf),
                    LogEvent.EventsTypes.Error,
                    $"Errore generazione PDF intervention #{id}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private async Task UpdateArticles(TicketIntervention ticketIntervention)
        {
            var articles =  await _context.TicketInterventionArticles.Where(x=>x.IdTicketIntervention == ticketIntervention.Id).ToListAsync();

            _context.TicketInterventionArticles.RemoveRange(articles);

            await _context.SaveChangesAsync();

            foreach (var item in ticketIntervention.InterventionArticles)
            {
                _context.TicketInterventionArticles.Add(new TicketInterventionArticle() { IdArticle = item.IdArticle, IdProduct = item.IdProduct, SerialNumber = item.SerialNumber, Description = item.Description, 
                    IdTicketIntervention = ticketIntervention.Id});
            }
            await _context.SaveChangesAsync();

        }
        
        private bool TicketInterventionExists(int id)
        {
            return _context.TicketsInterventions.Any(e => e.Id == id);
        }

        private bool ExistReport(int id)
        {
            string path = PathReportIntervention(id);

            return System.IO.File.Exists(path);
        }

        private string PathReportIntervention(int id)
        {
            return  Path.Combine(_archiveService.GetPath(), $"{id}.pdf");
        }

        private async Task<int?> TicketGetIdCompany(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);
            if (ticket != null)
            {
                return ticket.IdCompany;
            }
            else
                return null;
        }

        private string? ProductName(int? IdProduct)
        {
            var product = _context.Products.Find(IdProduct);

            return product?.Name;
        }

        public string? ArticleSN(int? IdArticle)
        {
            var article = _context.Articles.Find(IdArticle);
            return article?.SerialNumber;
        }

        private string GetDocumentPath(string value)
        {
            if (int.TryParse(value, out int id))
            {
                string path = _archiveService.GetPath(id, "pdf");

                return path;

            }
            else
                return string.Empty;

        }
    }
}
