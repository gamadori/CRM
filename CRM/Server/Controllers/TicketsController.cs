using System;
using CNM.Authorize;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Identity;
using CRM.Server.Extensions;
using CRM.Server.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq.Dynamic.Core;
using CRM.Shared.Helper;
using CRM.Server.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.IdentityModel.Tokens;
using CRM.Client.Pages.DashBoard;
using Microsoft.Data.SqlClient;
using Humanizer;
using CRM.Client.Services;
using System.Composition;
using SelectPdf;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using CRM.Server.Models;
using CRM.Shared.DTOs;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permits;
        private readonly ILogEventService _logEventService;
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly ILanguagesService _languageService;
        private readonly TelegramCommandsService _TelegramService;
        private readonly IArchiveService _archiveService;
        private readonly OpenAIEmbeddingService _embeddingService;
        private readonly ITicketPdfGenerator _pdfGenerator;
        private readonly IPushNotificationService _pushService;
        private readonly Services.ITicketsService _ticketsService;
        private readonly ITicketSummaryService _ticketSummaryService;
        private readonly ITicketNotificationService _ticketNotifications;

        // ✅ NUOVO: Aggiungi IConfiguration
        private readonly IConfiguration _configuration;

        public TicketsController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager, 
            IPermitsService permitsService, 
            ILogEventService logEventService, 
            IEmailSenderPlus emailSenderPlus, 
            
            TelegramCommandsService telegram, 
            IArchiveService archiveService, 
            OpenAIEmbeddingService embeddingService, 
            ITicketPdfGenerator pdfGenerator, 
            IPushNotificationService pushService,
            IConfiguration configuration,
            Services.ITicketsService ticketsService,
            ITicketSummaryService ticketSummaryService,
            ITicketNotificationService ticketNotifications) // ✅ AGGIUNTO
        {
            _context = context;
            _userManager = userManager;
            _permits = permitsService;
            _logEventService = logEventService;
            _emailSenderPlus = emailSenderPlus;
            
            _TelegramService = telegram;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Temp;
            _embeddingService = embeddingService;
            _pdfGenerator = pdfGenerator;
            _pushService = pushService;
            _configuration = configuration;
            _ticketsService = ticketsService;
            _ticketSummaryService = ticketSummaryService;
            _ticketNotifications = ticketNotifications;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<TicketDTO>>> SearchTickets([FromQuery] TicketFilter args)
        {
            try
            {
                List<object> parms = new List<object>();

                if (args.Top == null)
                    args.Top = 10;

                if (args.Skip == null)
                    args.Skip = 0;

                var select = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank FROM Tickets AS FT_TBL INNER JOIN FREETEXTTABLE(Tickets, Description, @Search, LANGUAGE N'Italian') AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY]";

                 //var select = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank FROM Tickets AS FT_TBL INNER JOIN CONTAINSTABLE(Tickets, Description, '@Search') AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY]";

                var pSearch = new SqlParameter("@Search", args.Search);

                parms.Add(pSearch);

                string where = string.Empty;

                if (args.IdCompany != null || args.IdProduct != null || args.IdArticle != null)
                {
                    where = "where ";

                    if (args.IdCompany != null)
                    {
                        where += $" FT_TBL.IdCompany = @IdCompany";
                        parms.Add(new SqlParameter("@IdCompany", args.IdCompany));
                    }
                    if (args.IdProduct != null)
                    {
                        where += " FT_TBL.IdProduct = @IdProduct";
                        parms.Add(new SqlParameter("@IdProduct", args.IdProduct));
                    }
                    if (args.IdArticle != null)
                    {
                        where += " FT_TBL.IdArticle = @IdArticle";
                        parms.Add(new SqlParameter("@IdArticle", args.IdArticle));
                    }
                }

                var orderby = $"ORDER BY KEY_TBL.RANK DESC OFFSET {args.Skip} ROWS FETCH NEXT {args.Top} ROWS ONLY";
                
                //FormattableString sql = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank FROM Tickets AS FT_TBL INNER JOIN FREETEXTTABLE(Tickets, Description, {args.Search}, LANGUAGE N'Italian', 2) AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY] ORDER BY KEY_TBL.RANK DESC OFFSET {args.Skip} ROWS FETCH NEXT {args.Top} ROWS ONLY";
                var sql = select + where;

                var total = await _context.Tickets.FromSqlRaw(sql, parms.ToArray()).CountAsync();

                var paginationMetadata = new
                {
                    totalCount = total,
                    
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                sql += orderby;


                List <TicketDTO> tickets = await _context.Tickets.FromSqlRaw(sql, parms.ToArray()).Select(x => new TicketDTO()
                {
                    Description = x.Description,
                    Id = x.Id,
                    Rank = x.RANK
                }).ToListAsync();

                return tickets;
            }
            catch (Exception ex)
            {
                return new List<TicketDTO>();
            }
        }

        // GET: api/Tickets
        [HttpGet]
        public async Task<ActionResult<ObjectView<TicketDTO, string>>> GetTicket([FromQuery] TicketFilter args)
        {
            try
            {
                
                var result = await _ticketsService.GetPagingAsync(args);

                var paginationMetadata = new
                {
                    totalCount = result.Items?.Count ?? 0,
                };

                // Ricalcola il totalCount dal result per il paging header
                // Il service restituisce tutti gli items paginati, il count deve essere gestito
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                return result;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("schedule-items")]
        public async Task<ActionResult<IEnumerable<TicketScheduleItemDTO>>> GetScheduleItems([FromQuery] TicketFilter args)
        {
            try
            {
                var tickets = _context.Tickets
                    .Include(t => t.Company)
                    .Include(t => t.State)
                    .Include(t => t.AssignedUsers)
                        .ThenInclude(a => a.User)
                    .AsQueryable();

                if (!await _permits.CanAccessOtherCompany())
                {
                    var idCompany = await _permits.GetIdCompany();
                    tickets = tickets.Where(t => t.IdCompany == idCompany);
                }

                if (args.DateFrom.HasValue)
                    tickets = tickets.Where(t => t.Date >= args.DateFrom || t.DateEnd >= args.DateFrom);

                if (args.DateTo.HasValue)
                {
                    var dateTo = args.DateTo.Value.Date == args.DateTo.Value
                        ? args.DateTo.Value.AddDays(1)
                        : args.DateTo.Value;

                    tickets = tickets.Where(t => t.Date < dateTo || t.DateEnd < dateTo);
                }

                if (!string.IsNullOrWhiteSpace(args.IdUserAssigned))
                {
                    tickets = args.ViewNotAssigned
                        ? tickets.Where(t => t.IdUserAssigned == args.IdUserAssigned || t.IdUserAssigned == null)
                        : tickets.Where(t => t.IdUserAssigned == args.IdUserAssigned
                                           || t.AssignedUsers.Any(a => a.IdUser == args.IdUserAssigned));
                }

                if (args.IdDeal != null)
                    tickets = tickets.Where(t => t.IdDeal == args.IdDeal);

                if (args.IdOrder != null)
                    tickets = tickets.Where(t => t.IdOrder == args.IdOrder);

                var items = await tickets
                    .OrderBy(t => t.Date)
                    .ThenBy(t => t.Time)
                    .Select(t => new TicketScheduleItemDTO
                    {
                        Id = t.Id,
                        Numero = t.Numero,
                        Date = t.Date,
                        Time = t.Time,
                        DateEnd = t.DateEnd,
                        DateExpired = t.DateExpired,
                        Company = t.Company != null ? t.Company.RagioneSociale : string.Empty,
                        Description = t.Description,
                        IdState = t.IdState,
                        State = t.State != null ? t.State.Description : string.Empty,
                        StateColor = t.State != null ? t.State.Color : string.Empty,
                        AssignedUsers = t.AssignedUsers
                            .OrderBy(a => a.AssignedDate)
                            .Select(a => new TicketScheduleUserDTO
                            {
                                Id = a.IdUser,
                                NameComplete = a.User != null ? a.User.NameComplete : string.Empty,
                                Color = a.User != null ? a.User.Color : null
                            })
                            .ToList()
                    })
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(items);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetScheduleItems), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Ticket>> GetTicket(int id)
        {
            try
            {
                var ticket = await _ticketsService.GetItemAsync(id);

                if (ticket == null)
                {
                    return NotFound();
                }
                else if (await _permits.CanGetObject(ticket.IdCompany))
                {
                    return Ok(ticket);
                }
                else
                {
                    await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Tickets/5
        [HttpGet("Details/{id}")]
        public async Task<ActionResult<TicketDTO>> GetTicketDetails(int id)
        {
            try
            {
                
                var ticketModel = await _ticketsService.GetDetailsAsync(id);

                if (ticketModel == null)
                {
                    return NotFound();
                }
                else if (await _permits.CanGetObject(ticketModel.IdCompany))
                {
                    return ticketModel;
                }
                else
                {
                    await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicketDetails), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicketDetails), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpPost("{id}/summary/propose")]
        public async Task<ActionResult<TicketSummaryProposalResponse>> ProposeSummary(int id, TicketSummaryProposalRequest request)
        {
            try
            {
                var response = await _ticketSummaryService.ProposeAsync(id, request ?? new TicketSummaryProposalRequest());
                if (response == null)
                    return NotFound();

                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(ProposeSummary), LogEvent.EventsTypes.Error, ex.Message);
                return Forbid();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(ProposeSummary), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpPut("{id}/summary")]
        public async Task<ActionResult<TicketDTO>> UpdateSummary(int id, UpdateTicketSummaryRequest request)
        {
            try
            {
                if (request == null || !ModelState.IsValid)
                    return BadRequest(ModelState);

                var response = await _ticketSummaryService.UpdateAsync(id, request);
                if (response == null)
                    return NotFound();

                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(UpdateSummary), LogEvent.EventsTypes.Error, ex.Message);
                return Forbid();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(UpdateSummary), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("Report/{id}")]
        public async Task<string?> CreateDocPdf(int id)
        {
            try
            {
                var idLang = await _languageService.GetIdLanguage();

                var pdf = await CreatePdf(id, idLang);

                return pdf;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(CreateDocPdf), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


       

        // PUT: api/Tickets/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicket(int id, Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return BadRequest();
            }
            
            var changeAssigned = await _ticketsService.TicketChangeAssigned(id, ticket.IdUserAssigned);

            if (!await _permits.CanWriteCompanyData(ticket.IdCompany))
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PutTicket), LogEvent.EventsTypes.Error, "Attempt to Edit without rights");
                return Forbid();
            }

            var result = await _ticketsService.PutAsync(id, ticket);

            if (!result)
            {
                return Problem("Error updating ticket");
            }

            if (changeAssigned)
            {
                var updatedTicket = await _context.Tickets.Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == id);
                if (updatedTicket != null)
                    await _ticketNotifications.NotifyTicketAssignedAsync(updatedTicket);
            }

            return NoContent();
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPut("{id}/schedule")]
        public async Task<IActionResult> UpdateSchedule(int id, TicketScheduleUpdateRequest request)
        {
            try
            {
                var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
                if (ticket == null)
                    return NotFound();

                if (!await _permits.CanWriteCompanyData(ticket.IdCompany))
                {
                    await _logEventService.RegisterAsync(nameof(TicketsController), nameof(UpdateSchedule), LogEvent.EventsTypes.Error, "Attempt to schedule ticket without object rights");
                    return Forbid();
                }

                var start = request.Date.Date + (request.Time?.ToTimeSpan() ?? TimeSpan.Zero);
                var end = request.DateEnd;
                if (end.HasValue && end.Value < start)
                    end = start;

                ticket.Date = request.Date.Date;
                ticket.Time = request.Time;
                ticket.DateEnd = end;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(UpdateSchedule), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPut("{id}/start-processing")]
        public async Task<IActionResult> StartProcessing(int id)
        {
            try
            {
                var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
                if (ticket == null)
                    return NotFound();

                if (!await _permits.CanWriteCompanyData(ticket.IdCompany))
                    return Forbid();

                if (ticket.Closed)
                    return BadRequest("Un ticket chiuso non puo essere messo in lavorazione.");

                var hasAssignedUser = !string.IsNullOrWhiteSpace(ticket.IdUserAssigned)
                    || await _context.TicketUserAssignments.AnyAsync(a => a.IdTicket == id);
                if (!hasAssignedUser)
                    return BadRequest("Assegna almeno un utente prima di mettere il ticket in lavorazione.");

                var processingStateId = await _context.TicketStates
                    .Where(s => s.State == (int)eTicketStates.Processing)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync();

                if (!processingStateId.HasValue)
                    return Problem("Lo stato In lavorazione non e configurato.");

                ticket.IdState = processingStateId.Value;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(StartProcessing), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

       
        // POST: api/Tickets
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Ticket>> PostTicket(Ticket ticket)
        {
            try
            {
                var savedTicket = await _ticketsService.PostAsync(ticket);

                await _ticketNotifications.NotifyNewTicketAsync(savedTicket);


                return CreatedAtAction("GetTicket", new { id = savedTicket.Id }, savedTicket);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PostTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }

            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PostTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        

        // DELETE: api/Tickets/5
        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            try
            {
                var ticket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                if (ticket == null)
                    return NotFound();

                if (!await _permits.CanWriteCompanyData(ticket.IdCompany))
                    return Forbid();

                var result = await _ticketsService.DeleteAsync(id);
                if (!result)
                    return NotFound();

                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(DeleteTicket), LogEvent.EventsTypes.Error, ex.Message);
                return Problem(ex.Message);
            }
        }

        [HttpGet("UsersToAssign/{id}")]
        public async Task<List<UserModel>> TicketGetUserToAssign(int id)
        {
            return await _ticketsService.GetUsersCanAssignTicketAsync(id);
        }

        [HttpGet("TypeUsersToAssign/{idType}")]
        public async Task<List<UserModel>> TicketTypeGetUserToAssign(int idType)
        {
            return await _ticketsService.GetUsersCanAssignTicketTypeAsync(idType);
        }

        [HttpPut("TicketClose/{id}")]
        public async Task<IActionResult> TicketClose(int id, TicketClose model)
        {
            try
            {
                if (id != model.Id)
                {
                    return BadRequest();
                }
                else if (await _permits.CanCloseTicket(id))
                {
                    var result = await _ticketsService.CloseAsync(id, model);

                    if (!result)
                        return Problem("Error closing ticket");

                    // Generate embedding after close
                    try
                    {
                        var ticket = await _context.Tickets.FindAsync(id);
                        if (ticket != null)
                        {
                            var ticketText = $"{ticket.Description} {ticket.CloseDescription}";
                            var embedding = await _embeddingService.GenerateEmbeddingAsync(ticketText);
                            ticket.DescriptionEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await _logEventService.RegisterAsync(nameof(TicketsController), nameof(TicketClose), LogEvent.EventsTypes.Error, ex);
                    }

                    return NoContent();
                }
                else
                    return BadRequest();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(TicketClose), LogEvent.EventsTypes.Error, ex.Message);
                return Problem(ex.Message);
            }
        }

        [HttpPut("TicketReOpen/{IdTicket}")]
        public async Task<ActionResult> ReOpen(int IdTicket, Ticket model)
        {
            try
            {

                if (IdTicket != model.Id)
                {
                    return BadRequest();
                }

                if (await _permits.CanReOpenTicket(IdTicket))
                {
                    await _ticketsService.ReOpenAsync(IdTicket);
                    return NoContent();
                }
                return NoContent();
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(ReOpen), LogEvent.EventsTypes.Error, ex);
                return BadRequest();
            }
        }

        [HttpPost("semantic-search")]
        public async Task<ActionResult<CRM.Shared.Models.SemanticSearchResponse>> SemanticSearch([FromBody] CRM.Shared.Models.SemanticSearchRequest request, [FromServices] OpenAIEmbeddingService embeddingService)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(request.ProblemDescription))
                {
                    return BadRequest("La descrizione del problema è obbligatoria");
                }

                // Validazione input: minimo 3 caratteri e almeno 2 parole
                var trimmedInput = request.ProblemDescription.Trim();
                if (trimmedInput.Length < 3)
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = 0,
                        EmbeddingGenerated = false,
                        Message = "La descrizione deve contenere almeno 3 caratteri"
                    });
                }

                // Controllo parole significative (almeno una parola di 3+ caratteri)
                var words = trimmedInput.Split(new[] { ' ', ',', '.', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                var hasValidWords = words.Any(w => w.Length >= 3 && System.Text.RegularExpressions.Regex.IsMatch(w, @"^[a-zA-Z0-9àèéìòùÀÈÉÌÒÙ]+$"));
                
                if (!hasValidWords)
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = 0,
                        EmbeddingGenerated = false,
                        Message = "La query deve contenere almeno una parola significativa (3+ caratteri alfanumerici)"
                    });
                }

                // Genera embedding per la query dell'utente
                var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.ProblemDescription);

                // Verifica che l'embedding generato non sia "nullo" o troppo simile a zero
                var embeddingMagnitude = Math.Sqrt(queryEmbedding.Sum(x => x * x));
                if (embeddingMagnitude < 0.1) // Soglia molto bassa per embedding validi
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        EmbeddingGenerated = false,
                        Message = "La query non contiene informazioni sufficienti per generare una ricerca valida"
                    });
                }

                // Recupera tutti i ticket chiusi con le loro descrizioni
                var query = _context.Tickets
                    .Include(x => x.Company)
                    .Include(x => x.TicketType)
                    .Include(x => x.UserClosed)
                    .Where(x => x.Closed == true && !string.IsNullOrEmpty(x.Description));

                // Filtra per permessi utente
                if (!await _permits.CanAccessOtherCompany())
                {
                    var idCompany = await _permits.GetIdCompany();
                    query = query.Where(x => x.IdCompany == idCompany);
                }

                var closedTickets = await query
                    .Select(x => new
                    {
                        x.Id,
                        x.Description,
                        x.CloseDescription,
                        x.DateClosed,
                        EmbeddingJson = x.DescriptionEmbedding,
                        CompanyName = x.Company.RagioneSociale,
                        Priority = x.Priority != null ? x.Priority.ToString() : "Normal"
                    })
                    .ToListAsync();

                // Calcola similarità per ogni ticket
                var results = new List<CRM.Shared.Models.TicketSimilarityResult>();

                foreach (var ticket in closedTickets)
                {
                    // Skip ticket senza embedding pre-calcolato
                    if (string.IsNullOrEmpty(ticket.EmbeddingJson))
                        continue;

                    try
                    {
                        // Deserializza embedding pre-calcolato
                        var ticketEmbedding = System.Text.Json.JsonSerializer
                            .Deserialize<float[]>(ticket.EmbeddingJson);

                        if (ticketEmbedding == null || ticketEmbedding.Length == 0)
                            continue;

                        var similarity = embeddingService.CalculateCosineSimilarity(
                            queryEmbedding, ticketEmbedding);
                        var percentage = embeddingService.CosineSimilarityToPercentage(similarity);

                        // SOGLIA ALZATA: solo risultati con almeno 60% di similarità
                        var effectiveThreshold = Math.Max(request.MinSimilarityThreshold, 60.0);

                        if (percentage >= effectiveThreshold)
                        {
                            results.Add(new CRM.Shared.Models.TicketSimilarityResult
                            {
                                TicketId = ticket.Id,
                                TicketNumber = $"#{ticket.Id}",
                                Title = ticket.Description?.Substring(0, Math.Min(100, ticket.Description.Length)) + "...",
                                Description = ticket.Description,
                                CustomerName = ticket.CompanyName,
                                SimilarityPercentage = Math.Round(percentage, 2),
                                CosineSimilarity = Math.Round(similarity, 4),
                                ClosedDate = ticket.DateClosed,
                                Solution = ticket.CloseDescription,
                                Priority = ticket.Priority
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log ma continua con altri ticket
                        await _logEventService.RegisterAsync(nameof(TicketsController), 
                            nameof(SemanticSearch), 
                            LogEvent.EventsTypes.Warning, 
                            $"Errore parsing embedding per ticket {ticket.Id}: {ex.Message}");
                        continue;
                    }
                }

                // Ordina per similarità e prendi i top risultati
                var topResults = results
                    .OrderByDescending(x => x.SimilarityPercentage)
                    .Take(request.TopResults)
                    .ToList();

                stopwatch.Stop();

                return new CRM.Shared.Models.SemanticSearchResponse
                {
                    Results = topResults,
                    TotalAnalyzed = closedTickets.Count,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    EmbeddingGenerated = queryEmbedding.Length > 0,
                    Message = topResults.Any() 
                        ? $"Trovati {topResults.Count} ticket simili (threshold 60%)" 
                        : "Nessun ticket simile trovato con similarità >= 60%. Prova a descrivere il problema in modo più dettagliato."
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SemanticSearch), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante la ricerca semantica: {ex.Message}");
            }
        }

        [HttpPost("hybrid-search")]
        public async Task<ActionResult<CRM.Shared.Models.SemanticSearchResponse>> HybridSearch(
            [FromBody] CRM.Shared.Models.SemanticSearchRequest request,
            [FromServices] OpenAIEmbeddingService embeddingService,
            [FromServices] OpenAIChatService chatService)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(request.ProblemDescription))
                {
                    return BadRequest("La descrizione del problema è obbligatoria");
                }

                // STEP 1: Validazione AI con GPT
                var validation = await chatService.ValidateITQueryAsync(request.ProblemDescription);
                if (!validation.IsValid)
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Message = $"❌ {validation.Reason}"
                    });
                }

                // STEP 2: SQL Full-Text Search (veloce e preciso!)
                var sqlResults = await SearchTickets(new TicketFilter
                {
                    Search = request.ProblemDescription,
                    Top = 20,
                    Skip = 0
                });

                if (sqlResults.Value == null || !sqlResults.Value.Any())
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Message = "Nessun ticket trovato con ricerca keyword. Prova con parole chiave diverse."
                    });
                }

                // STEP 3: Re-Ranking con Embeddings sui risultati SQL
                var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.ProblemDescription);
                var ticketIds = sqlResults.Value.Select(x => x.Id).ToList();

                var ticketsForReranking = await _context.Tickets
                    .Include(x => x.Company)
                    .Where(x => ticketIds.Contains(x.Id) && !string.IsNullOrEmpty(x.DescriptionEmbedding))
                    .Select(x => new
                    {
                        x.Id,
                        x.Description,
                        x.CloseDescription,
                        x.DateClosed,
                        EmbeddingJson = x.DescriptionEmbedding,
                        CompanyName = x.Company.RagioneSociale,
                        Priority = x.Priority != null ? x.Priority.ToString() : "Normal"
                    })
                    .ToListAsync();

                var rerankedResults = new List<CRM.Shared.Models.TicketSimilarityResult>();

                foreach (var ticket in ticketsForReranking)
                {
                    try
                    {
                        var ticketEmbedding = System.Text.Json.JsonSerializer
                            .Deserialize<float[]>(ticket.EmbeddingJson);

                        if (ticketEmbedding == null || ticketEmbedding.Length == 0)
                            continue;

                        var similarity = embeddingService.CalculateCosineSimilarity(
                            queryEmbedding, ticketEmbedding);
                        var percentage = embeddingService.CosineSimilarityToPercentage(similarity);

                        rerankedResults.Add(new CRM.Shared.Models.TicketSimilarityResult
                        {
                            TicketId = ticket.Id,
                            TicketNumber = $"#{ticket.Id}",
                            Title = ticket.Description?.Substring(0, Math.Min(100, ticket.Description.Length)) + "...",
                            Description = ticket.Description,
                            CustomerName = ticket.CompanyName,
                            SimilarityPercentage = Math.Round(percentage, 2),
                            CosineSimilarity = Math.Round(similarity, 4),
                            ClosedDate = ticket.DateClosed,
                            Solution = ticket.CloseDescription,
                            Priority = ticket.Priority
                        });
                    }
                    catch (Exception ex)
                    {
                        await _logEventService.RegisterAsync(nameof(TicketsController), 
                            nameof(HybridSearch), 
                            LogEvent.EventsTypes.Warning, 
                            $"Errore re-ranking ticket {ticket.Id}: {ex.Message}");
                        continue;
                    }
                }

                // Ordina per similarità AI e prendi top 20
                var top20Results = rerankedResults
                    .OrderByDescending(x => x.SimilarityPercentage)
                    .Take(20)
                    .ToList();

                if (!top20Results.Any())
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = sqlResults.Value.Count(),
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Message = "Ticket trovati ma senza embeddings validi per il ranking."
                    });
                }

                // STEP 4: AI VERIFICATION - GPT verifica se esiste una soluzione reale
                var ticketsForVerification = top20Results.Select(t => (
                    TicketId: t.TicketId,
                    Description: t.Description ?? "",
                    Solution: t.Solution ?? "",
                    Similarity: t.SimilarityPercentage
                )).ToList();

                var verification = await chatService.VerifySolutionRelevanceAsync(
                    request.ProblemDescription,
                    ticketsForVerification
                );

                // Se GPT ha trovato un best ticket, mettilo in cima
                List<CRM.Shared.Models.TicketSimilarityResult> finalResults;
                if (verification.HasRelevantSolution && verification.BestTicketId.HasValue)
                {
                    var bestTicket = top20Results.FirstOrDefault(t => t.TicketId == verification.BestTicketId.Value);
                    if (bestTicket != null)
                    {
                        // Rimuovi il best ticket e mettilo in cima
                        finalResults = new List<CRM.Shared.Models.TicketSimilarityResult> { bestTicket };
                        finalResults.AddRange(top20Results.Where(t => t.TicketId != verification.BestTicketId.Value));
                    }
                    else
                    {
                        finalResults = top20Results;
                    }
                }
                else
                {
                    finalResults = top20Results;
                }

                // STEP 5: GPT genera summary per il miglior risultato
                string aiSummary = "";
                if (verification.HasRelevantSolution && verification.BestTicketId.HasValue)
                {
                    var bestTicket = finalResults.First();
                    try
                    {
                        aiSummary = await chatService.GenerateSolutionSummaryAsync(
                            request.ProblemDescription,
                            bestTicket.Description ?? "",
                            bestTicket.Solution ?? ""
                        );
                    }
                    catch
                    {
                        aiSummary = verification.Reason;
                    }
                }

                // Prendi solo i risultati richiesti dall'utente
                var topResults = finalResults.Take(request.TopResults).ToList();

                stopwatch.Stop();

                string finalMessage;
                if (verification.HasRelevantSolution)
                {
                    finalMessage = $"✅ SOLUZIONE TROVATA! {aiSummary}";
                }
                else
                {
                    finalMessage = $"⚠️ Nessuna soluzione diretta trovata. {verification.Reason}. Ti mostro {topResults.Count} ticket simili che potrebbero aiutarti.";
                }

                return new CRM.Shared.Models.SemanticSearchResponse
                {
                    Results = topResults,
                    TotalAnalyzed = sqlResults.Value.Count(),
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    EmbeddingGenerated = true,
                    Message = finalMessage
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(HybridSearch), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante hybrid search: {ex.Message}");
            }
        }

        [HttpPost("generate-embeddings")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<object>> GenerateEmbeddingsForExistingTickets([FromQuery] int batchSize = 10)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Recupera ticket chiusi senza embedding
                var ticketsWithoutEmbedding = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.Description)
                        && (x.DescriptionEmbedding == null || x.DescriptionEmbedding == ""))
                    .OrderByDescending(x => x.DateClosed)
                    .Take(batchSize)
                    .ToListAsync();

                if (!ticketsWithoutEmbedding.Any())
                {
                    return Ok(new
                    {
                        message = "Tutti i ticket chiusi hanno già gli embeddings generati",
                        processed = 0,
                        remaining = 0
                    });
                }

                var totalRemaining = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.Description)
                        && (x.DescriptionEmbedding == null || x.DescriptionEmbedding == ""))
                    .CountAsync();

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var ticket in ticketsWithoutEmbedding)
                {
                    try
                    {
                        // Combina descrizione e soluzione
                        var ticketText = $"{ticket.Description} {ticket.CloseDescription ?? ""}";
                        
                        // Genera embedding
                        var embedding = await _embeddingService.GenerateEmbeddingAsync(ticketText);
                        
                        // Salva nel DB
                        ticket.DescriptionEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                        successCount++;

                        await _logEventService.RegisterAsync(
                            nameof(TicketsController), 
                            nameof(GenerateEmbeddingsForExistingTickets), 
                            LogEvent.EventsTypes.Info, 
                            $"Embedding generato per ticket #{ticket.Id}");
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var errorMsg = $"Ticket #{ticket.Id}: {ex.Message}";
                        errors.Add(errorMsg);
                        
                        await _logEventService.RegisterAsync(
                            nameof(TicketsController), 
                            nameof(GenerateEmbeddingsForExistingTickets), 
                            LogEvent.EventsTypes.Error, 
                            errorMsg);
                    }
                }

                // Salva modifiche
                await _context.SaveChangesAsync();

                stopwatch.Stop();

                return Ok(new
                {
                    message = $"Elaborazione completata",
                    processed = successCount,
                    errors = errorCount,
                    errorDetails = errors,
                    remainingTickets = totalRemaining - successCount,
                    processingTimeMs = stopwatch.ElapsedMilliseconds,
                    suggestion = totalRemaining - successCount > 0 
                        ? "Esegui nuovamente l'endpoint per processare il prossimo batch" 
                        : "Tutti i ticket sono stati processati!"
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(GenerateEmbeddingsForExistingTickets), 
                    LogEvent.EventsTypes.Error, 
                    ex);
                
                return Problem($"Errore durante la generazione degli embeddings: {ex.Message}");
            }
        }

        [HttpGet("embeddings-stats")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<object>> GetEmbeddingsStatistics()
        {
            try
            {
                var totalClosedTickets = await _context.Tickets
                    .Where(x => x.Closed == true)
                    .CountAsync();

                var ticketsWithEmbedding = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.DescriptionEmbedding))
                    .CountAsync();

                var ticketsWithoutEmbedding = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.Description)
                        && (x.DescriptionEmbedding == null || x.DescriptionEmbedding == ""))
                    .CountAsync();

                var ticketsWithoutDescription = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && string.IsNullOrEmpty(x.Description))
                    .CountAsync();

                var percentage = totalClosedTickets > 0 
                    ? Math.Round((double)ticketsWithEmbedding / totalClosedTickets * 100, 2) 
                    : 0;

                return Ok(new
                {
                    totalClosedTickets,
                    ticketsWithEmbedding,
                    ticketsWithoutEmbedding,
                    ticketsWithoutDescription,
                    completionPercentage = percentage,
                    ready = ticketsWithoutEmbedding == 0
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(GetEmbeddingsStatistics), 
                    LogEvent.EventsTypes.Error, 
                    ex);
                
                return Problem($"Errore: {ex.Message}");
            }
        }

        private async Task<List<TicketSearchModel>> TicketsSearch(int top, int skip, string txtSearch, int? idCompany, int? idProduct, int? IdArticle)
        {
            try
            {
                List<object> parms = new List<object>();

                var select = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank ";
                var from = $"FROM Tickets AS FT_TBL INNER JOIN FREETEXTTABLE(Tickets, Description, @Search, LANGUAGE N'Italian') AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY]";
                
                var pSearch = new SqlParameter("@Search", txtSearch);
                parms.Add(pSearch);
                
                string where = string.Empty;

                if (idCompany != null || idProduct != null || IdArticle != null) 
                {
                    where = "where ";

                    if (idCompany != null)
                    {
                        where += $" FT_TBL.IdCompany = @IdCompany";
                        parms.Add(new SqlParameter("@IdCompany", idCompany));
                    }
                    if (idProduct != null)
                    {
                        where += " FT_TBL.IdProduct = @IdProduct";
                        parms.Add(new SqlParameter("@IdProduct", idProduct));
                    }
                    if (IdArticle != null)
                    {
                        where += " FT_TBL.IdArticle = @IdArticle";
                        parms.Add(new SqlParameter("@IdArticle", IdArticle));
                    }
                }

                var orderby = $"ORDER BY KEY_TBL.RANK DESC OFFSET {skip} ROWS FETCH NEXT {top} ROWS ONLY";

                var sql = select + from + where + orderby;

                List<TicketSearchModel> tickets = await _context.Tickets.FromSqlRaw(sql, parms.ToArray()).Select(x => new TicketSearchModel()
                {
                    Description = x.Description,
                    Id = x.Id,
                    Rank = x.RANK
                }).ToListAsync();

                return tickets;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(TicketsSearch), LogEvent.EventsTypes.Error, ex);
                return new List<TicketSearchModel>();
            }
        }


        private async Task<string?> CreatePdf(int id, int? idLanguage)
        {
            try
            {
                Ticket ticket = await _context.Tickets.Include(x => x.UserAssigned).Where(x => x.Id == id).FirstOrDefaultAsync();

                Company? company = await _context.GetHeadCompanyAsync();

                if (ticket == null || company == null)
                    return null;

                HtmlToPdf converter = new HtmlToPdf();
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginLeft = 30;
                converter.Options.MarginRight = 30;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;

                converter.Options.DisplayFooter = true;
                converter.Footer.DisplayOnEvenPages = true;
                converter.Footer.DisplayOnOddPages = true;
                converter.Footer.DisplayOnFirstPage = true;

                converter.Options.DisplayHeader = true;
                converter.Header.DisplayOnFirstPage = true;
                converter.Header.DisplayOnEvenPages = true;
                converter.Header.DisplayOnOddPages = true;

                converter.Header.Height = 45;


                converter.Options.RenderingEngine = RenderingEngine.Blink;

               
                string headerUrl = HttpContext.AbsoluteUrl("/Reports/Header",
                        new { id = company.Id });

                PdfHtmlSection headerHtml = new PdfHtmlSection(headerUrl);
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
                headerHtml.MinPageLoadTime = 1;

                converter.Header.Add(headerHtml);
                
                string footerUrl = HttpContext.AbsoluteUrl("/Reports/Footer");

                PdfHtmlSection footerHtml = new PdfHtmlSection(footerUrl);
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
                headerHtml.MinPageLoadTime = 1;

                PdfTextSection text = new PdfTextSection(0, 10,
                        "Page: {page_number} of {total_pages}  ",
                       new System.Drawing.Font("Arial", 8));

                text.HorizontalAlign = PdfTextHorizontalAlign.Right;

                converter.Footer.Add(footerHtml);
                converter.Footer.Add(text);
                converter.Footer.FirstPageNumber = 1;

                var absUrl = HttpContext.AbsoluteUrl("/Reports/TicketReport", new { id = id, idLanguage = idLanguage });

                converter.Options.MinPageLoadTime = 1;
                converter.Options.MaxPageLoadTime = 300;
                PdfDocument doc = converter.ConvertUrl(absUrl);

                // save pdf document 
                var path = _archiveService.GetPath($"{Guid.NewGuid()}.pdf");
                doc.Save(path);

                
                var bytes = System.IO.File.ReadAllBytes(path);

                
                doc.Close();


                var s = Convert.ToBase64String(bytes);
                
                System.IO.File.Delete(path);

                return s;
            }

            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(CreatePdf), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Genera un PDF del ticket con QuestPDF
        /// </summary>
        /// <param name="id">ID del ticket</param>
        /// <returns>File PDF</returns>
        [HttpGet("pdf/{id}")]
        public async Task<IActionResult> DownloadTicketPdf(int id)
        {
            try
            {
                // Recupera il ticket con tutte le relazioni necessarie
                var ticket = await _context.Tickets
                    .Include(x => x.Company)
                    .Include(x => x.TicketType)
                    .Include(x => x.Article)
                        .ThenInclude(x => x!.Product)
                    .Include(x => x.Product)
                    .Include(x => x.Contact)
                    .Include(x => x.UserAssigned)
                    .Include(x => x.UserOpened)
                    .Include(x => x.UserClosed)
                    .Include(x => x.State)
                    .Include(x => x.Project)
                    .Where(x => x.Id == id)
                    .FirstOrDefaultAsync();

                if (ticket == null)
                {
                    return NotFound($"Ticket #{id} non trovato");
                }

                // Verifica permessi
                if (!await _permits.CanGetObject(ticket.IdCompany))
                {
                    await _logEventService.RegisterAsync(
                        nameof(TicketsController), 
                        nameof(DownloadTicketPdf), 
                        LogEvent.EventsTypes.Error, 
                        $"Accesso negato al ticket #{id}");
                    return Forbid();
                }

                // Genera il PDF
                var pdfBytes = _pdfGenerator.GenerateTicketPdf(ticket);

                // Restituisce il file
                var fileName = $"Ticket_{id}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(DownloadTicketPdf), 
                    LogEvent.EventsTypes.Error, 
                    ex);
                
                return Problem($"Errore durante la generazione del PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ NUOVO: Ottiene il carico di lavoro (workload) degli utenti per una data specifica
        /// Restituisce quanti ticket attivi ha ogni utente assegnato in quella giornata
        /// </summary>
        /// <param name="date">Data per cui calcolare il workload (solo la parte data, ignora orario)</param>
        /// <returns>Dizionario: { "userId": { userId, fullName, ticketCount, tickets: [...] } }</returns>
        [HttpGet("user-workload")]
        public async Task<ActionResult<Dictionary<string, object>>> GetUserWorkload([FromQuery] DateTime date)
        {
            try
            {
                var dateOnly = date.Date;
                var dateTomorrow = dateOnly.AddDays(1);
                
                // Query: Ottieni tutti i ticket NON chiusi per quella giornata con utenti assegnati
                var ticketsInDate = await _context.TicketUserAssignments
                    .Include(tua => tua.Ticket)
                    .Include(tua => tua.User)
                    .Where(tua => 
                        tua.Ticket.Date.HasValue && 
                        tua.Ticket.Date.Value >= dateOnly && 
                        tua.Ticket.Date.Value < dateTomorrow &&
                        !tua.Ticket.Closed) // Solo ticket aperti
                    .ToListAsync();

                // Filtra per permessi utente
                if (!await _permits.CanAccessOtherCompany())
                {
                    var idCompany = await _permits.GetIdCompany();
                    ticketsInDate = ticketsInDate
                        .Where(tua => tua.Ticket.IdCompany == idCompany)
                        .ToList();
                }

                // Raggruppa per utente
                var workloadByUser = ticketsInDate
                    .GroupBy(tua => new { tua.IdUser, tua.User.NameComplete })
                    .Select(g => new
                    {
                        UserId = g.Key.IdUser,
                        FullName = g.Key.NameComplete,
                        TicketCount = g.Count(),
                        Tickets = g.Select(tua => new
                        {
                            Id = tua.Ticket.Id,
                            Description = tua.Ticket.Description,
                            Company = tua.Ticket.Company?.RagioneSociale,
                            Time = tua.Ticket.Time,
                            Priority = tua.Ticket.Priority
                        }).ToList()
                    })
                    .ToDictionary(x => x.UserId, x => (object)new
                    {
                        x.UserId,
                        x.FullName,
                        x.TicketCount,
                        x.Tickets
                    });

                return Ok(workloadByUser);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(GetUserWorkload), 
                    LogEvent.EventsTypes.Error, 
                    ex);
                
                return Problem($"Errore durante il calcolo del workload: {ex.Message}");
            }
        }

        // ==========================================
        // GET: api/Tickets/{id}/assigned-users
        // Recupera la lista degli ID utenti assegnati a un ticket
        // ==========================================
        [HttpGet("{id}/assigned-users")]
        public async Task<ActionResult<List<string>>> GetAssignedUsers(int id)
        {
            try
            {
                var userIds = await _ticketsService.GetAssignedUserIdsAsync(id);

                await _logEventService.RegisterAsync(
                    nameof(TicketsController),
                    nameof(GetAssignedUsers),
                    LogEvent.EventsTypes.Info,
                    $"Ticket #{id}: Restituiti {userIds.Count} utenti assegnati: {string.Join(", ", userIds)}");

                return Ok(userIds);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController),
                    nameof(GetAssignedUsers),
                    LogEvent.EventsTypes.Error,
                    $"Errore GetAssignedUsers per ticket #{id}: {ex.Message}");
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        // ==========================================
        // POST: api/Tickets/{id}/assign-users
        // Assegna multipli utenti a un ticket
        // ==========================================
        [HttpPost("{id}/assign-users")]
        public async Task<IActionResult> AssignUsers(int id, [FromBody] AssignUsersRequest request)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserId = currentUser?.Id;

                var result = await _ticketsService.AssignUsersAsync(id, request, currentUserId);

                if (!result.Success)
                {
                    if (result.ErrorMessage?.Contains("non trovato") == true)
                        return NotFound(result.ErrorMessage);
                    return BadRequest(result.ErrorMessage);
                }

                // Notifiche (restano nel controller: dipendono da HttpContext)
                if (result.AddedUserIds.Any() && result.Ticket != null)
                {
                    await SendAssignmentNotifications(result.Ticket, result.AddedUserIds, isAssignment: true);
                }

                if (result.RemovedUserIds.Any() && result.Ticket != null)
                {
                    await SendAssignmentNotifications(result.Ticket, result.RemovedUserIds, isAssignment: false);
                }

                if (currentUser != null && result.Ticket != null)
                {
                    await SendManagerSummaryEmail(result.Ticket, currentUser, result.AddedUserIds, result.RemovedUserIds);
                }

                return Ok(new
                {
                    message = request.UserIds?.Any() == true
                        ? "Utenti assegnati con successo"
                        : "Tutte le assegnazioni rimosse con successo",
                    assignedCount = result.AssignedCount,
                    addedCount = result.AddedUserIds.Count,
                    removedCount = result.RemovedUserIds.Count
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

        /// <summary>
        /// ✅ NUOVO: Endpoint per salvare subscription push browser
        /// </summary>
        [HttpPost("push/subscribe")]
        public async Task<IActionResult> PushSubscribe([FromBody] PushSubscribeRequest request)
        {
            try
            {
                var userId = _userManager.GetUserId(User);

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Utente non autenticato");
                }

                var result = await _pushService.SaveSubscriptionAsync(
                    userId,
                    request.Subscription);

                if (result)
                {
                    return Ok(new { message = "Subscription salvata con successo" });
                }
                else
                {
                    return BadRequest("Impossibile salvare subscription");
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController),
                    nameof(PushSubscribe),
                    LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ NUOVO: Endpoint per rimuovere subscription push browser
        /// </summary>
        [HttpPost("push/unsubscribe")]
        public async Task<IActionResult> PushUnsubscribe()
        {
            try
            {
                var userId = _userManager.GetUserId(User);

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Utente non autenticato");
                }

                var result = await _pushService.RemoveSubscriptionAsync(userId);

                if (result)
                {
                    return Ok(new { message = "Subscription rimossa con successo" });
                }
                else
                {
                    return BadRequest("Impossibile rimuovere subscription");
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController),
                    nameof(PushUnsubscribe),
                    LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }
        /// <summary>
        /// ✅ NUOVO: Endpoint per ottenere VAPID public key (necessario per Chrome)
        /// </summary>
        [HttpGet("push/vapid-public-key")]
        [AllowAnonymous] // Pubblico perché serve al browser PRIMA del login
        public IActionResult GetVapidPublicKey()
        {
            var publicKey = _configuration["PushNotifications:WebPush:publicKey"];

            if (string.IsNullOrEmpty(publicKey))
            {
                return NotFound("VAPID public key non configurata");
            }

            return Ok(new { publicKey });
        }

        /// <summary>
        /// ✅ NUOVO: Invia email riepilogo al manager
        /// </summary>
        private async Task SendManagerSummaryEmail(Ticket ticket, ApplicationUser manager, List<string> addedUsers, List<string> removedUsers)
        {
            try
            {
                if (!addedUsers.Any() && !removedUsers.Any())
                    return;

                var ticketWithDetails = await _context.Tickets
                    .Include(t => t.Company)
                    .FirstOrDefaultAsync(t => t.Id == ticket.Id);

                if (ticketWithDetails == null)
                    return;

                var summary = new System.Text.StringBuilder();
                summary.AppendLine($"Riepilogo assegnazione ticket #{ticket.Id}:");
                summary.AppendLine($"Cliente: {ticketWithDetails.Company?.RagioneSociale}");
                summary.AppendLine();

                if (addedUsers.Any())
                {
                    summary.AppendLine($"✅ Utenti AGGIUNTI ({addedUsers.Count}):");
                    foreach (var userId in addedUsers)
                    {
                        var user = await _context.Users.FindAsync(userId);
                        if (user != null)
                            summary.AppendLine($"  • {user.NameComplete} ({user.Email})");
                    }
                    summary.AppendLine();
                }

                if (removedUsers.Any())
                {
                    summary.AppendLine($"❌ Utenti RIMOSSI ({removedUsers.Count}):");
                    foreach (var userId in removedUsers)
                    {
                        var user = await _context.Users.FindAsync(userId);
                        if (user != null)
                            summary.AppendLine($"  • {user.NameComplete} ({user.Email})");
                    }
                }

                var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{ticket.Id}");

                var keyValues = new Dictionary<string, string>();
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), manager.NameComplete);
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g"));
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticketWithDetails.Company?.RagioneSociale ?? "N/A");

                if (callbackUrl != null)
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                await _emailSenderPlus.SendEmailAsync(
                    new List<string>() { manager.Email },
                    EmailsTypes.NoticeNewTicket,
                    null,
                    keyValues);

                if (!string.IsNullOrEmpty(manager.PhoneNumber))
                {
                    await _TelegramService.SendMessage(manager.PhoneNumber, summary.ToString());
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController),
                    "SendManagerSummaryEmail",
                    LogEvent.EventsTypes.Error,
                    $"Errore invio email riepilogo: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ NUOVO: Invia email, telegram e push agli utenti assegnati/rimossi da un ticket
        /// </summary>
        /// <param name="ticket">Ticket interessato</param>
        /// <param name="userIds">Lista ID utenti</param>
        /// <param name="isAssignment">True = assegnazione, False = rimozione</param>
        private async Task SendAssignmentNotifications(Ticket ticket, List<string> userIds, bool isAssignment = true)
        {
            try
            {
                // Ricarica il ticket con le relazioni necessarie per l'email
                var ticketWithDetails = await _context.Tickets
                    .Include(t => t.Company)
                    .FirstOrDefaultAsync(t => t.Id == ticket.Id);

                if (ticketWithDetails == null)
                {
                    await _logEventService.RegisterAsync(
                        nameof(TicketsController), 
                        nameof(SendAssignmentNotifications), 
                        LogEvent.EventsTypes.Warning, 
                        $"Ticket #{ticket.Id} non trovato per invio notifiche");
                    return;
                }

                // URL del ticket
                var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{ticket.Id}");

                // Usa sempre template di assegnazione
                var emailType = EmailsTypes.NoticeTicketAssigned;

                // Invia email/telegram/push a ogni utente
                foreach (var userId in userIds)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null)
                    {
                        await _logEventService.RegisterAsync(
                            nameof(TicketsController), 
                            nameof(SendAssignmentNotifications), 
                            LogEvent.EventsTypes.Warning, 
                            $"Utente {userId} non trovato per notifica ticket #{ticket.Id}");
                        continue;
                    }

                    // Prepara i parametri per l'email
                    var keyValues = new Dictionary<string, string>();
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), user.NameComplete);
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));
                    
                    if (ticketWithDetails?.Company != null)
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticketWithDetails.Company.RagioneSociale);
                    
                    if (callbackUrl != null)
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                    // ✅ Invia EMAIL
                    try
                    {
                        var msg = await _emailSenderPlus.SendEmailAsync(
                            new List<string>() { user.Email }, 
                            emailType, 
                            null, 
                            keyValues);

                        // ✅ Invia TELEGRAM (se presente numero telefono)
                        if (!string.IsNullOrEmpty(user.PhoneNumber) && msg != null)
                        {
                            var telegramMessage = isAssignment
                                ? $"✅ Assegnato al ticket #{ticket.Id}\n{ticketWithDetails.Company?.RagioneSociale}\n{callbackUrl}"
                                : $"❌ Rimosso dal ticket #{ticket.Id}\n{ticketWithDetails.Company?.RagioneSociale}\n{callbackUrl}";
                            
                            await _TelegramService.SendMessage(user.PhoneNumber, telegramMessage);
                        }

                        // ✅ Invia PUSH NOTIFICATION
                        var pushNotification = new
                        {
                            title = isAssignment 
                                ? $"✅ Assegnato al ticket #{ticket.Id}"
                                : $"❌ Rimosso dal ticket #{ticket.Id}",
                            body = ticketWithDetails?.Company?.RagioneSociale ?? "Ticket CRM",
                            icon = "/favicon.ico",
                            badge = "/favicon.ico",
                            url = $"/Tickets/Info/{ticket.Id}",
                            data = new
                            {
                                ticketId = ticket.Id,
                                action = isAssignment ? "assigned" : "unassigned"
                            }
                        };

                        await _pushService.SendToUsersAsync(new List<string> { userId }, pushNotification);
                    }
                    catch (Exception ex)
                    {
                        await _logEventService.RegisterAsync(
                            nameof(TicketsController), 
                            nameof(SendAssignmentNotifications), 
                            LogEvent.EventsTypes.Warning, 
                            $"Errore invio email {(isAssignment ? "assegnazione" : "rimozione")} a {user.NameComplete}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(SendAssignmentNotifications), 
                    LogEvent.EventsTypes.Error, 
                    $"Errore invio notifiche ticket #{ticket.Id}: {ex.Message}");
            }
        }

    }

    /// <summary>
    /// ✅ NUOVO: Model per richiesta subscription push
    /// </summary>
    public class PushSubscribeRequest
    {
        public string Subscription { get; set; }
    }
}
