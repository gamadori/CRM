using CRM.Client.Models;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GanttPlansController : ControllerBase
    {
        private readonly IGanttPlansService _service;

        public GanttPlansController(IGanttPlansService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<GanttPlanDTO>>> GetList([FromQuery] GanttPlanFilter? args = null)
        {
            var items = await _service.GetListAsync(args);
            return Ok(items ?? new List<GanttPlanDTO>());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GanttPlanDTO>> GetItem(int id)
        {
            var item = await _service.GetItemAsync(id);
            if (item == null)
                return NotFound();

            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<GanttPlanDTO>>> Save(GanttPlanDTO dto)
        {
            var resp = await _service.SaveAsync(dto);
            return Ok(resp);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<GanttPlanDTO>>> Update(int id, GanttPlanDTO dto)
        {
            if (id != dto.Id)
                return BadRequest();
            var resp = await _service.SaveAsync(dto);
            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<APIResponseMessage<GanttPlanDTO>>> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);
            return Ok(resp);
        }
    }
}
