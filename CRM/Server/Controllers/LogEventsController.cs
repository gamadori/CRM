using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogEventsController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        public LogEventsController(ILogEventService logEventService)
        {
            _logEventService = logEventService;
            
        }

        // GET: api/Companies
        [HttpGet]
        public async Task<PagingResponse<LogEvent>?> GetPage([FromQuery] LogEventFilterModel? args = null)
        {
            try
            {
                var items = await _logEventService.GetPagingAsync(args);

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GroupsController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<LogEvent?>> GetItems([FromQuery] LogEventFilterModel? args = null)
        {
            try
            {
                var items = await _logEventService.GetListAsync(args);

                if (items == null)
                {
                    return Enumerable.Empty<LogEvent>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GroupsController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<LogEvent>();
            }
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<LogEvent>> Get(int id)
        {
            var item = await _logEventService.GetItemAsync(id);

            if (item == null)
            {
                return NotFound();
            }
            return item;
        }

        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<LogEvent>>> Put(int id, LogEvent item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _logEventService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving settings");

            return Ok(resp);
        }

        // POST: api/Companies
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<LogEvent>>> Post(LogEvent item)
        {
            var resp = await _logEventService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        // DELETE: api/Companies/5
        //[AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _logEventService.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Group");
            }
            else
                return NoContent();
        }

        [HttpGet("activities")]
        public async Task<IActionResult> GetActivities([FromQuery] string? userId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] ActivityType? type)
        {
            LogEventFilterModel filterModel = new LogEventFilterModel() { Skip = 0, Top = 20 };

            filterModel.EventType = EventsTypes.Info;

            var query = await _logEventService.GetPagingAsync(filterModel);

            

            if (query == null)
                return Ok(new List<ActivityModel>());   
            var activities = query.Items.Select(x => new ActivityModel() { Date = x.DateEvent, Description = x.Message, Title = x.Module, Type = x.ActivityType }).OrderByDescending(x => x.Date);
            return Ok(activities);
        }

    }
}
