using System;
using System.Collections.Generic;
using System.Linq;
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
            Services.ITicketsService ticketsService) // ✅ AGGIUNTO
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
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicket(int id, Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return BadRequest();
            }
            
            var changeAssigned = await _ticketsService.TicketChangeAssigned(id, ticket.IdUserAssigned);

            if (! await _permits.CanEditTicket())
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PutTicket), LogEvent.EventsTypes.Error, "Attempt to Edit without rights");
                return BadRequest();
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
                    await SendEmailUserAssigned(updatedTicket);
            }

            return NoContent();
        }

       
        // POST: api/Tickets
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Ticket>> PostTicket(Ticket ticket)
        {
            try
            {
                var savedTicket = await _ticketsService.PostAsync(ticket);

                await SendEmailNoticeNewTicket(savedTicket.Id);

                if (savedTicket.IdUserAssigned == null)
                {
                    await SendEmailNewTicketToBeAssigned(savedTicket.Id);
                }
                else
                    await SendEmailUserAssigned(savedTicket);


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
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            try
            {
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

        /// <summary>
        /// Assistente conversazionale: risponde a una domanda dell'utente basandosi
        /// sui ticket chiusi più simili (RAG) e generando la risposta con Claude.
        /// </summary>
        [HttpPost("assistant-chat")]
        public async Task<ActionResult<CRM.Shared.Models.AssistantChatResponse>> AssistantChat(
            [FromBody] CRM.Shared.Models.AssistantChatRequest request,
            [FromServices] OpenAIEmbeddingService embeddingService,
            [FromServices] AnthropicChatService chatService,
            [FromServices] IKnowledgeService knowledgeService)
        {
            try
            {
                if (request?.Messages == null || request.Messages.Count == 0)
                    return BadRequest("La conversazione è vuota");

                var lastUser = request.Messages
                    .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

                if (lastUser == null || string.IsNullOrWhiteSpace(lastUser.Content))
                    return BadRequest("Nessun messaggio utente valido");

                // Recupera i ticket chiusi più simili e la base di conoscenza pertinente
                var retrieval = await RetrieveSimilarClosedTicketsAsync(
                    lastUser.Content, embeddingService, request.TopTickets, request.MinSimilarityThreshold);

                var knowledge = await knowledgeService.SearchSimilarAsync(
                    retrieval.QueryEmbedding, retrieval.ProductIds, top: 4, minSimilarity: 55.0);

                // Costruisci il contesto (ticket + KB) e genera la risposta con Claude
                var context = BuildTicketContext(retrieval.Tickets) + BuildKnowledgeContext(knowledge);
                var reply = await chatService.AnswerAsync(request.Messages, context);

                return new CRM.Shared.Models.AssistantChatResponse
                {
                    Reply = reply,
                    ReferencedTickets = retrieval.Tickets,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(AssistantChat), LogEvent.EventsTypes.Error, ex);
                return Ok(new CRM.Shared.Models.AssistantChatResponse
                {
                    Reply = string.Empty,
                    Success = false,
                    Message = $"Errore durante l'elaborazione: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Versione streaming dell'assistente: invia i frammenti di risposta man mano
        /// che Claude li genera. I ticket di riferimento (forma ridotta) sono restituiti
        /// nell'header 'X-Referenced-Tickets' (JSON codificato in Base64/UTF-8).
        /// </summary>
        [HttpPost("assistant-chat-stream")]
        public async Task AssistantChatStream(
            [FromBody] CRM.Shared.Models.AssistantChatRequest request,
            [FromServices] OpenAIEmbeddingService embeddingService,
            [FromServices] AnthropicChatService chatService,
            [FromServices] IKnowledgeService knowledgeService,
            CancellationToken cancellationToken)
        {
            Response.ContentType = "text/plain; charset=utf-8";

            try
            {
                if (request?.Messages == null || request.Messages.Count == 0)
                {
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                    await Response.WriteAsync("La conversazione è vuota", cancellationToken);
                    return;
                }

                var lastUser = request.Messages
                    .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));

                if (lastUser == null || string.IsNullOrWhiteSpace(lastUser.Content))
                {
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                    await Response.WriteAsync("Nessun messaggio utente valido", cancellationToken);
                    return;
                }

                var retrieval = await RetrieveSimilarClosedTicketsAsync(
                    lastUser.Content, embeddingService, request.TopTickets, request.MinSimilarityThreshold);
                var referenced = retrieval.Tickets;

                var knowledge = await knowledgeService.SearchSimilarAsync(
                    retrieval.QueryEmbedding, retrieval.ProductIds, top: 4, minSimilarity: 55.0);

                // Ticket di riferimento in forma ridotta nell'header (Base64 per gestire accenti)
                var slim = referenced.Select(t => new
                {
                    t.TicketId,
                    t.TicketNumber,
                    t.CustomerName,
                    t.SimilarityPercentage,
                    t.CanAccess
                }).ToList();

                var slimJson = System.Text.Json.JsonSerializer.Serialize(slim);
                Response.Headers["X-Referenced-Tickets"] =
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(slimJson));

                var context = BuildTicketContext(referenced) + BuildKnowledgeContext(knowledge);

                await foreach (var chunk in chatService
                    .AnswerStreamAsync(request.Messages, context, cancellationToken))
                {
                    await Response.WriteAsync(chunk, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnesso: nessuna azione necessaria
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(AssistantChatStream), LogEvent.EventsTypes.Error, ex);
                // Se non è ancora stato scritto nulla, prova a segnalare l'errore in coda allo stream
                try
                {
                    await Response.WriteAsync($"\n\n⚠️ Errore durante l'elaborazione: {ex.Message}", cancellationToken);
                }
                catch { }
            }
        }

        /// <summary>
        /// Recupera i ticket chiusi più simili a una query cercando su TUTTI i ticket (senza filtro
        /// azienda: la soluzione può trovarsi ovunque). Per ogni risultato imposta CanAccess in base
        /// ai permessi dell'utente; i ticket non accessibili vengono restituiti come "casi simili"
        /// anonimi (nessun nome cliente, nessun link).
        /// </summary>
        private async Task<AssistantRetrieval> RetrieveSimilarClosedTicketsAsync(
            string query, OpenAIEmbeddingService embeddingService, int topN, double minSimilarity)
        {
            var scored = new List<(CRM.Shared.Models.TicketSimilarityResult Result, int? IdProduct)>();

            if (string.IsNullOrWhiteSpace(query))
                return new AssistantRetrieval(new(), Array.Empty<float>(), new());

            var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);
            var magnitude = Math.Sqrt(queryEmbedding.Sum(x => (double)x * x));
            if (magnitude < 0.1)
                return new AssistantRetrieval(new(), queryEmbedding, new());

            // Cerca su TUTTI i ticket chiusi (la soluzione può trovarsi in qualsiasi azienda)
            var closedTickets = await _context.Tickets
                .Include(x => x.Company)
                .Where(x => x.Closed == true
                    && !string.IsNullOrEmpty(x.Description)
                    && !string.IsNullOrEmpty(x.DescriptionEmbedding))
                .Select(x => new
                {
                    x.Id,
                    x.IdCompany,
                    x.IdProduct,
                    x.Description,
                    x.CloseDescription,
                    x.DateClosed,
                    EmbeddingJson = x.DescriptionEmbedding,
                    CompanyName = x.Company.RagioneSociale,
                    Priority = x.Priority != null ? x.Priority.ToString() : "Normal"
                })
                .ToListAsync();

            // Insieme delle aziende accessibili all'utente (null = accesso a tutte)
            var canAccessAll = await _permits.CanAccessOtherCompany();
            HashSet<int>? allowedCompanies = canAccessAll
                ? null
                : (await _permits.GetIdCompanies()).ToHashSet();

            foreach (var ticket in closedTickets)
            {
                try
                {
                    var ticketEmbedding = System.Text.Json.JsonSerializer
                        .Deserialize<float[]>(ticket.EmbeddingJson);

                    if (ticketEmbedding == null || ticketEmbedding.Length == 0)
                        continue;

                    var similarity = embeddingService.CalculateCosineSimilarity(queryEmbedding, ticketEmbedding);
                    var percentage = embeddingService.CosineSimilarityToPercentage(similarity);

                    if (percentage >= minSimilarity)
                    {
                        var canAccess = allowedCompanies == null || allowedCompanies.Contains(ticket.IdCompany);

                        scored.Add((new CRM.Shared.Models.TicketSimilarityResult
                        {
                            TicketId = ticket.Id,
                            TicketNumber = $"#{ticket.Id}",
                            Title = ticket.Description != null
                                ? (ticket.Description.Length > 100
                                    ? ticket.Description.Substring(0, 100) + "..."
                                    : ticket.Description)
                                : string.Empty,
                            Description = ticket.Description,
                            // Nome cliente solo se il ticket è accessibile (privacy verso altre aziende)
                            CustomerName = canAccess ? ticket.CompanyName : null,
                            SimilarityPercentage = Math.Round(percentage, 2),
                            CosineSimilarity = Math.Round(similarity, 4),
                            ClosedDate = ticket.DateClosed,
                            Solution = ticket.CloseDescription,
                            Priority = ticket.Priority,
                            CanAccess = canAccess
                        }, ticket.IdProduct));
                    }
                }
                catch
                {
                    // Ignora ticket con embedding non deserializzabile
                    continue;
                }
            }

            var top = scored
                .OrderByDescending(s => s.Result.SimilarityPercentage)
                .Take(Math.Max(1, topN))
                .ToList();

            var productIds = top
                .Where(s => s.IdProduct.HasValue)
                .Select(s => s.IdProduct!.Value)
                .Distinct()
                .ToList();

            return new AssistantRetrieval(top.Select(s => s.Result).ToList(), queryEmbedding, productIds);
        }

        /// <summary>Risultato del recupero per l'assistente: ticket simili, embedding della query e modelli coinvolti.</summary>
        private sealed record AssistantRetrieval(
            List<CRM.Shared.Models.TicketSimilarityResult> Tickets,
            float[] QueryEmbedding,
            List<int> ProductIds);

        /// <summary>
        /// Formatta l'elenco dei ticket simili in un blocco di testo da passare al modello.
        /// </summary>
        private static string BuildTicketContext(List<CRM.Shared.Models.TicketSimilarityResult> tickets)
        {
            if (tickets == null || tickets.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var t in tickets)
            {
                // Cita il numero/cliente solo per i ticket accessibili; gli altri restano casi anonimi
                if (t.CanAccess)
                    sb.AppendLine($"--- Ticket {t.TicketNumber} (cliente: {t.CustomerName}, similarità {t.SimilarityPercentage:F0}%) ---");
                else
                    sb.AppendLine($"--- Caso simile (similarità {t.SimilarityPercentage:F0}%) ---");

                sb.AppendLine($"Problema: {TrimText(t.Description, 600)}");
                sb.AppendLine($"Soluzione applicata: {(string.IsNullOrWhiteSpace(t.Solution) ? "(non registrata)" : TrimText(t.Solution, 600))}");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formatta le voci di conoscenza pertinenti in un blocco di testo per il modello.
        /// </summary>
        private static string BuildKnowledgeContext(List<CRM.Shared.Models.KnowledgeMatch> matches)
        {
            if (matches == null || matches.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== BASE DI CONOSCENZA ===");
            foreach (var k in matches)
            {
                var modello = string.IsNullOrWhiteSpace(k.ProductName) ? "generale" : k.ProductName;
                var categoria = string.IsNullOrWhiteSpace(k.Category) ? string.Empty : $", categoria: {k.Category}";
                sb.AppendLine($"--- KB: {k.Title} (modello: {modello}{categoria}) ---");
                sb.AppendLine(TrimText(k.Content, 900));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string TrimText(string s, int max)
            => string.IsNullOrEmpty(s) ? string.Empty : (s.Length > max ? s.Substring(0, max) + "..." : s);

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
                Company company;

                Ticket ticket = await _context.Tickets.Include(x => x.UserAssigned).Where(x => x.Id == id).FirstOrDefaultAsync();

                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings != null)
                    company = await _context.Companies?.FirstOrDefaultAsync(x=>x.Id == settings.IdHeadQuarter);
                else
                    company = await _context.Companies.Where(x => x.CompanyType == CompanyTypes.HeadCompany).FirstOrDefaultAsync();
                
                    
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

        private async Task SendEmailNoticeNewTicket(int idTicket)
        {
            try
            {
                var ticket = await _context.Tickets.Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == idTicket);

                if (ticket != null)
                {
                    var userOpened = await _context.Users.FindAsync(ticket.IdUserOpened);

                    var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{idTicket}");

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
                            await _TelegramService.SendMessage(userOpened.PhoneNumber, msg.TextBody);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SendEmailNoticeNewTicket), LogEvent.EventsTypes.Error, ex);
            }
        }

        private async Task SendEmailNewTicketToBeAssigned(int idTicket)
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
                var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{idTicket}");

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
                        await _TelegramService.SendMessage(phone, msg.TextBody);
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SendEmailNewTicketToBeAssigned), LogEvent.EventsTypes.Error, ex);
            }
        }

        private async Task<bool> SendEmailUserAssigned(Ticket ticket)
        {
            try
            {
                if (ticket.IdUserAssigned != null)
                {
                    var userAssigned = await _context.Users.FindAsync(ticket.IdUserAssigned);

                    if (userAssigned != null)
                    {
                        var keyValues = new Dictionary<string, string>();
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticket.Company.RagioneSociale);
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), userAssigned.NameComplete);

                        var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{ticket.Id}");

                        if (callbackUrl != null)
                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);
                        var msg = await _emailSenderPlus.SendEmailAsync(new List<string>() { userAssigned.UserName }, EmailsTypes.NoticeTicketAssigned, null, keyValues);

                        if (userAssigned.PhoneNumber != null && userAssigned.PhoneNumber.Length > 0 && msg != null)
                        {
                            await _TelegramService.SendMessage(userAssigned.PhoneNumber, msg.TextBody);
                        }

                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SendEmailUserAssigned), LogEvent.EventsTypes.Error, ex);
                return false;
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
