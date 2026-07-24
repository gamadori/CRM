using CRM.Client.Models;
using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GanttPhasesController : ControllerBase
    {
        private readonly IGanttPhasesService _service;

        public GanttPhasesController(IGanttPhasesService service)
        {
            _service = service;
        }

        [HttpGet("plan/{idGanttPlan}")]
        public async Task<ActionResult<IEnumerable<GanttPhaseDTO>>> GetTree(int idGanttPlan)
            => Ok(await _service.GetTreeAsync(idGanttPlan) ?? new List<GanttPhaseDTO>());

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<GanttPhaseDTO>>> Save(GanttPhaseDTO dto)
            => Ok(await _service.SaveAsync(dto));

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<GanttPhaseDTO>>> Update(int id, GanttPhaseDTO dto)
        {
            if (id != dto.Id) return BadRequest();
            return Ok(await _service.SaveAsync(dto));
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkSave(List<GanttPhaseDTO> dtos)
            => await _service.BulkSaveAsync(dtos) ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
            => await _service.DeleteAsync(id) ? NoContent() : BadRequest();

        [HttpPost("dependency")]
        public async Task<ActionResult<APIResponseMessage<GanttPhaseDependencyDTO>>> AddDependency(GanttPhaseDependencyDTO dto)
            => Ok(await _service.AddDependencyAsync(dto));

        [HttpDelete("dependency/{id}")]
        public async Task<IActionResult> RemoveDependency(int id)
            => await _service.RemoveDependencyAsync(id) ? NoContent() : BadRequest();
    }
}
