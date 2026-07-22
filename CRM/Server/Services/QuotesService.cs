using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Implementazione server del modulo Preventivi/Offerte. Accede direttamente al database
    /// e ricalcola i totali in modo autoritativo (il client non e' fonte di verita').
    /// </summary>
    public class QuotesService : IQuotesService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly IQuotePdfGenerator _pdfGenerator;
        private readonly IOrdersService _ordersService;
        private readonly IEmailSenderPlus _emailSender;
        private readonly IWebHostEnvironment _env;

        public QuotesService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService,
            ILogEventService logEventService,
            IQuotePdfGenerator pdfGenerator,
            IOrdersService ordersService,
            IEmailSenderPlus emailSender,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _pdfGenerator = pdfGenerator;
            _ordersService = ordersService;
            _emailSender = emailSender;
            _env = env;
        }

        public async Task<QuoteDTO?> GetItemAsync(int id)
        {
            var item = await _context.Quotes
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.Deal)
                .Include(x => x.User)
                .Include(x => x.Rows)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            var dto = item.ToDTO();
            if (dto != null)
            {
                var order = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.IdQuote == id)
                    .Select(o => new { o.Id, o.Number })
                    .FirstOrDefaultAsync();

                if (order != null)
                {
                    dto.IdOrder = order.Id;
                    dto.OrderNumber = order.Number ?? string.Empty;
                }

                dto.Permits = await _permitsService.ObjectPermits(dto.IdCompany, dto.IdUser);

                var lastDelivery = await _context.QuoteDeliveries
                    .AsNoTracking()
                    .Where(d => d.IdQuote == id)
                    .OrderByDescending(d => d.Date)
                    .Select(d => new { d.Date, d.To })
                    .FirstOrDefaultAsync();

                if (lastDelivery != null)
                {
                    dto.LastSentAt = lastDelivery.Date;
                    dto.LastSentTo = lastDelivery.To;
                }
            }
            return dto;
        }

        public async Task<PagingResponse<QuoteDTO, decimal>?> GetSummaryAsync(QuoteFilter? args)
        {
            try
            {
                var items = FilterItems(args);
                if (items == null)
                {
                    return new();
                }

                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,
                };

                var resp = new PagingResponse<QuoteDTO, decimal>
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = paginationMetadata,
                    Total = await FilterItems(args)!.SumAsync(x => x.Total),
                };

                foreach (var q in resp.Items)
                {
                    q.Permits = await _permitsService.ObjectPermits(q.IdCompany, q.IdUser);
                }

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<QuoteDTO>?> GetListAsync(QuoteFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);
                if (items == null)
                {
                    return new List<QuoteDTO>();
                }
                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<QuoteDTO>> PostAsync(Quote item)
        {
            try
            {
                await PopulateRowsFromDealAsync(item);
                Recalculate(item);

                int savedId;

                if (item.Id > 0)
                {
                    var existing = await _context.Quotes
                        .Include(q => q.Rows)
                        .FirstOrDefaultAsync(x => x.Id == item.Id);

                    if (existing == null)
                    {
                        return new APIResponseMessage<QuoteDTO>
                        {
                            State = false,
                            Message = "Preventivo non trovato",
                            Code = System.Net.HttpStatusCode.NotFound
                        };
                    }

                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = existing.IdUser;

                    if (string.IsNullOrWhiteSpace(item.Number))
                        item.Number = existing.Number ?? await GenerateNumberAsync(item.Date == default ? existing.Date : item.Date);

                    existing.Number = item.Number;
                    existing.Date = item.Date == default ? existing.Date : item.Date;
                    existing.ValidUntil = item.ValidUntil;
                    existing.IdCompany = item.IdCompany;
                    existing.IdContact = item.IdContact;
                    existing.IdDeal = item.IdDeal;
                    existing.IdUser = item.IdUser;
                    existing.State = item.State;
                    existing.Note = item.Note;
                    existing.TermsConditions = item.TermsConditions;
                    existing.Subtotal = item.Subtotal;
                    existing.TotalDiscount = item.TotalDiscount;
                    existing.TotalVat = item.TotalVat;
                    existing.Total = item.Total;

                    _context.QuoteRows.RemoveRange(existing.Rows);
                    foreach (var r in item.Rows)
                    {
                        r.Id = 0;
                        r.IdQuote = existing.Id;
                        r.Quote = null;
                        _context.QuoteRows.Add(r);
                    }

                    savedId = existing.Id;
                }
                else
                {
                    if (item.Date == default)
                        item.Date = DateTime.Now;

                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = await _permitsService.IdUser();

                    // Non persistere una FK vuota verso AspNetUsers (es. richiesta non autenticata)
                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = null;

                    if (string.IsNullOrWhiteSpace(item.Number))
                        item.Number = await GenerateNumberAsync(item.Date);

                    _context.Quotes.Add(item);
                    savedId = 0; // assegnato dopo SaveChanges
                }

                await _context.SaveChangesAsync();

                if (savedId == 0)
                    savedId = item.Id;

                return new APIResponseMessage<QuoteDTO>
                {
                    State = true,
                    Data = await GetItemAsync(savedId),
                    Message = "Preventivo salvato",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(PostAsync), EventsTypes.Error, ex);
                return new APIResponseMessage<QuoteDTO>
                {
                    State = false,
                    Message = "Errore nel salvataggio del preventivo",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<APIResponseMessage<QuoteDTO>> ChangeStateAsync(int id, QuoteStates state, bool updateDeal)
        {
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var quote = await _context.Quotes.FirstOrDefaultAsync(x => x.Id == id);
                if (quote == null)
                {
                    return new APIResponseMessage<QuoteDTO>
                    {
                        State = false,
                        Message = "Preventivo non trovato",
                        Code = System.Net.HttpStatusCode.NotFound
                    };
                }

                quote.State = state;

                // Integrazione con la trattativa collegata
                if (updateDeal && quote.IdDeal != null)
                {
                    var deal = await _context.Deals.FirstOrDefaultAsync(d => d.Id == quote.IdDeal);
                    if (deal != null)
                    {
                        if (state == QuoteStates.Sent && deal.Phase < DealPhases.OfferSubmitted)
                        {
                            deal.Phase = DealPhases.OfferSubmitted;
                            deal.Amount = quote.Total;
                        }
                        else if (state == QuoteStates.Accepted)
                        {
                            deal.Phase = DealPhases.Obtained;
                            deal.State = DealStates.CloseWon;
                            deal.Probability = 100;
                            deal.Amount = quote.Total;
                            if (deal.DateClosed == default)
                                deal.DateClosed = DateTime.Now;
                        }
                        else if (state == QuoteStates.Rejected)
                        {
                            deal.Phase = DealPhases.Lost;
                            deal.State = DealStates.CloseLost;
                            deal.Probability = 0;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                if (state == QuoteStates.Accepted)
                {
                    var orderResp = await _ordersService.CreateFromQuoteAsync(id);
                    if (!orderResp.State)
                    {
                        await transaction.RollbackAsync();
                        return new APIResponseMessage<QuoteDTO>
                        {
                            State = false,
                            Message = orderResp.Message ?? "Errore nella creazione dell'ordine dal preventivo",
                            Code = orderResp.Code
                        };
                    }
                }

                await transaction.CommitAsync();

                return new APIResponseMessage<QuoteDTO>
                {
                    State = true,
                    Data = await GetItemAsync(id),
                    Message = state == QuoteStates.Accepted
                        ? "Preventivo accettato e ordine generato"
                        : "Stato aggiornato",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(ChangeStateAsync), EventsTypes.Error, ex);
                return new APIResponseMessage<QuoteDTO>
                {
                    State = false,
                    Message = "Errore nel cambio stato",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        public async Task<(byte[] Bytes, string FileName)?> GeneratePdfAsync(int id)
        {
            try
            {
                var quote = await _context.Quotes
                    .Include(q => q.Company)
                    .Include(q => q.Contact)
                    .Include(q => q.Rows)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (quote == null)
                    return null;

                Company? provider = null;
                byte[]? logoBytes = null;

                var settings = await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync();
                provider = await _context.GetHeadCompanyAsync();

                if (settings != null)
                {
                    if (settings.LogoReport != null)
                    {
                        var logo = await _context.Logos.AsNoTracking()
                            .FirstOrDefaultAsync(l => l.Id == settings.LogoReport.Value);
                        logoBytes = DecodeLogo(logo?.InputFile);
                    }
                }

                var bytes = _pdfGenerator.Generate(quote, provider, logoBytes);
                var fileName = $"{(string.IsNullOrWhiteSpace(quote.Number) ? $"Preventivo-{id}" : quote.Number)}.pdf";
                return (bytes, fileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(GeneratePdfAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        /// <summary>
        /// Restituisce il PDF del preventivo: se è già stato inviato usa lo snapshot congelato
        /// su disco (il documento che il cliente ha ricevuto), altrimenti lo genera al volo.
        /// </summary>
        public async Task<(byte[] Bytes, string FileName)?> GetPdfAsync(int id)
        {
            var last = await _context.QuoteDeliveries
                .AsNoTracking()
                .Where(d => d.IdQuote == id)
                .OrderByDescending(d => d.Date)
                .Select(d => new { d.FilePath, d.FileName })
                .FirstOrDefaultAsync();

            if (last != null && !string.IsNullOrWhiteSpace(last.FilePath) && File.Exists(last.FilePath))
                return (await File.ReadAllBytesAsync(last.FilePath), last.FileName);

            return await GeneratePdfAsync(id);
        }

        /// <summary>
        /// Invia il preventivo al cliente via email con il PDF in allegato, congela lo snapshot
        /// spedito, registra l'invio e (se in Bozza) porta lo stato a Inviato.
        /// </summary>
        public async Task<APIResponseMessage<QuoteDTO>> SendAsync(int id, QuoteSendRequest request)
        {
            try
            {
                var quote = await _context.Quotes
                    .Include(q => q.Company)
                    .Include(q => q.Contact)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (quote == null)
                    return QuoteFail("Preventivo non trovato", System.Net.HttpStatusCode.NotFound);

                // Destinatario: esplicito → email contatto → email azienda
                var to = FirstEmail(request?.To)
                         ?? FirstEmail(quote.Contact?.Email)
                         ?? FirstEmail(quote.Company?.Email);

                if (string.IsNullOrWhiteSpace(to))
                    return QuoteFail("Nessun indirizzo email disponibile per il cliente", System.Net.HttpStatusCode.BadRequest);

                // Snapshot congelato: generato una volta e salvato su disco (serve anche
                // all'outbox, che invia in background e legge l'allegato dal percorso).
                var pdf = await GeneratePdfAsync(id);
                if (pdf == null)
                    return QuoteFail("Impossibile generare il PDF del preventivo", System.Net.HttpStatusCode.BadRequest);

                var filePath = await SaveSnapshotAsync(id, pdf.Value.FileName, pdf.Value.Bytes);

                var number = string.IsNullOrWhiteSpace(quote.Number) ? $"#{id}" : quote.Number;
                var subject = $"Preventivo {number}";
                var body = string.IsNullOrWhiteSpace(request?.Message)
                    ? $"In allegato il preventivo {number}."
                    : request!.Message!;

                var cc = string.IsNullOrWhiteSpace(request?.Cc) ? null : request!.Cc!.Trim();

                var context = quote.IdCompany.HasValue
                    ? new EmailContext(ActivityEntityType.Company, quote.IdCompany.Value)
                    : null;

                var enqueued = await _emailSender.SendEmailAsync(
                    new List<string> { to },
                    EmailsTypes.InvioDocumento,
                    new List<string> { filePath },
                    subject,
                    body,
                    keyValues: null,
                    cc: cc,
                    context: context);

                if (!enqueued)
                    return QuoteFail("Invio email non riuscito (verifica la configurazione SMTP)", System.Net.HttpStatusCode.BadGateway);

                _context.QuoteDeliveries.Add(new QuoteDelivery
                {
                    IdQuote = id,
                    Date = DateTime.Now,
                    To = to,
                    Cc = cc,
                    Channel = "Email",
                    FileName = pdf.Value.FileName,
                    FilePath = filePath,
                    IdUser = await _permitsService.IdUser()
                });

                if (quote.State == QuoteStates.Draft)
                    quote.State = QuoteStates.Sent;

                await _context.SaveChangesAsync();

                return new APIResponseMessage<QuoteDTO>
                {
                    State = true,
                    Data = await GetItemAsync(id),
                    Message = $"Preventivo inviato a {to}",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(SendAsync), EventsTypes.Error, ex);
                return QuoteFail("Errore nell'invio del preventivo", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        private async Task<string> SaveSnapshotAsync(int quoteId, string fileName, byte[] bytes)
        {
            var root = string.IsNullOrWhiteSpace(_env.ContentRootPath) ? AppContext.BaseDirectory : _env.ContentRootPath;
            var dir = Path.Combine(root, "Storage", "Quotes", quoteId.ToString());
            Directory.CreateDirectory(dir);

            var safeName = $"{Path.GetFileNameWithoutExtension(fileName)}-{DateTime.Now:yyyyMMddHHmmss}.pdf";
            var path = Path.Combine(dir, safeName);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }

        private static string? FirstEmail(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? null
                : value.Split(';', ',').Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0);

        private static APIResponseMessage<QuoteDTO> QuoteFail(string message, System.Net.HttpStatusCode code)
            => new() { State = false, Message = message, Code = code };

        private static byte[]? DecodeLogo(string? inputFile)
        {
            if (string.IsNullOrWhiteSpace(inputFile))
                return null;
            try
            {
                var base64 = inputFile.Contains(',') ? inputFile.Split(',')[1] : inputFile;
                var bytes = Convert.FromBase64String(base64);
                if (bytes.Length <= 8)
                    return null;

                bool isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                bool isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8;
                return (isPng || isJpeg) ? bytes : null;
            }
            catch
            {
                return null;
            }
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Quotes.FindAsync(id);
            if (item == null)
            {
                return false;
            }
            try
            {
                _context.Quotes.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private static void Recalculate(Quote quote)
        {
            quote.Rows ??= new List<QuoteRow>();

            int order = 0;
            foreach (var r in quote.Rows.OrderBy(r => r.SortOrder))
            {
                r.SortOrder = order++;
                var (net, vat, total) = QuoteMath.Line(r.Quantity, r.UnitPrice, r.DiscountPct, r.VatRate);
                r.LineNet = net;
                r.LineVat = vat;
                r.LineTotal = total;
            }

            quote.Subtotal = quote.Rows.Sum(r => r.LineNet);
            quote.TotalDiscount = quote.Rows.Sum(r => QuoteMath.DiscountAmount(r.Quantity, r.UnitPrice, r.DiscountPct));
            quote.TotalVat = quote.Rows.Sum(r => r.LineVat);
            quote.Total = quote.Subtotal + quote.TotalVat;
        }

        private async Task PopulateRowsFromDealAsync(Quote quote)
        {
            if (quote.Id != 0 || quote.IdDeal == null || quote.Rows.Any())
            {
                return;
            }

            var deal = await _context.Deals
                .Include(x => x.ProductInterests)
                    .ThenInclude(x => x.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == quote.IdDeal.Value);

            if (deal == null || deal.ProductInterests.Count == 0)
            {
                return;
            }

            quote.IdCompany ??= deal.IdCompany;
            quote.IdContact ??= deal.IdContact;

            var defaultVat = await _context.GlobalSettings
                .AsNoTracking()
                .Select(x => x.DefaultVatRate)
                .FirstOrDefaultAsync();

            if (defaultVat <= 0)
            {
                defaultVat = 22m;
            }

            quote.Rows = deal.ProductInterests
                .OrderBy(x => x.SortOrder)
                .Select((row, index) => new QuoteRow
                {
                    IdProduct = row.IdProduct,
                    Description = ProductDescription(row),
                    Quantity = row.Quantity,
                    UnitPrice = row.UnitPrice,
                    DiscountPct = row.DiscountPct,
                    VatRate = defaultVat,
                    SortOrder = index
                })
                .ToList();
        }

        private static string ProductDescription(DealProductInterest row)
        {
            if (row.Product == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(row.Product.Code)
                ? row.Product.Name
                : $"{row.Product.Code} - {row.Product.Name}";
        }

        private async Task<string> GenerateNumberAsync(DateTime date)
        {
            int year = date.Year;
            string prefix = $"OFF-{year}-";

            var numbers = await _context.Quotes
                .Where(q => q.Number != null && q.Number.StartsWith(prefix))
                .Select(q => q.Number!)
                .ToListAsync();

            int max = 0;
            foreach (var n in numbers)
            {
                var suffix = n.Substring(prefix.Length);
                if (int.TryParse(suffix, out var v) && v > max)
                    max = v;
            }

            return prefix + (max + 1).ToString("D4");
        }

        private IQueryable<Quote>? FilterItems(QuoteFilter? args = null)
        {
            try
            {
                var items = _context.Quotes
                    .Include(x => x.Company)
                    .Include(x => x.Contact)
                    .Include(x => x.Deal)
                    .Include(x => x.User)
                    .AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                    items = items.OrderBy(args.OrderBy);
                else
                    items = items.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id);

                if (args?.IdCompany != null)
                    items = items.Where(x => x.IdCompany == args.IdCompany);

                if (args?.IdDeal != null)
                    items = items.Where(x => x.IdDeal == args.IdDeal);

                if (args?.IdUser != null)
                    items = items.Where(x => x.IdUser == args.IdUser);

                if (args?.State != null)
                    items = items.Where(x => x.State == args.State);

                if (!string.IsNullOrWhiteSpace(args?.Search))
                {
                    var search = args.Search.Trim();
                    items = items.Where(x =>
                        (x.Number != null && x.Number.Contains(search)) ||
                        (x.Note != null && x.Note.Contains(search)) ||
                        (x.Company != null && x.Company.RagioneSociale.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(args?.Filter))
                    items = items.Where(args.Filter);

                return items;
            }
            catch (Exception ex)
            {
                _logEventService.RegisterAsync(nameof(QuotesService), nameof(FilterItems), EventsTypes.Error, ex).Wait();
                return null;
            }
        }
    }
}
