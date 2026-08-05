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
        private readonly IExchangeRateService _exchangeRates;

        public ExpenseReceiptService(ApplicationDbContext context, IExchangeRateService exchangeRates)
        {
            _context = context;
            _exchangeRates = exchangeRates;
        }

        /// <summary>Valuta di casa dell'installazione; EUR se non configurata.</summary>
        private async Task<string> GetBaseCurrencyAsync()
        {
            var configured = await _context.GlobalSettings
                .AsNoTracking()
                .OrderBy(g => g.Id)
                .Select(g => g.BaseCurrency)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(configured) ? "EUR" : configured.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Calcola e CONGELA la conversione in valuta base sulla riga.
        /// <para>
        /// Se il cambio non e' determinabile la spesa resta senza <c>AmountBase</c>: e' voluto.
        /// Inventare un cambio produrrebbe un totale plausibile e sbagliato, che e' il tipo di
        /// errore che nessuno va a cercare. La riga verra' mostrata come "da convertire".
        /// </para>
        /// </summary>
        private async Task ApplyConversionAsync(ExpenseReceipt receipt, decimal? providedRate)
        {
            receipt.ExchangeRate = null;
            receipt.AmountBase = null;

            if (!receipt.TotalAmount.HasValue || string.IsNullOrWhiteSpace(receipt.Currency))
                return;

            var baseCurrency = await GetBaseCurrencyAsync();

            // Un cambio indicato a mano vince sempre su quello di riferimento: per un rimborso
            // spesso vale quello applicato dalla carta, che il servizio BCE non conosce.
            var rate = providedRate
                ?? await _exchangeRates.GetRateAsync(receipt.Currency, baseCurrency, receipt.TransactionDate ?? receipt.CreatedDate);

            if (!rate.HasValue || rate.Value <= 0)
                return;

            receipt.ExchangeRate = rate.Value;
            receipt.AmountBase = Math.Round(receipt.TotalAmount.Value * rate.Value, 2, MidpointRounding.AwayFromZero);
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

            var summary = await BuildOwnerSummaryAsync(receipts);
            summary.TicketInterventionId = interventionId;
            return summary;
        }

        public async Task<List<ExpenseReceiptDTO>> GetByActivityIdAsync(int activityId)
        {
            var receipts = await _context.ExpenseReceipts
                .Where(er => er.IdActivity == activityId)
                .Include(er => er.AttachmentFile)
                .Include(er => er.Activity)
                .Include(er => er.Initiative)
                .Include(er => er.UserSpender).ThenInclude(u => u.Contact)
                .OrderByDescending(er => er.CreatedDate)
                .ToListAsync();

            return receipts.Select(MapToDTO).ToList();
        }

        public async Task<ExpenseReceiptSummaryDTO> GetSummaryByActivityIdAsync(int activityId)
        {
            var receipts = await _context.ExpenseReceipts
                .Where(er => er.IdActivity == activityId)
                .ToListAsync();

            return await BuildOwnerSummaryAsync(receipts);
        }

        /// <summary>
        /// Riepilogo delle spese di un singolo contenitore (intervento o attivita').
        /// <para>
        /// I totali sommano <c>AmountBase</c>, non l'importo originale: sommare importi in valute
        /// diverse produce un numero che non significa niente, e la pagina ci stampava sopra il
        /// simbolo della valuta di casa. Le righe non ancora convertite restano fuori dai totali e
        /// vengono contate a parte, cosi' chi guarda sa che il totale non e' tutto.
        /// </para>
        /// </summary>
        private async Task<ExpenseReceiptSummaryDTO> BuildOwnerSummaryAsync(List<ExpenseReceipt> receipts)
        {
            var converted = receipts.Where(r => r.AmountBase.HasValue).ToList();

            return new ExpenseReceiptSummaryDTO
            {
                TotalReceiptsCount = receipts.Count,
                ConfirmedReceiptsCount = receipts.Count(r => r.IsConfirmed),
                PendingReceiptsCount = receipts.Count(r => !r.IsConfirmed),
                TotalExpenses = converted.Sum(r => r.AmountBase.Value),

                // L'imposta segue lo stesso cambio congelato dell'importo: convertirla con un
                // cambio diverso la scollegherebbe dal totale di cui fa parte.
                TotalTaxes = converted
                    .Where(r => r.TaxAmount.HasValue && r.ExchangeRate.HasValue)
                    .Sum(r => Math.Round(r.TaxAmount.Value * r.ExchangeRate.Value, 2, MidpointRounding.AwayFromZero)),

                NeedsConversionCount = receipts.Count(r => r.TotalAmount.HasValue && !r.AmountBase.HasValue),
                BaseCurrency = await GetBaseCurrencyAsync()
            };
        }

        /// <summary>
        /// Query trasversale: e' quella che rende le note spese guardabili senza scendere dentro
        /// un intervento.
        /// </summary>
        /// <param name="restrictToUserId">
        /// Se valorizzato, si vedono solo le spese di quella persona. Il vincolo si applica qui e
        /// non nel controller: e' l'unico punto da cui passano tutte le letture.
        /// </param>
        private IQueryable<ExpenseReceipt> BuildQuery(ExpenseReceiptFilter filter, string restrictToUserId)
        {
            var query = _context.ExpenseReceipts
                .AsNoTracking()
                .Include(er => er.AttachmentFile)
                .Include(er => er.Activity)
                .Include(er => er.Initiative)
                .Include(er => er.UserSpender).ThenInclude(u => u.Contact)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(restrictToUserId))
                query = query.Where(er => er.IdUserSpender == restrictToUserId);
            else if (!string.IsNullOrWhiteSpace(filter.IdUserSpender))
                query = query.Where(er => er.IdUserSpender == filter.IdUserSpender);

            // Il periodo si valuta sulla data della spesa, con ripiego sulla data di
            // registrazione: alcune righe storiche non hanno la data di transazione.
            if (filter.DateFrom.HasValue)
            {
                var from = filter.DateFrom.Value.Date;
                query = query.Where(er => (er.TransactionDate ?? er.CreatedDate) >= from);
            }

            if (filter.DateTo.HasValue)
            {
                var to = filter.DateTo.Value.Date.AddDays(1);
                query = query.Where(er => (er.TransactionDate ?? er.CreatedDate) < to);
            }

            // Una spesa di fiera ha un contesto anche quando non ha ne' intervento ne' attivita':
            // senza l'iniziativa fra le condizioni di "None", il costo dello stand comparirebbe
            // fra i costi generali proprio mentre il resoconto lo conta come costo della fiera.
            query = filter.Context switch
            {
                ExpenseContextFilter.Intervention => query.Where(er => er.TicketInterventionId != null),
                ExpenseContextFilter.Activity => query.Where(er => er.IdActivity != null),
                ExpenseContextFilter.Initiative => query.Where(er => er.IdInitiative != null),
                ExpenseContextFilter.None => query.Where(er => er.TicketInterventionId == null && er.IdActivity == null && er.IdInitiative == null),
                _ => query
            };

            if (filter.IdInitiative.HasValue)
                query = query.Where(er => er.IdInitiative == filter.IdInitiative.Value);

            if (filter.IsConfirmed.HasValue)
                query = query.Where(er => er.IsConfirmed == filter.IsConfirmed.Value);

            if (filter.NeedsConversion == true)
                query = query.Where(er => er.TotalAmount != null && er.AmountBase == null);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(er =>
                    (er.MerchantName != null && er.MerchantName.Contains(term))
                    || (er.Description != null && er.Description.Contains(term))
                    || (er.Notes != null && er.Notes.Contains(term)));
            }

            return query;
        }

        public async Task<(List<ExpenseReceiptDTO> Items, int TotalCount)> SearchAsync(
            ExpenseReceiptFilter filter, string restrictToUserId)
        {
            var query = BuildQuery(filter, restrictToUserId);

            var total = await query.CountAsync();

            var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;
            var page = filter.PageNumber > 0 ? filter.PageNumber : 1;

            var receipts = await query
                .OrderByDescending(er => er.TransactionDate ?? er.CreatedDate)
                .ThenByDescending(er => er.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (receipts.Select(MapToDTO).ToList(), total);
        }

        public async Task<ExpenseReceiptSummaryDTO> GetSummaryAsync(
            ExpenseReceiptFilter filter, string restrictToUserId)
        {
            // Sul riepilogo si legge tutto l'insieme filtrato, non la pagina: un totale calcolato
            // sulla sola pagina visibile sarebbe una risposta sbagliata alla domanda "quanto".
            var receipts = await BuildQuery(filter, restrictToUserId).ToListAsync();

            var converted = receipts.Where(r => r.AmountBase.HasValue).ToList();

            return new ExpenseReceiptSummaryDTO
            {
                BaseCurrency = await GetBaseCurrencyAsync(),
                TotalReceiptsCount = receipts.Count,
                ConfirmedReceiptsCount = receipts.Count(r => r.IsConfirmed),
                PendingReceiptsCount = receipts.Count(r => !r.IsConfirmed),

                // I totali sommano SOLO il convertito: mescolare valute diverse darebbe un numero
                // senza significato. Le righe escluse sono contate qui sotto, non nascoste.
                TotalExpenses = converted.Sum(r => r.AmountBase.Value),
                TotalTaxes = converted
                    .Where(r => r.TaxAmount.HasValue && r.ExchangeRate.HasValue)
                    .Sum(r => Math.Round(r.TaxAmount.Value * r.ExchangeRate.Value, 2, MidpointRounding.AwayFromZero)),

                NeedsConversionCount = receipts.Count(r => r.TotalAmount.HasValue && !r.AmountBase.HasValue),

                ByCurrency = receipts
                    .Where(r => r.TotalAmount.HasValue)
                    .GroupBy(r => r.Currency ?? "?")
                    .Select(g => new ExpenseCurrencyBreakdownDTO
                    {
                        Currency = g.Key,
                        Count = g.Count(),
                        TotalOriginal = g.Sum(r => r.TotalAmount.Value),
                        TotalBase = g.Where(r => r.AmountBase.HasValue).Sum(r => r.AmountBase.Value)
                    })
                    .OrderByDescending(x => x.TotalBase)
                    .ToList(),

                ByUser = receipts
                    .GroupBy(r => new { r.IdUserSpender, Name = r.UserSpender != null ? r.UserSpender.NameComplete : null })
                    .Select(g => new ExpenseUserBreakdownDTO
                    {
                        IdUser = g.Key.IdUserSpender,
                        UserName = g.Key.Name,
                        Count = g.Count(),
                        TotalBase = g.Where(r => r.AmountBase.HasValue).Sum(r => r.AmountBase.Value)
                    })
                    .OrderByDescending(x => x.TotalBase)
                    .ToList()
            };
        }

        public async Task<ExpenseReceiptDTO> GetByIdAsync(int id)
        {
            var receipt = await _context.ExpenseReceipts
                .Include(er => er.AttachmentFile)
                .Include(er => er.Activity)
                .Include(er => er.Initiative)
                .Include(er => er.UserSpender).ThenInclude(u => u.Contact)
                .Include(er => er.Documents).ThenInclude(d => d.Lines)
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
                IdActivity = dto.IdActivity,
                IdInitiative = dto.IdInitiative,
                // Se non e' indicato chi ha speso, ha speso chi sta registrando: e' il caso
                // normale, e lasciarlo vuoto renderebbe la spesa invisibile nei totali per persona.
                IdUserSpender = string.IsNullOrWhiteSpace(dto.IdUserSpender) ? userId : dto.IdUserSpender,
                Description = dto.Description,
                AttachmentFileId = dto.AttachmentFileId,
                TotalAmount = dto.TotalAmount,
                TaxAmount = dto.TaxAmount,
                TransactionDate = dto.TransactionDate,
                MerchantName = dto.MerchantName,
                Currency = dto.Currency,
                Notes = dto.Notes,
                IsConfirmed = dto.IsConfirmed,
                ExtractionConfidence = dto.ExtractionConfidence,
                ExtractedFieldsJson = dto.ExtractedFieldsJson,
                Documents = (dto.Documents ?? new()).Select(ToEntity).ToList(),
                CreatedDate = DateTime.UtcNow
            };

            if (dto.IsConfirmed)
            {
                receipt.ConfirmedDate = DateTime.UtcNow;
                receipt.ConfirmedByUserId = userId;
            }

            if (receipt.Documents.Any())
                await RecalculateFromDocumentsAsync(receipt);
            else
                await ApplyConversionAsync(receipt, dto.ExchangeRate);

            _context.ExpenseReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            return await GetByIdAsync(receipt.Id);
        }

        public async Task<List<ExpenseReceiptDTO>> CreateBatchAsync(
            ExpenseReceiptCreateUpdateDTO dto,
            string userId)
        {
            var documents = dto.Documents ?? new();
            if (documents.Count <= 1)
                return new List<ExpenseReceiptDTO> { await CreateAsync(dto, userId) };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var created = new List<ExpenseReceiptDTO>();

            foreach (var document in documents.OrderBy(item => item.SortOrder))
            {
                var item = new ExpenseReceiptCreateUpdateDTO
                {
                    TicketInterventionId = dto.TicketInterventionId,
                    IdActivity = dto.IdActivity,
                    IdInitiative = dto.IdInitiative,
                    IdUserSpender = dto.IdUserSpender,
                    Description = $"{document.MerchantName} - {document.TransactionDate?.ToString("dd/MM/yyyy")}",
                    AttachmentFileId = dto.AttachmentFileId,
                    TotalAmount = document.TotalAmount,
                    TaxAmount = document.TaxAmount,
                    TransactionDate = document.TransactionDate,
                    MerchantName = document.MerchantName,
                    Currency = document.Currency,
                    Notes = dto.Notes,
                    IsConfirmed = dto.IsConfirmed,
                    ExtractionConfidence = document.ExtractionConfidence,
                    ExtractedFieldsJson = dto.ExtractedFieldsJson,
                    Documents = new List<ExpenseReceiptDocumentDTO> { document }
                };

                created.Add(await CreateAsync(item, userId));
            }

            await transaction.CommitAsync();
            return created;
        }

        public async Task<ExpenseReceiptDTO> UpdateAsync(int id, ExpenseReceiptCreateUpdateDTO dto, string userId)
        {
            var receipt = await _context.ExpenseReceipts
                .Include(er => er.Documents).ThenInclude(d => d.Lines)
                .FirstOrDefaultAsync(er => er.Id == id);
            if (receipt == null)
                return null;

            receipt.TicketInterventionId = dto.TicketInterventionId;
            receipt.IdActivity = dto.IdActivity;
            receipt.IdInitiative = dto.IdInitiative;

            if (!string.IsNullOrWhiteSpace(dto.IdUserSpender))
                receipt.IdUserSpender = dto.IdUserSpender;

            receipt.Description = dto.Description;
            receipt.AttachmentFileId = dto.AttachmentFileId;
            receipt.TotalAmount = dto.TotalAmount;
            receipt.TaxAmount = dto.TaxAmount;
            receipt.TransactionDate = dto.TransactionDate;
            receipt.MerchantName = dto.MerchantName;
            receipt.Currency = dto.Currency ?? receipt.Currency;
            receipt.Notes = dto.Notes;
            receipt.ExtractedFieldsJson = dto.ExtractedFieldsJson ?? receipt.ExtractedFieldsJson;
            receipt.LastModifiedDate = DateTime.UtcNow;

            SyncDocuments(receipt, dto.Documents);

            // Importo, valuta o data possono essere cambiati: la conversione va rifatta, mai
            // lasciata quella di prima.
            if (receipt.Documents.Any())
                await RecalculateFromDocumentsAsync(receipt);
            else
                await ApplyConversionAsync(receipt, dto.ExchangeRate);

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
                IdUserSpender = userId,
                AttachmentFileId = attachmentFileId,
                TotalAmount = extractionResult.TotalAmount,
                TaxAmount = extractionResult.TaxAmount,
                TransactionDate = extractionResult.TransactionDate,
                MerchantName = extractionResult.MerchantName,
                // Nessun ripiego su EUR: se l'OCR non ha letto la valuta il campo resta vuoto e
                // va scelto, altrimenti uno scontrino estero verrebbe convertito come se fosse
                // in euro, cioe' per niente.
                Currency = extractionResult.Currency,
                ExtractionConfidence = extractionResult.AverageConfidence,
                ExtractedFieldsJson = System.Text.Json.JsonSerializer.Serialize(extractionResult),
                ProcessedDate = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                IsConfirmed = false,
                Description = $"{extractionResult.MerchantName} - {extractionResult.TransactionDate?.ToString("dd/MM/yyyy")}"
            };

            receipt.Documents = ToDocumentDtos(extractionResult).Select(ToEntity).ToList();

            await RecalculateFromDocumentsAsync(receipt);

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
                IdActivity = receipt.IdActivity,
                ActivitySubject = receipt.Activity?.Subject,
                IdInitiative = receipt.IdInitiative,
                InitiativeName = receipt.Initiative?.Name,
                IdUserSpender = receipt.IdUserSpender,
                UserSpenderName = receipt.UserSpender?.NameComplete,
                ExchangeRate = receipt.ExchangeRate,
                AmountBase = receipt.AmountBase,
                Description = receipt.Description,
                AttachmentFileId = receipt.AttachmentFileId,
                AttachmentFileName = receipt.AttachmentFile?.Name,
                TotalAmount = receipt.TotalAmount,
                TaxAmount = receipt.TaxAmount,
                TransactionDate = receipt.TransactionDate,
                MerchantName = receipt.MerchantName,
                Currency = receipt.Currency,
                ExtractionConfidence = receipt.ExtractionConfidence,
                ExtractedFieldsJson = receipt.ExtractedFieldsJson,
                ProcessedDate = receipt.ProcessedDate,
                IsConfirmed = receipt.IsConfirmed,
                ConfirmedDate = receipt.ConfirmedDate,
                ConfirmedByUserId = receipt.ConfirmedByUserId,
                CreatedDate = receipt.CreatedDate,
                LastModifiedDate = receipt.LastModifiedDate,
                Notes = receipt.Notes,
                Documents = receipt.Documents
                    .OrderBy(document => document.SortOrder)
                    .Select(document => new ExpenseReceiptDocumentDTO
                    {
                        Id = document.Id,
                        SortOrder = document.SortOrder,
                        PageFrom = document.PageFrom,
                        PageTo = document.PageTo,
                        DocumentType = document.DocumentType,
                        MerchantName = document.MerchantName,
                        TransactionDate = document.TransactionDate,
                        Currency = document.Currency,
                        SubtotalAmount = document.SubtotalAmount,
                        TaxAmount = document.TaxAmount,
                        TotalAmount = document.TotalAmount,
                        ExchangeRate = document.ExchangeRate,
                        AmountBase = document.AmountBase,
                        TaxAmountBase = document.TaxAmountBase,
                        ExtractionConfidence = document.ExtractionConfidence,
                        Lines = document.Lines
                            .OrderBy(line => line.SortOrder)
                            .Select(line => new ExpenseReceiptDocumentLineDTO
                            {
                                Id = line.Id,
                                SortOrder = line.SortOrder,
                                Description = line.Description,
                                Quantity = line.Quantity,
                                UnitPrice = line.UnitPrice,
                                TotalPrice = line.TotalPrice,
                                ExtractionConfidence = line.ExtractionConfidence
                            }).ToList()
                    }).ToList()
            };
        }

        private static ExpenseReceiptDocument ToEntity(ExpenseReceiptDocumentDTO dto)
        {
            var document = new ExpenseReceiptDocument();
            ApplyDocument(document, dto);
            document.Lines = (dto.Lines ?? new()).Select(ToEntity).ToList();
            return document;
        }

        private static ExpenseReceiptDocumentLine ToEntity(ExpenseReceiptDocumentLineDTO dto) => new()
        {
            SortOrder = dto.SortOrder,
            Description = dto.Description,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            TotalPrice = dto.TotalPrice,
            ExtractionConfidence = dto.ExtractionConfidence
        };

        private void SyncDocuments(ExpenseReceipt receipt, List<ExpenseReceiptDocumentDTO> incoming)
        {
            incoming ??= new();
            var incomingIds = incoming.Where(document => document.Id > 0).Select(document => document.Id).ToHashSet();
            var removedDocuments = receipt.Documents.Where(document => !incomingIds.Contains(document.Id)).ToList();
            _context.ExpenseReceiptDocuments.RemoveRange(removedDocuments);
            foreach (var removedDocument in removedDocuments)
                receipt.Documents.Remove(removedDocument);

            foreach (var documentDto in incoming)
            {
                var document = documentDto.Id > 0
                    ? receipt.Documents.FirstOrDefault(item => item.Id == documentDto.Id)
                    : null;

                if (document == null)
                {
                    document = new ExpenseReceiptDocument();
                    receipt.Documents.Add(document);
                }

                var currencyChanged = document.Id > 0
                    && (!string.Equals(document.Currency, documentDto.Currency, StringComparison.OrdinalIgnoreCase)
                        || document.TransactionDate?.Date != documentDto.TransactionDate?.Date);

                ApplyDocument(document, documentDto);
                if (currencyChanged)
                    document.ExchangeRate = null;
                SyncLines(document, documentDto.Lines ?? new());
            }
        }

        private void SyncLines(ExpenseReceiptDocument document, List<ExpenseReceiptDocumentLineDTO> incoming)
        {
            var incomingIds = incoming.Where(line => line.Id > 0).Select(line => line.Id).ToHashSet();
            var removedLines = document.Lines.Where(line => !incomingIds.Contains(line.Id)).ToList();
            _context.ExpenseReceiptDocumentLines.RemoveRange(removedLines);

            foreach (var lineDto in incoming)
            {
                var line = lineDto.Id > 0
                    ? document.Lines.FirstOrDefault(item => item.Id == lineDto.Id)
                    : null;

                if (line == null)
                {
                    line = new ExpenseReceiptDocumentLine();
                    document.Lines.Add(line);
                }

                line.SortOrder = lineDto.SortOrder;
                line.Description = lineDto.Description;
                line.Quantity = lineDto.Quantity;
                line.UnitPrice = lineDto.UnitPrice;
                line.TotalPrice = lineDto.TotalPrice;
                line.ExtractionConfidence = lineDto.ExtractionConfidence;
            }
        }

        private static void ApplyDocument(ExpenseReceiptDocument document, ExpenseReceiptDocumentDTO dto)
        {
            document.SortOrder = dto.SortOrder;
            document.PageFrom = dto.PageFrom;
            document.PageTo = dto.PageTo;
            document.DocumentType = dto.DocumentType;
            document.MerchantName = dto.MerchantName;
            document.TransactionDate = dto.TransactionDate;
            document.Currency = dto.Currency;
            document.SubtotalAmount = dto.SubtotalAmount;
            document.TaxAmount = dto.TaxAmount;
            document.TotalAmount = dto.TotalAmount;
            document.ExchangeRate = dto.ExchangeRate;
            document.ExtractionConfidence = dto.ExtractionConfidence;
        }

        private async Task RecalculateFromDocumentsAsync(ExpenseReceipt receipt)
        {
            var documents = receipt.Documents.OrderBy(document => document.SortOrder).ToList();
            if (documents.Count == 0)
            {
                await ApplyConversionAsync(receipt, null);
                return;
            }

            var baseCurrency = await GetBaseCurrencyAsync();

            // Con un documento solo, testata e documento sono la stessa spesa vista due volte.
            if (documents.Count == 1)
            {
                await AlignSingleDocumentAsync(receipt, documents[0], baseCurrency);
                return;
            }

            foreach (var document in documents)
                await ApplyDocumentConversionAsync(document, baseCurrency);

            receipt.TransactionDate = documents
                .Where(document => document.TransactionDate.HasValue)
                .Select(document => document.TransactionDate)
                .Min();
            receipt.MerchantName = $"{documents.Count} documenti fiscali";

            var allTotalsConverted = documents.All(document =>
                document.TotalAmount.HasValue && document.AmountBase.HasValue);
            var taxes = documents.Where(document => document.TaxAmount.HasValue).ToList();
            var allTaxesConverted = taxes.All(document => document.TaxAmountBase.HasValue);

            // La testata multi-documento e' sempre espressa nella valuta base. Gli importi
            // originali restano sui documenti e non vengono mai sommati fra valute diverse.
            receipt.Currency = baseCurrency;
            receipt.ExchangeRate = allTotalsConverted ? 1m : null;
            receipt.TotalAmount = allTotalsConverted
                ? documents.Sum(document => document.AmountBase!.Value)
                : null;
            receipt.AmountBase = receipt.TotalAmount;
            receipt.TaxAmount = allTaxesConverted
                ? taxes.Sum(document => document.TaxAmountBase!.Value)
                : null;
        }

        /// <summary>
        /// Allinea l'unico documento alla testata, e non il contrario.
        /// <para>
        /// Con un documento solo i due oggetti descrivono la stessa spesa, e prima vinceva il
        /// documento: i valori letti dall'OCR sovrascrivevano la testata subito dopo il
        /// salvataggio. Risultato, <b>ogni correzione fatta nel modulo veniva buttata via</b> -
        /// la valuta scelta fra i candidati (il "$" confermato come USD tornava vuoto e la spesa
        /// restava "da convertire"), ma allo stesso modo l'importo corretto a mano, la data,
        /// l'esercente. Il modulo sembrava funzionare e non serviva a niente.
        /// </para>
        /// <para>
        /// Vince la testata perche' e' quella che l'operatore ha davanti e corregge. Il documento
        /// conserva il valore letto solo dove la testata non dice niente.
        /// </para>
        /// </summary>
        private async Task AlignSingleDocumentAsync(
            ExpenseReceipt receipt, ExpenseReceiptDocument document, string baseCurrency)
        {
            document.MerchantName = string.IsNullOrWhiteSpace(receipt.MerchantName)
                ? document.MerchantName
                : receipt.MerchantName;
            document.TransactionDate = receipt.TransactionDate ?? document.TransactionDate;
            document.TotalAmount = receipt.TotalAmount ?? document.TotalAmount;
            document.TaxAmount = receipt.TaxAmount ?? document.TaxAmount;
            document.Currency = string.IsNullOrWhiteSpace(receipt.Currency)
                ? document.Currency
                : receipt.Currency;
            document.ExchangeRate = receipt.ExchangeRate ?? document.ExchangeRate;

            await ApplyDocumentConversionAsync(document, baseCurrency);

            // La testata riprende i valori normalizzati dalla conversione (valuta in maiuscolo,
            // cambio effettivo, importo in valuta base): restano una copia sola, coerente.
            receipt.MerchantName = document.MerchantName;
            receipt.TransactionDate = document.TransactionDate;
            receipt.TotalAmount = document.TotalAmount;
            receipt.TaxAmount = document.TaxAmount;
            receipt.Currency = document.Currency;
            receipt.ExchangeRate = document.ExchangeRate;
            receipt.AmountBase = document.AmountBase;
        }

        private async Task ApplyDocumentConversionAsync(ExpenseReceiptDocument document, string baseCurrency)
        {
            document.AmountBase = null;
            document.TaxAmountBase = null;

            if (!document.TotalAmount.HasValue || string.IsNullOrWhiteSpace(document.Currency))
            {
                document.ExchangeRate = null;
                return;
            }

            var currency = document.Currency.Trim().ToUpperInvariant();
            document.Currency = currency;
            var rate = currency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase)
                ? 1m
                : document.ExchangeRate ?? await _exchangeRates.GetRateAsync(
                    currency,
                    baseCurrency,
                    document.TransactionDate ?? DateTime.Today);

            if (!rate.HasValue || rate.Value <= 0)
            {
                document.ExchangeRate = null;
                return;
            }

            document.ExchangeRate = rate.Value;
            document.AmountBase = Math.Round(
                document.TotalAmount.Value * rate.Value,
                2,
                MidpointRounding.AwayFromZero);
            if (document.TaxAmount.HasValue)
            {
                document.TaxAmountBase = Math.Round(
                    document.TaxAmount.Value * rate.Value,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        private static List<ExpenseReceiptDocumentDTO> ToDocumentDtos(ReceiptExtractionResult extraction)
        {
            var documents = extraction.DocumentResults?.Count > 0
                ? extraction.DocumentResults
                : new List<ReceiptExtractionResult> { extraction };

            return documents.Select((document, documentIndex) => new ExpenseReceiptDocumentDTO
            {
                SortOrder = documentIndex,
                PageFrom = document.PageFrom,
                PageTo = document.PageTo,
                DocumentType = document.DocumentType,
                MerchantName = document.MerchantName,
                TransactionDate = document.TransactionDate,
                Currency = document.Currency,
                SubtotalAmount = document.SubtotalAmount,
                TaxAmount = document.TaxAmount,
                TotalAmount = document.TotalAmount,
                ExtractionConfidence = document.AverageConfidence,
                Lines = (document.LineItems ?? new()).Select((line, lineIndex) => new ExpenseReceiptDocumentLineDTO
                {
                    SortOrder = lineIndex,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    TotalPrice = line.TotalPrice,
                    ExtractionConfidence = line.Confidence
                }).ToList()
            }).ToList();
        }
    }
}
