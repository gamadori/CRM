using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketStatesController : ControllerBase
    {
        private readonly ITicketStatesService _service;

        private readonly ILogEventService _logEventService;
        public TicketStatesController(ITicketStatesService service, ILogEventService logEventService)
        {
            _service = service;
            _logEventService = logEventService;
           
        }
        // GET: api/Companies
        [HttpGet]
        public async Task<PagingResponse<TicketState>?> GetPage([FromQuery] TicketStateFilter? args = null)
        {
            try
            {
                var items = await _service.GetPagingAsync(args) ?? new PagingResponse<TicketState>();


                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        [HttpGet("list")]
        public async Task<IEnumerable<TicketState?>> GetItems([FromQuery] TicketStateFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);

                if (items == null)
                {
                    return Enumerable.Empty<TicketState>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketStatesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<TicketState>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TicketState>> Get(int id)
        {
            var item = await _service.GetItemAsync(id);

            if (item == null)
            {
                return NotFound();
            }
            return item;
        }

        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<Article>>> Put(int id, TicketState item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return Problem("Error saving Ticket State");

            return Ok(resp);
        }

        // POST: api/Companies
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<TicketState>>> Post(TicketState item)
        {
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        // DELETE: api/Companies/5
        //[AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Ticket State");
            }
            else
                return NoContent();
        }
    }
}
