using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio per gestire chat completion con OpenAI GPT
    /// </summary>
    public class OpenAIChatService
    {
        private readonly ILogger<OpenAIChatService> _logger;
        private readonly ChatClient _chatClient;
        private readonly string _model;

        public OpenAIChatService(
            IConfiguration configuration,
            ILogger<OpenAIChatService> logger)
        {
            _logger = logger;

            var apiKey = configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
            {
                _logger.LogWarning("OpenAI API Key non configurata");
                throw new InvalidOperationException("OpenAI API Key non configurata");
            }

            _model = configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini";
            var openAIClient = new OpenAIClient(apiKey);
            _chatClient = openAIClient.GetChatClient(_model);

            _logger.LogInformation("OpenAI Chat Service inizializzato con modello: {Model}", _model);
        }

        /// <summary>
        /// Esegue una chat completion semplice
        /// </summary>
        public async Task<string> ChatCompletionAsync(string userMessage, int maxTokens = 150)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userMessage))
                {
                    return string.Empty;
                }

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("Sei un assistente tecnico IT esperto in supporto tecnico e troubleshooting."),
                    new UserChatMessage(userMessage)
                };

                var options = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = maxTokens,
                    Temperature = 0.3f // Bassa temperatura per risposte più deterministiche
                };

                var response = await _chatClient.CompleteChatAsync(messages, options);
                var content = response.Value.Content[0].Text;

                _logger.LogDebug("Chat completion completata: {Tokens} token utilizzati", 
                    response.Value.Usage.TotalTokenCount);

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante chat completion");
                throw;
            }
        }

        /// <summary>
        /// Valida se una query è relativa a problemi tecnici IT
        /// </summary>
        public async Task<ValidationResult> ValidateITQueryAsync(string query)
        {
            try
            {
                var prompt = $@"Analizza questa richiesta e dimmi se è relativa a problemi tecnici IT (computer, software, hardware, rete, stampanti, sistemi, ecc.).

Query utente: '{query}'

Rispondi SOLO in questo formato JSON:
{{
  ""is_valid"": true o false,
  ""reason"": ""breve motivazione (max 100 caratteri)""
}}";

                var response = await ChatCompletionAsync(prompt, maxTokens: 100);

                // Parse JSON response
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(response);
                    var isValid = json.RootElement.GetProperty("is_valid").GetBoolean();
                    var reason = json.RootElement.GetProperty("reason").GetString() ?? "";

                    return new ValidationResult
                    {
                        IsValid = isValid,
                        Reason = reason
                    };
                }
                catch
                {
                    // Fallback: se GPT non risponde in JSON, analizza il testo
                    var isValid = response.Contains("true", StringComparison.OrdinalIgnoreCase) ||
                                  response.Contains("sì", StringComparison.OrdinalIgnoreCase) ||
                                  response.Contains("valida", StringComparison.OrdinalIgnoreCase);

                    return new ValidationResult
                    {
                        IsValid = isValid,
                        Reason = isValid 
                            ? "Query tecnica rilevata" 
                            : "Query non sembra relativa a problemi IT"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante validazione query");
                // In caso di errore, consideriamo la query valida per non bloccare l'utente
                return new ValidationResult
                {
                    IsValid = true,
                    Reason = "Validazione non disponibile, query accettata"
                };
            }
        }

        /// <summary>
        /// Genera un riassunto intelligente di come un ticket simile può aiutare l'utente
        /// </summary>
        public async Task<string> GenerateSolutionSummaryAsync(
            string userProblem, 
            string ticketDescription, 
            string ticketSolution)
        {
            try
            {
                var prompt = $@"L'utente ha questo problema:
'{userProblem}'

È stato trovato un ticket simile:
Descrizione: {ticketDescription}
Soluzione applicata: {ticketSolution}

Scrivi in 2 frasi (max 150 caratteri totali) come questa soluzione può aiutare l'utente. Sii conciso e pratico.";

                var response = await ChatCompletionAsync(prompt, maxTokens: 100);
                return response.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante generazione summary");
                return "Ticket simile trovato che potrebbe contenere una soluzione utile.";
            }
        }

        /// <summary>
        /// Estrae keywords tecniche da una query
        /// </summary>
        public async Task<List<string>> ExtractKeywordsAsync(string query)
        {
            try
            {
                var prompt = $@"Estrai le 3-5 parole chiave tecniche più importanti da questa query IT.
Rispondi SOLO con le parole separate da virgola, senza altro testo.

Query: '{query}'";

                var response = await ChatCompletionAsync(prompt, maxTokens: 50);
                
                var keywords = response
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => k.Length >= 3)
                    .Take(5)
                    .ToList();

                return keywords;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante estrazione keywords");
                // Fallback: split della query originale
                return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 4)
                    .Take(3)
                    .ToList();
            }
        }

        /// <summary>
        /// Analizza i ticket trovati e determina se contengono una soluzione reale al problema
        /// </summary>
        public async Task<AIVerificationResult> VerifySolutionRelevanceAsync(
            string userProblem, 
            List<(int TicketId, string Description, string Solution, double Similarity)> topTickets)
        {
            try
            {
                if (!topTickets.Any())
                {
                    return new AIVerificationResult
                    {
                        HasRelevantSolution = false,
                        BestTicketId = null,
                        Reason = "Nessun ticket da analizzare"
                    };
                }

                // Costruisci prompt con i top ticket
                var ticketsSummary = string.Join("\n\n", topTickets.Select((t, i) =>
                {
                    var descPreview = string.IsNullOrEmpty(t.Description) 
                        ? "N/A" 
                        : t.Description.Substring(0, Math.Min(200, t.Description.Length));
                    var solPreview = string.IsNullOrEmpty(t.Solution) 
                        ? "N/A" 
                        : t.Solution.Substring(0, Math.Min(200, t.Solution.Length));
                    
                    return $"TICKET #{i+1} (ID:{t.TicketId}, Similarità:{t.Similarity:F0}%)\n" +
                           $"Problema: {descPreview}\n" +
                           $"Soluzione: {solPreview}";
                }));

                var prompt = $@"Analizza questi ticket di supporto e dimmi se ALMENO UNO risolve veramente questo problema:

PROBLEMA UTENTE:
'{userProblem}'

TICKET TROVATI:
{ticketsSummary}

ANALISI RICHIESTA:
1. Esiste almeno UN ticket con una soluzione applicabile a questo problema?
2. Se SÌ, quale ticket ha la soluzione MIGLIORE (indica il numero)?
3. Perché questa soluzione è rilevante?

Rispondi in formato JSON:
{{
  ""has_solution"": true/false,
  ""best_ticket_number"": 1-20 oppure null,
  ""reason"": ""spiegazione breve (max 100 caratteri)""
}}";

                var response = await ChatCompletionAsync(prompt, maxTokens: 150);

                // Parse JSON response
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(response);
                    var hasSolution = json.RootElement.GetProperty("has_solution").GetBoolean();
                    
                    int? bestTicketNumber = null;
                    if (json.RootElement.TryGetProperty("best_ticket_number", out var ticketNumElement) 
                        && ticketNumElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        bestTicketNumber = ticketNumElement.GetInt32();
                    }

                    var reason = json.RootElement.GetProperty("reason").GetString() ?? "";

                    // Converti numero ticket in ID reale
                    int? bestTicketId = null;
                    if (bestTicketNumber.HasValue && bestTicketNumber.Value > 0 && bestTicketNumber.Value <= topTickets.Count)
                    {
                        bestTicketId = topTickets[bestTicketNumber.Value - 1].TicketId;
                    }

                    return new AIVerificationResult
                    {
                        HasRelevantSolution = hasSolution,
                        BestTicketId = bestTicketId,
                        Reason = reason
                    };
                }
                catch
                {
                    // Fallback: analisi testuale
                    var hasSolution = response.Contains("true", StringComparison.OrdinalIgnoreCase) ||
                                     response.Contains("sì", StringComparison.OrdinalIgnoreCase);

                    return new AIVerificationResult
                    {
                        HasRelevantSolution = hasSolution,
                        BestTicketId = hasSolution ? topTickets.First().TicketId : null,
                        Reason = hasSolution ? "Soluzione trovata" : "Nessuna soluzione applicabile"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante verifica AI soluzioni");
                return new AIVerificationResult
                {
                    HasRelevantSolution = false,
                    BestTicketId = null,
                    Reason = "Errore durante la verifica"
                };
            }
        }
    }

    /// <summary>
    /// Risultato validazione query
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risultato verifica AI soluzioni
    /// </summary>
    public class AIVerificationResult
    {
        public bool HasRelevantSolution { get; set; }
        public int? BestTicketId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
