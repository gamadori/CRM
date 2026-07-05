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
    /// Modulo Ordini. Ordine generato tipicamente da un preventivo accettato.
    /// Totali ricalcolati in modo autoritativo lato server.
    /// </summary>
    public class OrdersService : IOrdersService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly IOrderPdfGenerator _pdfGenerator;

        public OrdersService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService,
            IOrderPdfGenerator pdfGenerator)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _pdfGenerator = pdfGenerator;
        }

        public async Task<OrderDTO?> GetItemAsync(int id)
        {
            var item = await _context.Orders
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.Quote)
                .Include(x => x.User)
                .Include(x => x.Rows)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            var dto = item.ToDTO();
            if (dto != null)
                dto.Permits = await _permitsService.ObjectPermits(dto.IdCompany, dto.IdUser);
            return dto;
        }

        public async Task<PagingResponse<OrderDTO, decimal>?> GetSummaryAsync(OrderFilter? args)
        {
            try
            {
                var items = FilterItems(args);
                if (items == null)
                    return new();

                int count = items.Count();
                if (args?.Skip != null && args.Top != null)
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);

                var resp = new PagingResponse<OrderDTO, decimal>
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args != null ? args.PageSize : 0 },
                    Total = await FilterItems(args)!.SumAsync(x => x.Total),
                };

                foreach (var o in resp.Items)
                    o.Permits = await _permitsService.ObjectPermits(o.IdCompany, o.IdUser);

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<OrderDTO>?> GetListAsync(OrderFilter? args = null)
        {
            try
            {
                var items = FilterItems(args);
                if (items == null)
                    return new List<OrderDTO>();
                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<OrderDTO>> PostAsync(Order item)
        {
            try
            {
                Recalculate(item);

                int savedId;

                if (item.Id > 0)
                {
                    var existing = await _context.Orders.Include(o => o.Rows).FirstOrDefaultAsync(x => x.Id == item.Id);
                    if (existing == null)
                        return Fail("Ordine non trovato", System.Net.HttpStatusCode.NotFound);

                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = existing.IdUser;
                    if (string.IsNullOrWhiteSpace(item.Number))
                        item.Number = existing.Number ?? await GenerateNumberAsync(item.Date == default ? existing.Date : item.Date);

                    existing.Number = item.Number;
                    existing.Date = item.Date == default ? existing.Date : item.Date;
                    existing.DeliveryDate = item.DeliveryDate;
                    existing.IdCompany = item.IdCompany;
                    existing.IdContact = item.IdContact;
                    existing.IdQuote = item.IdQuote;
                    existing.IdDeal = item.IdDeal;
                    existing.IdUser = NormalizeUser(item.IdUser);
                    existing.State = item.State;
                    existing.Note = item.Note;
                    existing.Subtotal = item.Subtotal;
                    existing.TotalDiscount = item.TotalDiscount;
                    existing.TotalVat = item.TotalVat;
                    existing.Total = item.Total;

                    _context.OrderRows.RemoveRange(existing.Rows);
                    foreach (var r in item.Rows)
                    {
                        r.Id = 0;
                        r.IdOrder = existing.Id;
                        r.Order = null;
                        _context.OrderRows.Add(r);
                    }

                    savedId = existing.Id;
                }
                else
                {
                    if (item.Date == default)
                        item.Date = DateTime.Now;
                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = await _permitsService.IdUser();
                    item.IdUser = NormalizeUser(item.IdUser);
                    if (string.IsNullOrWhiteSpace(item.Number))
                        item.Number = await GenerateNumberAsync(item.Date);

                    _context.Orders.Add(item);
                    savedId = 0;
                }

                await _context.SaveChangesAsync();
                if (savedId == 0) savedId = item.Id;

                return new APIResponseMessage<OrderDTO>
                {
                    State = true,
                    Data = await GetItemAsync(savedId),
                    Message = "Ordine salvato",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio dell'ordine", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<OrderDTO>> CreateFromQuoteAsync(int quoteId)
        {
            try
            {
                var quote = await _context.Quotes
                    .Include(q => q.Rows)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == quoteId);

                if (quote == null)
                    return Fail("Preventivo non trovato", System.Net.HttpStatusCode.NotFound);

                var existing = await _context.Orders.FirstOrDefaultAsync(o => o.IdQuote == quoteId);
                if (existing != null)
                    return Fail($"Esiste gia' un ordine ({existing.Number}) per questo preventivo", System.Net.HttpStatusCode.Conflict);

                var order = new Order
                {
                    Date = DateTime.Now,
                    IdCompany = quote.IdCompany,
                    IdContact = quote.IdContact,
                    IdQuote = quote.Id,
                    IdDeal = quote.IdDeal,
                    IdUser = NormalizeUser(quote.IdUser ?? await _permitsService.IdUser()),
                    State = OrderStates.Confirmed,
                    Note = quote.Note,
                    Rows = quote.Rows.OrderBy(r => r.SortOrder).Select(r => new OrderRow
                    {
                        IdProduct = r.IdProduct,
                        IdArticle = r.IdArticle,
                        Description = r.Description,
                        Quantity = r.Quantity,
                        UnitPrice = r.UnitPrice,
                        DiscountPct = r.DiscountPct,
                        VatRate = r.VatRate,
                        SortOrder = r.SortOrder
                    }).ToList()
                };

                Recalculate(order);
                order.Number = await GenerateNumberAsync(order.Date);

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                return new APIResponseMessage<OrderDTO>
                {
                    State = true,
                    Data = await GetItemAsync(order.Id),
                    Message = "Ordine creato dal preventivo",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(CreateFromQuoteAsync), EventsTypes.Error, ex);
                return Fail("Errore nella creazione dell'ordine", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<OrderDTO>> ChangeStateAsync(int id, OrderStates state)
        {
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == id);
                if (order == null)
                    return Fail("Ordine non trovato", System.Net.HttpStatusCode.NotFound);

                order.State = state;
                await _context.SaveChangesAsync();

                return new APIResponseMessage<OrderDTO>
                {
                    State = true,
                    Data = await GetItemAsync(id),
                    Message = "Stato aggiornato",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(ChangeStateAsync), EventsTypes.Error, ex);
                return Fail("Errore nel cambio stato", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<(byte[] Bytes, string FileName)?> GeneratePdfAsync(int id)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Company)
                    .Include(o => o.Contact)
                    .Include(o => o.Rows)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (order == null)
                    return null;

                Company? provider = null;
                byte[]? logoBytes = null;

                var settings = await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync();
                if (settings != null)
                {
                    provider = await _context.Companies.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == settings.IdHeadQuarter);

                    if (settings.LogoReport != null)
                    {
                        var logo = await _context.Logos.AsNoTracking()
                            .FirstOrDefaultAsync(l => l.Id == settings.LogoReport.Value);
                        logoBytes = DecodeLogo(logo?.InputFile);
                    }
                }

                var bytes = _pdfGenerator.Generate(order, provider, logoBytes);
                var fileName = $"{(string.IsNullOrWhiteSpace(order.Number) ? $"Ordine-{id}" : order.Number)}-cortesia.pdf";
                return (bytes, fileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(GeneratePdfAsync), EventsTypes.Error, ex);
                return null;
            }
        }

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

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Orders.FindAsync(id);
            if (item == null)
                return false;
            try
            {
                _context.Orders.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private static string? NormalizeUser(string? idUser)
            => string.IsNullOrWhiteSpace(idUser) ? null : idUser;

        private static APIResponseMessage<OrderDTO> Fail(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static void Recalculate(Order order)
        {
            order.Rows ??= new List<OrderRow>();

            int position = 0;
            foreach (var r in order.Rows.OrderBy(r => r.SortOrder))
            {
                r.SortOrder = position++;
                var (net, vat, total) = QuoteMath.Line(r.Quantity, r.UnitPrice, r.DiscountPct, r.VatRate);
                r.LineNet = net;
                r.LineVat = vat;
                r.LineTotal = total;
            }

            order.Subtotal = order.Rows.Sum(r => r.LineNet);
            order.TotalDiscount = order.Rows.Sum(r => QuoteMath.DiscountAmount(r.Quantity, r.UnitPrice, r.DiscountPct));
            order.TotalVat = order.Rows.Sum(r => r.LineVat);
            order.Total = order.Subtotal + order.TotalVat;
        }

        private async Task<string> GenerateNumberAsync(DateTime date)
        {
            int year = date.Year;
            string prefix = $"ORD-{year}-";

            var numbers = await _context.Orders
                .Where(o => o.Number != null && o.Number.StartsWith(prefix))
                .Select(o => o.Number!)
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

        private IQueryable<Order>? FilterItems(OrderFilter? args = null)
        {
            try
            {
                var items = _context.Orders
                    .Include(x => x.Company)
                    .Include(x => x.Contact)
                    .Include(x => x.Quote)
                    .Include(x => x.User)
                    .AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                    items = items.OrderBy(args.OrderBy);
                else
                    items = items.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id);

                if (args?.IdCompany != null)
                    items = items.Where(x => x.IdCompany == args.IdCompany);
                if (args?.IdQuote != null)
                    items = items.Where(x => x.IdQuote == args.IdQuote);
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
                _logEventService.RegisterAsync(nameof(OrdersService), nameof(FilterItems), EventsTypes.Error, ex).Wait();
                return null;
            }
        }
    }
}
