using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public class ExpenseReceiptService : IExpenseReceiptService
    {
        private readonly ApplicationDbContext _context;

        public ExpenseReceiptService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ExpenseReceiptDTO>> GetByInterventionIdAsync(int interventionId)
        {
            var receipts = await _context.ExpenseReceipts
                .Where(er => er.TicketInterventionId == interventionId)
                .Include(er => er.AttachmentFile)
                .OrderByDescending(er => er.CreatedDate)
                .ToListAsync();

            return receipts.Select(MapToDTO).ToList();
        }

        public async Task<ExpenseReceiptSummaryDTO> GetSummaryByInterventionIdAsync(int interventionId)
        {
            var receipts = await _context.ExpenseReceipts
                .Where(er => er.TicketInterventionId == interventionId)
                .ToListAsync();

            return new ExpenseReceiptSummaryDTO
            {
                TicketInterventionId = interventionId,
                TotalReceiptsCount = receipts.Count,
                ConfirmedReceiptsCount = receipts.Count(r => r.IsConfirmed),
                PendingReceiptsCount = receipts.Count(r => !r.IsConfirmed),
                TotalExpenses = receipts.Where(r => r.TotalAmount.HasValue).Sum(r => r.TotalAmount.Value),
                TotalTaxes = receipts.Where(r => r.TaxAmount.HasValue).Sum(r => r.TaxAmount.Value)
            };
        }

        public async Task<ExpenseReceiptDTO> GetByIdAsync(int id)
        {
            var receipt = await _context.ExpenseReceipts
                .Include(er => er.AttachmentFile)
                .FirstOrDefaultAsync(er => er.Id == id);

            if (receipt == null)
                return null;

            return MapToDTO(receipt);
        }

        public async Task<ExpenseReceiptDTO> CreateAsync(ExpenseReceiptCreateUpdateDTO dto, string userId)
        {
            var receipt = new ExpenseReceipt
            {
                TicketInterventionId = dto.TicketInterventionId,
                Description = dto.Description,
                AttachmentFileId = dto.AttachmentFileId,
                TotalAmount = dto.TotalAmount,
                TaxAmount = dto.TaxAmount,
                TransactionDate = dto.TransactionDate,
                MerchantName = dto.MerchantName,
                Currency = dto.Currency ?? "EUR",
                Notes = dto.Notes,
                IsConfirmed = dto.IsConfirmed,
                ExtractionConfidence = dto.ExtractionConfidence,
                ExtractedFieldsJson = dto.ExtractedFieldsJson,
                CreatedDate = DateTime.UtcNow
            };

            if (dto.IsConfirmed)
            {
                receipt.ConfirmedDate = DateTime.UtcNow;
                receipt.ConfirmedByUserId = userId;
            }

            _context.ExpenseReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(receipt.Id);
        }

        public async Task<ExpenseReceiptDTO> UpdateAsync(int id, ExpenseReceiptCreateUpdateDTO dto, string userId)
        {
            var receipt = await _context.ExpenseReceipts.FindAsync(id);
            if (receipt == null)
                return null;

            receipt.Description = dto.Description;
            receipt.AttachmentFileId = dto.AttachmentFileId;
            receipt.TotalAmount = dto.TotalAmount;
            receipt.TaxAmount = dto.TaxAmount;
            receipt.TransactionDate = dto.TransactionDate;
            receipt.MerchantName = dto.MerchantName;
            receipt.Currency = dto.Currency ?? receipt.Currency ?? "EUR";
            receipt.Notes = dto.Notes;
            receipt.LastModifiedDate = DateTime.UtcNow;

            // Se viene confermato ora
            if (dto.IsConfirmed && !receipt.IsConfirmed)
            {
                receipt.IsConfirmed = true;
                receipt.ConfirmedDate = DateTime.UtcNow;
                receipt.ConfirmedByUserId = userId;
            }

            await _context.SaveChangesAsync();

            return await GetByIdAsync(receipt.Id);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var receipt = await _context.ExpenseReceipts.FindAsync(id);
            if (receipt == null)
                return false;

            _context.ExpenseReceipts.Remove(receipt);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ConfirmAsync(int id, string userId)
        {
            var receipt = await _context.ExpenseReceipts.FindAsync(id);
            if (receipt == null)
                return false;

            receipt.IsConfirmed = true;
            receipt.ConfirmedDate = DateTime.UtcNow;
            receipt.ConfirmedByUserId = userId;
            receipt.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<ExpenseReceiptDTO> CreateFromExtractionAsync(
            int interventionId,
            int attachmentFileId,
            ReceiptExtractionResult extractionResult,
            string userId)
        {
            var receipt = new ExpenseReceipt
            {
                TicketInterventionId = interventionId,
                AttachmentFileId = attachmentFileId,
                TotalAmount = extractionResult.TotalAmount,
                TaxAmount = extractionResult.TaxAmount,
                TransactionDate = extractionResult.TransactionDate,
                MerchantName = extractionResult.MerchantName,
                Currency = extractionResult.Currency ?? "EUR",
                ExtractionConfidence = extractionResult.AverageConfidence,
                ExtractedFieldsJson = System.Text.Json.JsonSerializer.Serialize(extractionResult),
                ProcessedDate = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                IsConfirmed = false,
                Description = $"{extractionResult.MerchantName} - {extractionResult.TransactionDate?.ToString("dd/MM/yyyy")}"
            };

            _context.ExpenseReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(receipt.Id);
        }

        private ExpenseReceiptDTO MapToDTO(ExpenseReceipt receipt)
        {
            return new ExpenseReceiptDTO
            {
                Id = receipt.Id,
                TicketInterventionId = receipt.TicketInterventionId,
                Description = receipt.Description,
                AttachmentFileId = receipt.AttachmentFileId,
                AttachmentFileName = receipt.AttachmentFile?.Name,
                TotalAmount = receipt.TotalAmount,
                TaxAmount = receipt.TaxAmount,
                TransactionDate = receipt.TransactionDate,
                MerchantName = receipt.MerchantName,
                Currency = receipt.Currency,
                ExtractionConfidence = receipt.ExtractionConfidence,
                ProcessedDate = receipt.ProcessedDate,
                IsConfirmed = receipt.IsConfirmed,
                ConfirmedDate = receipt.ConfirmedDate,
                ConfirmedByUserId = receipt.ConfirmedByUserId,
                CreatedDate = receipt.CreatedDate,
                LastModifiedDate = receipt.LastModifiedDate,
                Notes = receipt.Notes
            };
        }
    }
}
