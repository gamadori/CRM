using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace RedG.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogosController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly ILogosService _logosService;
        public LogosController(ILogEventService logEventService, ILogosService logosService)
        {
            _logEventService = logEventService;
            _logosService = logosService;
        }

        // GET: api/Companies
        [HttpGet]
        public async Task<PagingResponse<Logo>?> GetPage([FromQuery] LogosFilterModel? args = null)
        {
            try
            {
                var items = await _logosService.GetPagingAsync(args);

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<Logo?>> GetItems([FromQuery] LogosFilterModel? args = null)
        {
            try
            {
                var items = await _logosService.GetListAsync(args);

                if (items == null)
                {
                    return Enumerable.Empty<Logo>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LogosController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<Logo>();
            }
        }

        
        [HttpGet("{id}")]
        public async Task<ActionResult<Logo>> Get(int id)
        {
            var item = await _logosService.GetItemAsync(id);

            if (item == null)
            {
                return NotFound();
            }
            return item;
        }
        
        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        
        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<Logo>>> Put(int id, Logo item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _logosService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving settings");

            return Ok(resp);
        }

        // POST: api/Companies
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<Logo>>> Post(Logo item)
        {
            var resp = await _logosService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }
        
        // DELETE: api/Companies/5
        //[AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _logosService.DeleteAsync(id);
            
            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Logo");
            }
            else
                return NoContent();
        }
    }
}
