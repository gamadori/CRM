using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>Recupera gli allegati del CRM e delega l'OCR al provider attivo.</summary>
    public class ReceiptProcessorService : IReceiptProcessorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        private readonly IReceiptAnalyzer _analyzer;
        private readonly ILogger<ReceiptProcessorService> _logger;

        public ReceiptProcessorService(
            ApplicationDbContext context,
            IArchiveService archiveService,
            IReceiptAnalyzer analyzer,
            ILogger<ReceiptProcessorService> logger)
        {
            _context = context;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Attachments;
            _analyzer = analyzer;
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

                return await _analyzer.AnalyzeAsync(fileBytes, file.Name, useCustomModel, customModelId, kind);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione del file {FileId}", attachmentFileId);
                return Error($"Errore durante l'elaborazione: {ex.Message}");
            }
        }

        public Task<ReceiptExtractionResult> ProcessReceiptFromBytesAsync(
            byte[] fileBytes,
            string fileName,
            bool useCustomModel = false,
            string customModelId = null,
            ReceiptDocumentKind kind = ReceiptDocumentKind.Unknown) =>
            _analyzer.AnalyzeAsync(fileBytes, fileName, useCustomModel, customModelId, kind);

        public Task<bool> HealthCheckAsync() => _analyzer.HealthCheckAsync();

        private static ReceiptExtractionResult Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
