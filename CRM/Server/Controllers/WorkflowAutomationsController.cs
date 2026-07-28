using CRM.Client.Models;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WorkflowAutomationsController : ControllerBase
    {
        private readonly IWorkflowAutomationService _service;

        public WorkflowAutomationsController(IWorkflowAutomationService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<PagingResponse<WorkflowAutomation>>> Get([FromQuery] WorkflowAutomationFilter args)
        {
            return await _service.GetPagingAsync(args) ?? new PagingResponse<WorkflowAutomation>();
        }

        [HttpGet("list")]
        public async Task<IEnumerable<WorkflowAutomation>> GetList([FromQuery] WorkflowAutomationFilter? args = null)
        {
            return await _service.GetListAsync(args) ?? Enumerable.Empty<WorkflowAutomation>();
        }

        [HttpGet("executions")]
        public async Task<ActionResult<PagingResponse<WorkflowAutomationExecutionDTO>>> GetExecutions([FromQuery] WorkflowAutomationExecutionFilter args)
        {
            return await _service.GetExecutionsAsync(args) ?? new PagingResponse<WorkflowAutomationExecutionDTO>();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<WorkflowAutomation?>> GetItem(int id)
        {
            var item = await _service.GetItemAsync(id);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<WorkflowAutomation>>> Post(WorkflowAutomation item)
        {
            return Ok(await _service.PostAsync(item));
        }

        [HttpPost("run")]
        public async Task<ActionResult<int>> Run([FromQuery] int maxItems = 50)
        {
            return Ok(await _service.ExecutePendingAsync(maxItems));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<APIResponseMessage<WorkflowAutomation>>> Put(int id, WorkflowAutomation item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            return Ok(await _service.PostAsync(item));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _service.DeleteAsync(id) ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
