using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Server.Services;
using Newtonsoft.Json;
using CRM.Server.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Linq.Dynamic.Core;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Data;
using CRM.Shared.Extensions;
using SelectPdf;
using CRM.Client.Helpers;
using CRM.Server.Extensions;
using CRM.Shared.Resources.Models;
using Microsoft.AspNetCore.Authorization;
using CRM.Client.Services;
using Microsoft.Identity.Client; // ✅ AGGIUNTO

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

        public TicketInterventionsController(ApplicationDbContext context, IPermitsService permitsService, IArchiveService archiveService,
            IWebHostEnvironment hostEnvironment, ILogEventService logEventService, IEmailSenderPlus emailSender, IInterventionPdfGenerator pdfGenerator,
            ISignatureOtpService otpService)
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

                var list = await items.ToListAsync();

                foreach (var item in list)
                {
                    int? idCompany = await TicketGetIdCompany(item.IdTicket);

                    if (idCompany != null)
                    {
                        item.Permits = await _permitsService.ObjectPermits(idCompany, item.IdUser);
                        item.UserName = (await _context.Users.FindAsync(item.IdUser))?.UserName;
                    }
                }

                return list;
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
            var ticketIntervention = await _context.TicketsInterventions.Include(x=>x.TicketInterventionsTypes).Where(x => x.Id == id).FirstOrDefaultAsync();

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


            ticketIntervention.InterventionArticles = await _context.TicketInterventionArticles.Where(x=>x.IdTicketIntervention == id).Select(x=> new TicketInterventionArticleModel() {Id = Guid.NewGuid(), IdArticle = x.IdArticle, IdProduct = x.IdProduct, 
                Description = x.Description, IdTicketIntervention = x.IdTicketIntervention, IdLink = x.Id, Product =  x.Product.Name, Article =  x.Article.SerialNumber
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
                    string path = _archiveService.GetPath(id, "pdf");
                    _archiveService.SaveAttachments(id, file.ContentType, file.Content);

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

            var state = await _emailSender.SendEmailAsync(email.To.ToList(";"), EmailsTypes.InvioDocumento, new List<string>() { { path } }, email.Subject, email.Message, null, email.CC);

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

                // Rate limiting: max 1 OTP ogni 60 secondi
                if (intervention.SignatureOtpExpiry.HasValue && 
                    intervention.SignatureOtpExpiry.Value > DateTime.Now.AddSeconds(-60))
                {
                    return StatusCode(429, new { error = "Troppo presto. Riprova tra " + 
                        (int)(intervention.SignatureOtpExpiry.Value.AddSeconds(-60) - DateTime.Now).TotalSeconds + " secondi" });
                }

                // Genera OTP e challenge
                var otp = _otpService.GenerateOtp();
                var challengeId = _otpService.GenerateChallengeId();
                var otpHash = _otpService.HashOtp(otp, challengeId);

                // Salva stato temporaneo
                intervention.SignatureOtpHash = otpHash;
                intervention.SignatureOtpChallengeId = challengeId;
                intervention.SignatureOtpExpiry = DateTime.Now.AddMinutes(5); // TTL 5 min
                intervention.SignatureOtpAttempts = 0;
                intervention.PendingSignature = signatureData.Signature;
                intervention.PendingSignatureName = signatureData.SignerName;

                _context.Entry(intervention).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // ✅ INVIO OTP via EMAIL (in produzione usare SMS provider)
                var companyEmail = intervention.Ticket?.Company?.Email;
                if (!string.IsNullOrWhiteSpace(companyEmail))
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

                    await _emailSender.SendEmailAsync(companyEmail, subject, message);
                }

                await _logEventService.RegisterAsync(
                    nameof(TicketInterventionsController),
                    nameof(RequestSignatureOtp),
                    LogEvent.EventsTypes.Info,
                    $"OTP generato per intervention #{id} - Email: {companyEmail} - Challenge: {challengeId.Substring(0, 8)}...");

                return Ok(new OtpRequestResponse
                {
                    Success = true,
                    ChallengeId = challengeId,
                    ExpiresAt = intervention.SignatureOtpExpiry.Value,
                    SentTo = MaskEmail(companyEmail)
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

                if (!await _permitsService.BelongsToMainCompany())
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

                // Invia email di conferma con allegato
                var confirmationUrl = $"{Request.Scheme}://{Request.Host}/ConfirmSignature?token={confirmationToken}&id={id}&action=confirm";
                var rejectUrl = $"{Request.Scheme}://{Request.Host}/ConfirmSignature?token={confirmationToken}&id={id}&action=reject";
                
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
                                <li>Tecnico: {intervention.User?.NameComplete}</li>
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
                                <li>Tecnico: {intervention.User?.NameComplete}</li>
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
                    .Include(x => x.User)
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

    /// <summary>
    /// DTO per ricevere firma con nome firmatario
    /// </summary>
    public class SignatureData
    {
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO per firma in attesa di verifica OTP
    /// </summary>
    public class SignaturePendingData
    {
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risposta richiesta OTP
    /// </summary>
    public class OtpRequestResponse
    {
        public bool Success { get; set; }
        public string ChallengeId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string SentTo { get; set; } = string.Empty;
    }

    /// <summary>
    /// Richiesta verifica OTP
    /// </summary>
    public class OtpVerifyRequest
    {
        public string ChallengeId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risposta verifica OTP
    /// </summary>
    public class OtpVerifyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO per salvare firma con email
    /// </summary>
    public class SignatureDataWithEmail
    {
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
        public string SignerEmail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risposta salvataggio firma
    /// </summary>
    public class SignatureSaveResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty; // "pending", "verified"
        public string Message { get; set; } = string.Empty;
        public bool ConfirmationRequired { get; set; }
    }

    /// <summary>
    /// Richiesta rinvio email conferma
    /// </summary>
    public class ResendEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public enum SignatureStatus
    {
        Pending,
        Verified,
        Rejected
    }
}
