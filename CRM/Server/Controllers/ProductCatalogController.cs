using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ILogEventService = CRM.Client.Services.ILogEventService;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductCatalogController : ControllerBase
    {
        private readonly IProductCatalogService _service;
        private readonly ILogEventService _logEventService;

        public ProductCatalogController(IProductCatalogService service, ILogEventService logEventService)
        {
            _service = service;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<ActionResult<ProductCatalogPageDTO>> GetPage([FromQuery] ProductCatalogFilter? filter = null)
        {
            try
            {
                return Ok(await _service.GetPageAsync(filter));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductCatalogController), nameof(GetPage), CRM.Shared.LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("{idProduct}")]
        public async Task<ActionResult<ProductCatalogDetailDTO>> GetDetails(int idProduct)
        {
            try
            {
                var detail = await _service.GetDetailsAsync(idProduct);
                return detail == null ? NotFound() : Ok(detail);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductCatalogController), nameof(GetDetails), CRM.Shared.LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
