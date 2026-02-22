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
    public class FolderLanguagesController : ControllerBase
    {
        private readonly IFolderLanguagesService _service;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permitsService;

        public FolderLanguagesController(IFolderLanguagesService service, ILogEventService logEventService, IPermitsService permitsService)
        {
            _service = service;
            _logEventService = logEventService;
            _permitsService = permitsService;
        }

        [HttpGet]
        public async Task<PagingResponse<FolderLanguageDTO>?> GetPage([FromQuery] FolderLanguageFilter? args = null)
        {
            try
            {
                var items = await _service.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(FolderLanguagesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<FolderLanguageDTO>?> GetItems([FromQuery] FolderLanguageFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<FolderLanguageDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(FolderLanguagesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<FolderLanguageDTO>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FolderLanguageDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _service.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(FolderLanguagesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<FolderLanguageDTO>>> Put(int id, FolderLanguage item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return Problem("Error saving folder");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<FolderLanguageDTO>>> Post(FolderLanguage item)
        {
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting folder language");
            }
            else
                return NoContent();
        }
    }
}