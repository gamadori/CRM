using CRM.Server.Data;
using CRM.Server.Services.ExpenseCategorization;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>Recupera gli allegati del CRM e delega l'OCR al provider attivo.</summary>
    public class ReceiptProcessorService : IReceiptProcessorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        private readonly IReceiptAnalyzer _analyzer;
        private readonly IExpenseCategorizer _categorizer;
        private readonly ILogger<ReceiptProcessorService> _logger;

        public ReceiptProcessorService(
            ApplicationDbContext context,
            IArchiveService archiveService,
            IReceiptAnalyzer analyzer,
            IExpenseCategorizer categorizer,
            ILogger<ReceiptProcessorService> logger)
        {
            _context = context;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Attachments;
            _analyzer = analyzer;
            _categorizer = categorizer;
            _logger = logger;
        }

        public async Task<ReceiptExtractionResult> ProcessReceiptAsync(
            int attachmentFileId,
            bool useCustomModel = false,
            string customModelId = null,
            ReceiptDocumentKind kind = ReceiptDocumentKind.Unknown)
        {
            try
            {
                var file = await _context.AttachmentFiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == attachmentFileId);

                if (file == null)
                    return Error($"File con ID {attachmentFileId} non trovato");

                var fileBytes = _archiveService.GetAttachment(file.Id, file.Name);
                if (fileBytes == null || fileBytes.Length == 0)
                    return Error("File vuoto o non trovato nell'archivio");

                var result = await _analyzer.AnalyzeAsync(fileBytes, file.Name, useCustomModel, customModelId, kind);
                return await ApplyCategoriesAsync(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione del file {FileId}", attachmentFileId);
                return Error($"Errore durante l'elaborazione: {ex.Message}");
            }
        }

        public async Task<ReceiptExtractionResult> ProcessReceiptFromBytesAsync(
            byte[] fileBytes,
            string fileName,
            bool useCustomModel = false,
            string customModelId = null,
            ReceiptDocumentKind kind = ReceiptDocumentKind.Unknown)
        {
            var result = await _analyzer.AnalyzeAsync(fileBytes, fileName, useCustomModel, customModelId, kind);
            return await ApplyCategoriesAsync(result);
        }

        public Task<bool> HealthCheckAsync() => _analyzer.HealthCheckAsync();

        /// <summary>
        /// Aggiunge all'estrazione la tipologia di spesa proposta, una per documento fiscale.
        /// <para>
        /// Il posto giusto e' qui e non nell'analizzatore: la tipologia non e' un dato letto sul
        /// documento, e' una deduzione che si fa sopra a quello che l'OCR ha letto. Tenerla fuori
        /// dall'analizzatore lascia quest'ultimo sostituibile - un altro provider OCR cambia il
        /// modo di leggere, non il modo di classificare.
        /// </para>
        /// <para>
        /// Non fallisce mai: se la classificazione non riesce, l'estrazione torna comunque com'era
        /// e la tipologia resta un campo da compilare.
        /// </para>
        /// </summary>
        private async Task<ReceiptExtractionResult> ApplyCategoriesAsync(ReceiptExtractionResult result)
        {
            if (result == null || !result.Success)
                return result;

            try
            {
                var documents = result.DocumentResults?.Count > 0
                    ? result.DocumentResults
                    : new List<ReceiptExtractionResult> { result };

                var suggestions = await _categorizer.CategorizeAsync(
                    documents.Select(ToCategoryRequest).ToList());

                for (var i = 0; i < documents.Count && i < suggestions.Count; i++)
                    Apply(documents[i], suggestions[i]);

                // Testata di un file multi-documento: prende la tipologia solo se i documenti
                // vanno d'accordo. Il pieno di benzina e la cena stanno nello stesso PDF, e una
                // testata che ne dichiarasse una sola direbbe il falso sull'altra.
                if (result.DocumentResults?.Count > 0)
                {
                    var distinct = documents
                        .Select(document => document.SuggestedCategory)
                        .Distinct()
                        .ToList();

                    if (distinct.Count == 1 && distinct[0].HasValue)
                    {
                        result.SuggestedCategory = documents[0].SuggestedCategory;
                        result.CategorySource = documents[0].CategorySource;
                        result.CategoryConfidence = documents[0].CategoryConfidence;
                        result.CategoryReason = documents[0].CategoryReason;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tipologia di spesa non determinata: l'estrazione resta valida");
            }

            return result;
        }

        private static ExpenseCategoryRequest ToCategoryRequest(ReceiptExtractionResult document) =>
            new(
                document.MerchantName,
                document.DocumentType,
                (document.LineItems ?? new List<ReceiptLineItem>())
                    .Select(line => line.Description)
                    .Where(description => !string.IsNullOrWhiteSpace(description))
                    .ToList(),
                document.Description,
                document.TotalAmount,
                document.Currency);

        private static void Apply(ReceiptExtractionResult document, ExpenseCategorySuggestion suggestion)
        {
            if (!suggestion.HasCategory)
                return;

            document.SuggestedCategory = suggestion.Category;
            document.CategorySource = suggestion.Source;
            document.CategoryConfidence = suggestion.Confidence;
            document.CategoryReason = suggestion.Reason;
        }

        private static ReceiptExtractionResult Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
