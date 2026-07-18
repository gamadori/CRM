using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Authorize(Policy = "AdminRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalTicketApiKeysController : ControllerBase
    {
        private readonly IExternalTicketApiService _service;

        public ExternalTicketApiKeysController(IExternalTicketApiService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<List<ExternalTicketApiKeyDTO>> GetList()
        {
            return _service.GetApiKeysAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ExternalTicketApiKeyCreateResponse>> Create(ExternalTicketApiKeyCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                return await _service.CreateApiKeyAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Revoke(int id)
        {
            return await _service.RevokeApiKeyAsync(id) ? NoContent() : NotFound();
        }
    }
}
