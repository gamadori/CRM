using Anthropic;
using Anthropic.Models.Messages;
using CRM.Server.Data;
using CRM.Server.Services.Usage;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;

namespace CRM.Server.Services
{
    public class TicketSummaryService : ITicketSummaryService
    {
        private const int DefaultMaxMessages = 40;
        private const int MaxAllowedMessages = 120;

        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ITicketsService _ticketsService;
        private readonly ILogger<TicketSummaryService> _logger;
        private readonly IUsageRecorder _usage;
        private readonly AnthropicClient? _client;
        private readonly string _model;

        public TicketSummaryService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ITicketsService ticketsService,
            IConfiguration configuration,
            ILogger<TicketSummaryService> logger,
            IUsageRecorder usage)
        {
            _context = context;
            _permitsService = permitsService;
            _ticketsService = ticketsService;
            _logger = logger;
            _usage = usage;

            var apiKey = configuration["Anthropic:ApiKey"];
            _model = configuration["Anthropic:ChatModel"] ?? "claude-opus-4-8";

            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_ANTHROPIC_API_KEY_HERE")
                _client = new AnthropicClient { ApiKey = apiKey };
        }

        public async Task<TicketSummaryProposalResponse?> ProposeAsync(int idTicket, TicketSummaryProposalRequest request)
        {
            if (!await _permitsService.CanReadTicketChat(idTicket))
                throw new UnauthorizedAccessException("Utente non autorizzato a leggere la chat del ticket.");

            var ticket = await _context.Tickets
                .Include(x => x.Company)
                .Include(x => x.Product)
                .Include(x => x.Article)
                .Include(x => x.TicketType)
                .FirstOrDefaultAsync(x => x.Id == idTicket);

            if (ticket == null)
                return null;

            var maxMessages = request.MaxMessages <= 0
                ? DefaultMaxMessages
                : Math.Clamp(request.MaxMessages, 1, MaxAllowedMessages);

            var messages = await _context.TicketChats
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.IdTicket == idTicket && !x.Deleted)
                .OrderByDescending(x => x.Date)
                .Take(maxMessages)
                .OrderBy(x => x.Date)
                .ToListAsync();

            var fallbackSummary = BuildFallbackSummary(ticket, messages);
            var response = new TicketSummaryProposalResponse
            {
                IdTicket = idTicket,
                Summary = fallbackSummary,
                GeneratedByAi = false,
                GeneratedAt = DateTime.Now,
                SourceMessageCount = messages.Count,
                LastMessageAt = messages.LastOrDefault()?.Date
            };

            if (_client == null)
            {
                response.Warning = "AI non configurata: bozza costruita dagli ultimi messaggi della chat.";
                return response;
            }

            try
            {
                var aiSummary = await GenerateWithAiAsync(ticket, messages);
                if (!string.IsNullOrWhiteSpace(aiSummary))
                {
                    response.Summary = aiSummary.Trim();
                    response.GeneratedByAi = true;
                    response.Warning = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Errore durante la sintesi AI del ticket {IdTicket}", idTicket);
                response.Warning = "Sintesi AI non disponibile: bozza costruita dagli ultimi messaggi della chat.";
            }

            return response;
        }

        public async Task<TicketDTO?> UpdateAsync(int idTicket, UpdateTicketSummaryRequest request)
        {
            if (!await _permitsService.CanEditTicket())
                throw new UnauthorizedAccessException("Utente non autorizzato a modificare il riepilogo del ticket.");

            var ticket = await _context.Tickets.FirstOrDefaultAsync(x => x.Id == idTicket);
            if (ticket == null)
                return null;

            ticket.OperationalSummary = string.IsNullOrWhiteSpace(request.Summary)
                ? null
                : request.Summary.Trim();
            ticket.OperationalSummaryUpdatedAt = DateTime.Now;
            ticket.OperationalSummaryUpdatedBy = await _permitsService.IdUser();

            await _context.SaveChangesAsync();

            return await _ticketsService.GetDetailsAsync(idTicket);
        }

        private async Task<string?> GenerateWithAiAsync(Ticket ticket, IReadOnlyList<TicketChat> messages)
        {
            var stopwatch = Stopwatch.StartNew();

            var response = await _client!.Messages.Create(new MessageCreateParams
            {
                Model = _model,
                MaxTokens = 900,
                System = BuildSystemPrompt(),
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
                Messages = new List<MessageParam>
                {
                    new() { Role = Role.User, Content = BuildUserPrompt(ticket, messages) }
                },
            });

            await _usage.RecordTokensAsync(
                ExternalServiceFeature.TicketSummary, _model, response.TokenUsageOf(),
                true, stopwatch.ElapsedMilliseconds);

            return string.Concat(
                response.Content
                    .Select(x => x.Value)
                    .OfType<TextBlock>()
                    .Select(x => x.Text));
        }

        private static string BuildSystemPrompt()
        {
            return """
                Sei un assistente operativo per un CRM tecnico.
                Devi sintetizzare il ticket integrando descrizione iniziale e chat.
                Non cambiare il significato, non inventare fatti, non inserire soluzioni non dette.
                Scrivi in italiano, in modo breve e utile per un operatore che deve capire lo stato attuale.
                Non usare markdown, grassetti, titoli con asterischi o formattazioni speciali.
                Usa questa struttura:
                Contesto:
                Aggiornamenti dalla chat:
                Stato attuale / prossimi passi:
                Se una sezione non ha informazioni, scrivi "Non indicato".
                """;
        }

        private static string BuildUserPrompt(Ticket ticket, IReadOnlyList<TicketChat> messages)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Ticket #{ticket.Id}");
            sb.AppendLine($"Cliente: {ticket.Company?.RagioneSociale ?? "Non indicato"}");
            sb.AppendLine($"Prodotto: {ticket.Product?.Name ?? "Non indicato"}");
            sb.AppendLine($"Articolo: {ticket.Article?.SerialNumber ?? "Non indicato"}");
            sb.AppendLine();
            sb.AppendLine("Descrizione iniziale:");
            sb.AppendLine(NormalizeText(ticket.Description));
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(ticket.OperationalSummary))
            {
                sb.AppendLine("Riepilogo operativo precedente:");
                sb.AppendLine(NormalizeText(ticket.OperationalSummary));
                sb.AppendLine();
            }

            sb.AppendLine("Messaggi chat in ordine cronologico:");
            if (messages.Count == 0)
            {
                sb.AppendLine("Nessun messaggio chat disponibile.");
            }
            else
            {
                foreach (var message in messages)
                {
                    var user = message.User?.NameComplete ?? "Utente";
                    sb.AppendLine($"[{message.Date:dd/MM/yyyy HH:mm}] {user}: {NormalizeText(message.Message)}");
                }
            }

            return sb.ToString();
        }

        private static string BuildFallbackSummary(Ticket ticket, IReadOnlyList<TicketChat> messages)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Contesto:");
            sb.AppendLine(NormalizeText(ticket.Description));
            sb.AppendLine();
            sb.AppendLine("Aggiornamenti dalla chat:");

            if (messages.Count == 0)
            {
                sb.AppendLine("Non indicato");
            }
            else
            {
                foreach (var message in messages.TakeLast(8))
                {
                    var user = message.User?.NameComplete ?? "Utente";
                    sb.AppendLine($"{message.Date:dd/MM/yyyy HH:mm} - {user}: {NormalizeText(message.Message)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Stato attuale / prossimi passi:");
            sb.AppendLine("Non indicato");

            return sb.ToString().Trim();
        }

        private static string NormalizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Non indicato";

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
