using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PriceListController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly Services.IPriceListService _priceListService;

        public PriceListController(ILogEventService logEventService, Services.IPriceListService priceListService)
        {
            _logEventService = logEventService;
            _priceListService = priceListService;
        }

        [HttpGet("by-company/{idCompany}")]
        public async Task<ActionResult<IEnumerable<PriceListItemDTO>>> GetByCompany(int idCompany)
        {
            try
            {
                var items = await _priceListService.GetByCompanyAsync(idCompany);
                return Ok(items ?? new List<PriceListItemDTO>());
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(PriceListController), nameof(GetByCompany), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("resolve")]
        public async Task<ActionResult<PriceListItemDTO?>> Resolve([FromQuery] int idCompany, [FromQuery] int idProduct)
        {
            try
            {
                var item = await _priceListService.ResolveAsync(idCompany, idProduct);
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(PriceListController), nameof(Resolve), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<PriceListItemDTO>>> Upsert(PriceListItem item)
        {
            var resp = await _priceListService.UpsertAsync(item);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Upsert return null");
            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _priceListService.DeleteAsync(id);
            if (!resp)
                return StatusCode(StatusCodes.Status500InternalServerError, "Errore nell'eliminazione della voce di listino");
            return NoContent();
        }
    }
}
