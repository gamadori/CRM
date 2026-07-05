using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Gestione listini prezzi: prezzo/sconto di un prodotto per uno specifico cliente.
    /// Usato in fase di preventivo/ordine per proporre il prezzo dedicato al posto del catalogo.
    /// </summary>
    public class PriceListService : IPriceListService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;

        public PriceListService(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        public async Task<List<PriceListItemDTO>?> GetByCompanyAsync(int idCompany)
        {
            try
            {
                var items = await _context.PriceListItems
                    .Include(p => p.Product)
                    .Where(p => p.IdCompany == idCompany)
                    .OrderBy(p => p.Product!.Name)
                    .AsNoTracking()
                    .ToListAsync();

                return items.Select(i => i.ToDTO()!).ToList();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(PriceListService), nameof(GetByCompanyAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<PriceListItemDTO?> ResolveAsync(int idCompany, int idProduct)
        {
            try
            {
                var item = await _context.PriceListItems
                    .Include(p => p.Product)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdCompany == idCompany && p.IdProduct == idProduct);

                return item.ToDTO();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(PriceListService), nameof(ResolveAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<PriceListItemDTO>> UpsertAsync(PriceListItem item)
        {
            try
            {
                var existing = await _context.PriceListItems
                    .FirstOrDefaultAsync(p => p.IdCompany == item.IdCompany && p.IdProduct == item.IdProduct);

                if (existing != null)
                {
                    existing.UnitPrice = item.UnitPrice;
                    existing.DiscountPct = item.DiscountPct;
                }
                else
                {
                    existing = new PriceListItem
                    {
                        IdCompany = item.IdCompany,
                        IdProduct = item.IdProduct,
                        UnitPrice = item.UnitPrice,
                        DiscountPct = item.DiscountPct
                    };
                    _context.PriceListItems.Add(existing);
                }

                await _context.SaveChangesAsync();

                var dto = await ResolveAsync(existing.IdCompany, existing.IdProduct);

                return new APIResponseMessage<PriceListItemDTO>
                {
                    State = true,
                    Data = dto,
                    Message = "Voce di listino salvata",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(PriceListService), nameof(UpsertAsync), EventsTypes.Error, ex);
                return new APIResponseMessage<PriceListItemDTO>
                {
                    State = false,
                    Message = "Errore nel salvataggio della voce di listino",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.PriceListItems.FindAsync(id);
            if (item == null)
                return false;
            try
            {
                _context.PriceListItems.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(PriceListService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }
    }
}
