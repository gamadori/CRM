using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductCatalogAssetsController : ControllerBase
    {
        private readonly CRM.Server.Services.IProductCatalogAssetsService _service;
        private readonly ILogEventService _logEventService;

        public ProductCatalogAssetsController(CRM.Server.Services.IProductCatalogAssetsService service, ILogEventService logEventService)
        {
            _service = service;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<PagingResponse<ProductCatalogAssetDTO>?> GetPage([FromQuery] ProductCatalogAssetFilter? args = null)
        {
            return await _service.GetPagingAsync(args);
        }

        [HttpGet("list")]
        public async Task<IEnumerable<ProductCatalogAssetDTO>?> GetItems([FromQuery] ProductCatalogAssetFilter? args = null)
        {
            return await _service.GetListAsync(args) ?? Enumerable.Empty<ProductCatalogAssetDTO>();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductCatalogAssetDTO?>> GetItem(int id)
        {
            var item = await _service.GetItemAsync(id);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ProductCatalogAssetDTO>>> Post(ProductCatalogAsset item)
        {
            return Ok(await _service.PostAsync(item));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ProductCatalogAssetDTO>>> Put(int id, ProductCatalogAsset item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            return Ok(await _service.PostAsync(item));
        }

        [HttpPost("upload")]
        public async Task<ActionResult<APIResponseMessage<List<ProductCatalogAssetDTO>>>> Upload(ProductCatalogAssetUploadRequest request)
        {
            return Ok(await _service.UploadAsync(request));
        }

        [HttpPost("{id}/cover")]
        public async Task<IActionResult> SetCover(int id)
        {
            return await _service.SetCoverAsync(id) ? NoContent() : BadRequest();
        }

        [HttpGet("{id}/file")]
        public async Task<IActionResult> File(int id)
        {
            try
            {
                var file = await _service.DownloadFileAsync(id);
                if (file.Bytes.Length == 0)
                {
                    return NotFound();
                }

                return File(file.Bytes, file.ContentType, file.FileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductCatalogAssetsController), nameof(File), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _service.DeleteAsync(id) ? NoContent() : NotFound();
        }
    }
}
