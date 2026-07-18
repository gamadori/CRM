using CRM.Server.Data;
using CRM.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Risultato completo del recupero conoscenza per l'assistente: ticket chiusi simili,
    /// voci di knowledge base pertinenti e il blocco di contesto già formattato per il modello.
    /// </summary>
    public sealed record TicketKnowledgeResult(
        List<TicketSimilarityResult> Tickets,
        List<KnowledgeMatch> Knowledge,
        string ContextText);

    /// <summary>
    /// Recupero della "conoscenza" per l'assistente AI: ticket chiusi simili (ricerca
    /// semantica su embeddings) + knowledge base per modello di macchina. Estratto da
    /// TicketsController per essere riusabile sia dagli endpoint sia dai tool
    /// dell'assistente unificato. I permessi utente sono applicati qui (CanAccess sui
    /// ticket di altre aziende).
    /// </summary>
    public class TicketKnowledgeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permits;
        private readonly OpenAIEmbeddingService _embeddings;
        private readonly IKnowledgeService _knowledge;

        public TicketKnowledgeService(
            ApplicationDbContext context,
            IPermitsService permits,
            OpenAIEmbeddingService embeddings,
            IKnowledgeService knowledge)
        {
            _context = context;
            _permits = permits;
            _embeddings = embeddings;
            _knowledge = knowledge;
        }

        /// <summary>
        /// Recupera ticket chiusi simili e knowledge base pertinente per una domanda.
        /// </summary>
        /// <param name="query">La domanda/problema descritto dall'operatore.</param>
        /// <param name="conversationText">Testo dell'intera conversazione utente: usato per
        /// rilevare i modelli di macchina citati (anche in turni precedenti).</param>
        /// <param name="idTicket">Ticket di contesto opzionale: il suo modello riceve priorità.</param>
        /// <param name="idProduct">Prodotto di contesto opzionale: riceve priorità.</param>
        /// <param name="topTickets">Numero massimo di ticket simili.</param>
        /// <param name="minSimilarity">Soglia minima di similarità (0-100) per i ticket.</param>
        public async Task<TicketKnowledgeResult> RetrieveAsync(
            string query,
            string? conversationText = null,
            int? idTicket = null,
            int? idProduct = null,
            int topTickets = 5,
            double minSimilarity = 55.0)
        {
            var retrieval = await RetrieveSimilarClosedTicketsAsync(query, topTickets, minSimilarity);

            var relevantProductIds = await BuildRelevantProductIdsAsync(
                retrieval.ProductIds, conversationText ?? query, idTicket, idProduct);

            // Soglia KB più permissiva dei ticket: con text-embedding-3-small una domanda
            // specifica in linguaggio naturale contro un blocco di manuale supera raramente il 55%.
            // Il cap top:4 + l'ordinamento per punteggio evitano di introdurre rumore.
            var knowledge = retrieval.QueryEmbedding.Length > 0
                ? await _knowledge.SearchSimilarAsync(retrieval.QueryEmbedding, relevantProductIds, top: 4, minSimilarity: 45.0)
                : new List<KnowledgeMatch>();

            var context = BuildTicketContext(retrieval.Tickets) + BuildKnowledgeContext(knowledge);

            return new TicketKnowledgeResult(retrieval.Tickets, knowledge, context);
        }

        /// <summary>
        /// Recupera i ticket chiusi più simili a una query cercando su TUTTI i ticket (senza filtro
        /// azienda: la soluzione può trovarsi ovunque). Per ogni risultato imposta CanAccess in base
        /// ai permessi dell'utente; i ticket non accessibili vengono restituiti come "casi simili"
        /// anonimi (nessun nome cliente, nessun link).
        /// </summary>
        private async Task<InternalRetrieval> RetrieveSimilarClosedTicketsAsync(
            string query, int topN, double minSimilarity)
        {
            var scored = new List<(TicketSimilarityResult Result, int? IdProduct)>();

            if (string.IsNullOrWhiteSpace(query))
                return new InternalRetrieval(new(), Array.Empty<float>(), new());

            var queryEmbedding = await _embeddings.GenerateEmbeddingAsync(query);
            var magnitude = Math.Sqrt(queryEmbedding.Sum(x => (double)x * x));
            if (magnitude < 0.1)
                return new InternalRetrieval(new(), queryEmbedding, new());

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

                    var similarity = _embeddings.CalculateCosineSimilarity(queryEmbedding, ticketEmbedding);
                    var percentage = _embeddings.CosineSimilarityToPercentage(similarity);

                    if (percentage >= minSimilarity)
                    {
                        var canAccess = allowedCompanies == null || allowedCompanies.Contains(ticket.IdCompany);

                        scored.Add((new TicketSimilarityResult
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

            // Ordina per similarità e, a parità, per data di chiusura più recente (una soluzione
            // recente è più probabilmente ancora valida). Poi seleziona i primi N scartando i
            // doppioni: ticket diversi ma con la STESSA soluzione registrata non aggiungono
            // informazione, riempirebbero solo il contesto a scapito di casi realmente diversi.
            var seenSolutions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var top = new List<(TicketSimilarityResult Result, int? IdProduct)>();
            foreach (var s in scored
                .OrderByDescending(s => s.Result.SimilarityPercentage)
                .ThenByDescending(s => s.Result.ClosedDate ?? DateTime.MinValue))
            {
                // Deduplica solo le soluzioni non vuote: teniamo il primo occorso (più simile/recente).
                var solutionKey = (s.Result.Solution ?? string.Empty).Trim();
                if (solutionKey.Length > 0 && !seenSolutions.Add(solutionKey))
                    continue;

                top.Add(s);
                if (top.Count >= Math.Max(1, topN))
                    break;
            }

            var productIds = top
                .Where(s => s.IdProduct.HasValue)
                .Select(s => s.IdProduct!.Value)
                .Distinct()
                .ToList();

            return new InternalRetrieval(top.Select(s => s.Result).ToList(), queryEmbedding, productIds);
        }

        private sealed record InternalRetrieval(
            List<TicketSimilarityResult> Tickets,
            float[] QueryEmbedding,
            List<int> ProductIds);

        /// <summary>
        /// Costruisce l'insieme dei modelli "rilevanti" per il recupero della conoscenza, unendo:
        /// i modelli dei ticket chiusi simili, quelli citati esplicitamente nel testo della domanda
        /// (per nome o codice) e quello del contesto (ticket/prodotto di partenza della chat).
        /// La conoscenza di questi modelli riceve il bonus di similarità in <see cref="KnowledgeService"/>.
        /// </summary>
        private async Task<List<int>> BuildRelevantProductIdsAsync(
            IEnumerable<int> fromSimilarTickets, string query, int? idTicket, int? idProduct)
        {
            var ids = new HashSet<int>(fromSimilarTickets ?? Enumerable.Empty<int>());

            // Modello citato nella conversazione (es. "TABMACHINE 80100", ma anche solo "tabmachine"):
            // match su nome completo, codice, oppure token distintivo del nome (>=5 char, a parola intera).
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = " " + query.ToLowerInvariant() + " ";
                var products = await _context.Products
                    .Where(p => !p.IsArchived)
                    .Select(p => new { p.Id, p.Name, p.Code })
                    .ToListAsync();

                foreach (var p in products)
                {
                    var name = (p.Name ?? string.Empty).Trim().ToLowerInvariant();
                    var code = (p.Code ?? string.Empty).Trim().ToLowerInvariant();

                    var matched =
                        (name.Length >= 3 && q.Contains(name)) ||
                        (code.Length >= 3 && q.Contains(code));

                    // Token distintivi del nome (es. "tabmachine", "80100") come parola intera
                    if (!matched && name.Length > 0)
                    {
                        foreach (var tok in Regex.Split(name, @"[^a-z0-9]+"))
                        {
                            if (tok.Length >= 5 && Regex.IsMatch(q, $@"\b{Regex.Escape(tok)}\b"))
                            {
                                matched = true;
                                break;
                            }
                        }
                    }

                    if (matched)
                        ids.Add(p.Id);
                }
            }

            // Contesto esplicito: prodotto passato direttamente o dedotto dal ticket di partenza
            if (idProduct.HasValue)
                ids.Add(idProduct.Value);

            if (idTicket.HasValue)
            {
                var pid = await _context.Tickets
                    .Where(t => t.Id == idTicket.Value)
                    .Select(t => t.IdProduct)
                    .FirstOrDefaultAsync();
                if (pid.HasValue)
                    ids.Add(pid.Value);
            }

            return ids.ToList();
        }

        /// <summary>
        /// Formatta l'elenco dei ticket simili in un blocco di testo da passare al modello.
        /// </summary>
        private static string BuildTicketContext(List<TicketSimilarityResult> tickets)
        {
            if (tickets == null || tickets.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
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
        private static string BuildKnowledgeContext(List<KnowledgeMatch> matches)
        {
            if (matches == null || matches.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== BASE DI CONOSCENZA ===");
            foreach (var k in matches)
            {
                var modello = string.IsNullOrWhiteSpace(k.ProductName) ? "generale" : k.ProductName;
                var categoria = string.IsNullOrWhiteSpace(k.Category) ? string.Empty : $", categoria: {k.Category}";
                sb.AppendLine($"--- KB: {k.Title} (modello: {modello}{categoria}) ---");
                // Le chunk KB sono già limitate a MaxChunkLength (1800) in fase di import: passiamo
                // il blocco intero, altrimenti dati come le tabelle tecniche vengono troncati a metà
                // (es. "Capacità magazzino lamelle 3000" cadeva oltre i 900 caratteri).
                sb.AppendLine(TrimText(k.Content, 1800));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string TrimText(string s, int max)
            => string.IsNullOrEmpty(s) ? string.Empty : (s.Length > max ? s.Substring(0, max) + "..." : s);
    }
}
