using CNM.Authorize;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [AuthorizeRole(ePolicy.AdminRole)]
    [Route("api/[controller]")]
    [ApiController]
    public class MachineParameterApiKeysController : ControllerBase
    {
        private readonly IMachineParameterApiKeyService _service;

        public MachineParameterApiKeysController(IMachineParameterApiKeyService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<List<MachineParameterApiKeyDTO>> GetList()
        {
            return await _service.GetListAsync();
        }

        [HttpPost]
        public async Task<MachineParameterApiKeyCreateResponse> Create(MachineParameterApiKeyCreateRequest request)
        {
            return await _service.CreateAsync(request);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Revoke(int id)
        {
            return await _service.RevokeAsync(id) ? NoContent() : NotFound();
        }
    }
}
