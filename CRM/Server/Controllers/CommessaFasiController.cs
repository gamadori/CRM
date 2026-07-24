using CRM.Client.Models;
using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommessaFasiController : ControllerBase
    {
        private readonly ICommessaFasiService _service;

        public CommessaFasiController(ICommessaFasiService service)
        {
            _service = service;
        }

        [HttpGet("commessa/{idCommessa}")]
        public async Task<ActionResult<IEnumerable<CommessaFaseDTO>>> GetTree(int idCommessa)
            => Ok(await _service.GetTreeAsync(idCommessa) ?? new List<CommessaFaseDTO>());

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<CommessaFaseDTO>>> Save(CommessaFaseDTO dto)
            => Ok(await _service.SaveAsync(dto));

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<CommessaFaseDTO>>> Update(int id, CommessaFaseDTO dto)
        {
            if (id != dto.Id) return BadRequest();
            return Ok(await _service.SaveAsync(dto));
        }

        [HttpPost("bulk")]
        public async Task<IActionResult> BulkSave(List<CommessaFaseDTO> dtos)
            => await _service.BulkSaveAsync(dtos) ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
            => await _service.DeleteAsync(id) ? NoContent() : BadRequest();

        [HttpPost("dependency")]
        public async Task<ActionResult<APIResponseMessage<CommessaFaseDependencyDTO>>> AddDependency(CommessaFaseDependencyDTO dto)
            => Ok(await _service.AddDependencyAsync(dto));

        [HttpDelete("dependency/{id}")]
        public async Task<IActionResult> RemoveDependency(int id)
            => await _service.RemoveDependencyAsync(id) ? NoContent() : BadRequest();
    }
}
