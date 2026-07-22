using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Modulo Fatture (parte indipendente dal provider). Genera l'XML FatturaPA e delega
    /// la trasmissione a SdI a un IEInvoiceProvider. Numerazione fiscale progressiva per anno.
    /// </summary>
    public class InvoicesService : IInvoicesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly IEInvoiceProvider _provider;

        public InvoicesService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService,
            IEInvoiceProvider provider)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _provider = provider;
        }

        public async Task<InvoiceDTO?> GetItemAsync(int id)
        {
            var item = await _context.Invoices
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.Order)
                .Include(x => x.User)
                .Include(x => x.Rows)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            // Perimetro aziende: una fattura di un'altra azienda non esiste, per questo utente.
            if (item != null && !await CanAccessAsync(item.IdCompany))
                return null;

            var dto = item.ToDTO();
            if (dto != null)
                dto.Permits = await _permitsService.ObjectPermits(dto.IdCompany, dto.IdUser);
            return dto;
        }

        public async Task<PagingResponse<InvoiceDTO, decimal>?> GetSummaryAsync(InvoiceFilter? args)
        {
            try
            {
                var items = await FilterItems(args);
                if (items == null)
                    return new();

                int count = items.Count();
                if (args?.Skip != null && args.Top != null)
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);

                var resp = new PagingResponse<InvoiceDTO, decimal>
                {
                    Items = await items.Select(item => item.ToDTO()).ToListAsync(),
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args != null ? args.PageSize : 0 },
                    Total = await (await FilterItems(args))!.SumAsync(x => x.Total),
                };

                foreach (var o in resp.Items)
                    o.Permits = await _permitsService.ObjectPermits(o.IdCompany, o.IdUser);

                return resp;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<InvoiceDTO>?> GetListAsync(InvoiceFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);
                if (items == null)
                    return new List<InvoiceDTO>();
                return await items.Select(item => item.ToDTO()).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<InvoiceDTO>> PostAsync(Invoice item)
        {
            try
            {
                // Perimetro aziende: non si scrive per un'azienda che non si può vedere.
                if (!await CanAccessAsync(item.IdCompany))
                    return Fail("Azienda non accessibile per questo utente", System.Net.HttpStatusCode.Forbidden);

                Recalculate(item);

                int savedId;
                if (item.Id > 0)
                {
                    var existing = await _context.Invoices.Include(i => i.Rows).FirstOrDefaultAsync(x => x.Id == item.Id);
                    // Non si modifica una fattura altrui spacciandola per propria.
                    if (existing != null && !await CanAccessAsync(existing.IdCompany))
                        existing = null;

                    if (existing == null)
                        return Fail("Fattura non trovata", System.Net.HttpStatusCode.NotFound);

                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = existing.IdUser;
                    if (string.IsNullOrWhiteSpace(item.Number))
                        item.Number = existing.Number;

                    existing.Number = item.Number;
                    existing.Date = item.Date == default ? existing.Date : item.Date;
                    existing.IdCompany = item.IdCompany;
                    existing.IdContact = item.IdContact;
                    existing.IdOrder = item.IdOrder;
                    existing.IdUser = NormalizeUser(item.IdUser);
                    existing.TipoDocumento = item.TipoDocumento;
                    existing.CodiceDestinatario = item.CodiceDestinatario;
                    existing.PecDestinatario = item.PecDestinatario;
                    existing.Causale = item.Causale;
                    existing.Note = item.Note;
                    existing.State = item.State;
                    existing.Subtotal = item.Subtotal;
                    existing.TotalVat = item.TotalVat;
                    existing.Total = item.Total;

                    _context.InvoiceRows.RemoveRange(existing.Rows);
                    foreach (var r in item.Rows)
                    {
                        r.Id = 0;
                        r.IdInvoice = existing.Id;
                        r.Invoice = null;
                        _context.InvoiceRows.Add(r);
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

                    // Se non indicato manualmente, eredita il codice destinatario SdI dal cliente
                    if (string.IsNullOrWhiteSpace(item.CodiceDestinatario))
                        item.CodiceDestinatario = await GetCompanySdiAsync(item.IdCompany);

                    _context.Invoices.Add(item);
                    savedId = 0;
                }

                await _context.SaveChangesAsync();
                if (savedId == 0) savedId = item.Id;

                return Ok(await GetItemAsync(savedId), "Fattura salvata");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio della fattura", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<InvoiceDTO>> CreateFromOrderAsync(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Rows)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || !await CanAccessAsync(order.IdCompany))
                    return Fail("Ordine non trovato", System.Net.HttpStatusCode.NotFound);

                var invoice = new Invoice
                {
                    Date = DateTime.Now,
                    IdCompany = order.IdCompany,
                    IdContact = order.IdContact,
                    IdOrder = order.Id,
                    IdUser = NormalizeUser(order.IdUser ?? await _permitsService.IdUser()),
                    TipoDocumento = "TD01",
                    State = InvoiceStates.Issued,
                    Note = order.Note,
                    Rows = order.Rows.OrderBy(r => r.SortOrder).Select(r => new InvoiceRow
                    {
                        IdProduct = r.IdProduct,
                        Description = r.Description,
                        Quantity = r.Quantity,
                        UnitPrice = r.UnitPrice,
                        DiscountPct = r.DiscountPct,
                        VatRate = r.VatRate,
                        SortOrder = r.SortOrder
                    }).ToList()
                };

                // Precompila il codice destinatario SdI dall'anagrafica del cliente
                invoice.CodiceDestinatario = await GetCompanySdiAsync(order.IdCompany);

                Recalculate(invoice);
                invoice.Number = await GenerateNumberAsync(invoice.Date);

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                return Ok(await GetItemAsync(invoice.Id), "Fattura creata dall'ordine");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(CreateFromOrderAsync), EventsTypes.Error, ex);
                return Fail("Errore nella creazione della fattura", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<InvoiceDTO>> UpdateRecipientAsync(int id, string? codiceDestinatario)
        {
            try
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == id);
                if (invoice == null || !await CanAccessAsync(invoice.IdCompany))
                    return Fail("Fattura non trovata", System.Net.HttpStatusCode.NotFound);

                // Dopo la trasmissione a SdI il dato è congelato: non si tocca
                if (invoice.State == InvoiceStates.Sent || invoice.State == InvoiceStates.Delivered)
                    return Fail("La fattura è già stata trasmessa: codice destinatario non modificabile", System.Net.HttpStatusCode.Conflict);

                var codice = string.IsNullOrWhiteSpace(codiceDestinatario) ? "0000000" : codiceDestinatario.Trim();
                invoice.CodiceDestinatario = codice;

                await _context.SaveChangesAsync();

                return Ok(await GetItemAsync(id), "Codice destinatario aggiornato");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(UpdateRecipientAsync), EventsTypes.Error, ex);
                return Fail("Errore nell'aggiornamento del codice destinatario", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<(string Xml, string FileName)?> GenerateXmlAsync(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Company)
                    .Include(i => i.Rows)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null || !await CanAccessAsync(invoice.IdCompany))
                    return null;

                var settings = await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync();
                var issuer = await _context.GetHeadCompanyAsync();

                if (issuer == null)
                    return null;

                var xml = FatturaPaXmlBuilder.Build(invoice, issuer, settings?.RegimeFiscale);

                // Nome file secondo la convenzione SdI: IT{PIVA}_{progressivo}.xml
                var piva = new string((issuer.PIva ?? "").Where(char.IsDigit).ToArray());
                var progressivo = (invoice.Number ?? invoice.Id.ToString()).Replace("/", "-");
                var fileName = $"IT{piva}_{progressivo}.xml";

                return (xml, fileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(GenerateXmlAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<InvoiceDTO>> SendAsync(int id)
        {
            try
            {
                var generated = await GenerateXmlAsync(id);
                if (generated == null)
                    return Fail("Impossibile generare l'XML della fattura", System.Net.HttpStatusCode.BadRequest);

                var result = await _provider.SendAsync(generated.Value.Xml);
                if (!result.Success)
                    return Fail(result.Message ?? "Trasmissione non riuscita", System.Net.HttpStatusCode.BadGateway);

                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
                if (invoice != null)
                {
                    invoice.State = InvoiceStates.Sent;
                    invoice.SdiReference = result.ProviderReference;
                    await _context.SaveChangesAsync();
                }

                return Ok(await GetItemAsync(id), "Fattura trasmessa a SdI");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(SendAsync), EventsTypes.Error, ex);
                return Fail("Errore nella trasmissione della fattura", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Invoices.FindAsync(id);
            if (item == null || !await CanAccessAsync(item.IdCompany))
                return false;
            try
            {
                _context.Invoices.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private static string? NormalizeUser(string? idUser) => string.IsNullOrWhiteSpace(idUser) ? null : idUser;

        /// <summary>
        /// Recupera il codice destinatario SdI (campo CodiceSDI) dall'anagrafica del cliente.
        /// Restituisce null se non impostato, così il builder FatturaPA applica il default "0000000".
        /// </summary>
        private async Task<string?> GetCompanySdiAsync(int? idCompany)
        {
            if (idCompany == null)
                return null;

            var sdi = await _context.Companies
                .Where(c => c.Id == idCompany.Value)
                .Select(c => c.CodiceSDI)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(sdi) ? null : sdi.Trim();
        }

        private static APIResponseMessage<InvoiceDTO> Fail(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static APIResponseMessage<InvoiceDTO> Ok(InvoiceDTO? data, string msg)
            => new() { State = true, Data = data, Message = msg, Code = System.Net.HttpStatusCode.OK };

        private static void Recalculate(Invoice invoice)
        {
            invoice.Rows ??= new List<InvoiceRow>();

            int position = 0;
            foreach (var r in invoice.Rows.OrderBy(r => r.SortOrder))
            {
                r.SortOrder = position++;
                var (net, vat, total) = QuoteMath.Line(r.Quantity, r.UnitPrice, r.DiscountPct, r.VatRate);
                r.LineNet = net;
                r.LineVat = vat;
                r.LineTotal = total;
            }

            invoice.Subtotal = invoice.Rows.Sum(r => r.LineNet);
            invoice.TotalVat = invoice.Rows.Sum(r => r.LineVat);
            invoice.Total = invoice.Subtotal + invoice.TotalVat;
        }

        private async Task<string> GenerateNumberAsync(DateTime date)
        {
            int year = date.Year;
            string prefix = $"FT-{year}-";

            var numbers = await _context.Invoices
                .Where(i => i.Number != null && i.Number.StartsWith(prefix))
                .Select(i => i.Number!)
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

        /// <summary>
        /// True se l'utente corrente può vedere i dati dell'azienda indicata. Un documento senza
        /// azienda è visibile solo a chi vede tutto (azienda madre): fail-closed.
        /// </summary>
        private async Task<bool> CanAccessAsync(int? idCompany)
        {
            var allowed = await _permitsService.GetVisibleCompanyIds();
            if (allowed == null)
                return true;

            return idCompany != null && allowed.Contains(idCompany.Value);
        }

        private async Task<IQueryable<Invoice>?> FilterItems(InvoiceFilter? args = null)
        {
            try
            {
                var items = _context.Invoices
                    .Include(x => x.Company)
                    .Include(x => x.Contact)
                    .Include(x => x.Order)
                    .Include(x => x.User)
                    .AsQueryable();

                // Perimetro aziende dell'utente: senza questo filtro un utente cliente o
                // rivenditore vedrebbe le fatture di tutte le aziende dell'installazione.
                var allowed = await _permitsService.GetVisibleCompanyIds();
                if (allowed != null)
                    items = items.Where(x => x.IdCompany != null && allowed.Contains(x.IdCompany.Value));

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                    items = items.OrderBy(args.OrderBy);
                else
                    items = items.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id);

                if (args?.IdCompany != null)
                    items = items.Where(x => x.IdCompany == args.IdCompany);
                if (args?.IdOrder != null)
                    items = items.Where(x => x.IdOrder == args.IdOrder);
                if (args?.IdUser != null)
                    items = items.Where(x => x.IdUser == args.IdUser);
                if (args?.State != null)
                    items = items.Where(x => x.State == args.State);

                if (!string.IsNullOrWhiteSpace(args?.Search))
                {
                    var search = args.Search.Trim();
                    items = items.Where(x =>
                        (x.Number != null && x.Number.Contains(search)) ||
                        (x.Company != null && x.Company.RagioneSociale.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(args?.Filter))
                    items = items.Where(args.Filter);

                return items;
            }
            catch (Exception ex)
            {
                _logEventService.RegisterAsync(nameof(InvoicesService), nameof(FilterItems), EventsTypes.Error, ex).Wait();
                return null;
            }
        }
    }
}
