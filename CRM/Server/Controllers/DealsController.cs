using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DealsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;
        private readonly IDealsService _dealService;
        public DealsController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits, IDealsService dealService)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
            _dealService = dealService;
        }

        // GET: api/Deals
        [HttpGet]
        public async Task<ActionResult<PagingResponse<DealDTO, decimal>>> GetDeal([FromQuery]DealFilter args)
        {
            try
            {
               

                var deals = await _dealService.GetSummaryAsync(args);

                return deals ?? new PagingResponse<DealDTO, decimal>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(GetDeal), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<DealDTO>?> GetItems([FromQuery] DealFilter? args = null)
        {
            try
            {
                var items = await _dealService.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<DealDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<DealDTO>();
            }
        }

        [HttpGet("forecast")]
        public async Task<ActionResult<CommercialForecastDTO>> GetForecast([FromQuery] DealForecastFilter? args = null)
        {
            try
            {
                var forecast = await _dealService.GetForecastAsync(args);
                return forecast == null ? StatusCode(StatusCodes.Status500InternalServerError) : Ok(forecast);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(GetForecast), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DealDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _dealService.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }


        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<DealDTO>>> Put(int id, Deal item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _dealService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving deal");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<DealDTO>>> Post(Deal item)
        {
            var resp = await _dealService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _dealService.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting Deal");
            }
            else
                return NoContent();
        }
    }
}
