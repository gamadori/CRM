using CNM.Authorize;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AuthorizeRole(ePolicy.AdminRole)]
    public sealed class MaintenanceController : ControllerBase
    {
        private readonly MaintenanceState _state;
        private readonly IHubContext<SignalRHub> _hub;

        public MaintenanceController(MaintenanceState state, IHubContext<SignalRHub> hub)
        {
            _state = state;
            _hub = hub;
        }

        [HttpGet]
        public ActionResult<MaintenanceNoticeDTO> Get() => Ok(_state.GetCurrent());

        [HttpPost("schedule")]
        public async Task<ActionResult<MaintenanceNoticeDTO>> Schedule(ScheduleMaintenanceRequest request)
        {
            var notice = _state.Schedule(request.Minutes, request.Message);
            await _hub.Clients.All.SendAsync("MaintenanceNotice", notice);
            return Ok(notice);
        }

        [HttpDelete]
        public async Task<ActionResult<MaintenanceNoticeDTO>> Cancel()
        {
            var notice = _state.Cancel();
            await _hub.Clients.All.SendAsync("MaintenanceNotice", notice);
            return Ok(notice);
        }
    }
}
