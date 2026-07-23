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
        private readonly IAppOfflineService _appOfflineService;

        public MaintenanceController(
            MaintenanceState state,
            IHubContext<SignalRHub> hub,
            IAppOfflineService appOfflineService)
        {
            _state = state;
            _hub = hub;
            _appOfflineService = appOfflineService;
        }

        [HttpGet]
        public ActionResult<MaintenanceStatusDTO> Get() => Ok(GetStatus());

        [HttpPost("schedule")]
        public async Task<ActionResult<MaintenanceStatusDTO>> Schedule(ScheduleMaintenanceRequest request)
        {
            var notice = _state.Schedule(request.Minutes, request.Message, request.AutoPublishAppOffline);
            await _hub.Clients.All.SendAsync("MaintenanceNotice", notice);
            return Ok(GetStatus());
        }

        [HttpDelete]
        public async Task<ActionResult<MaintenanceStatusDTO>> Cancel()
        {
            var notice = _state.Cancel();
            await _hub.Clients.All.SendAsync("MaintenanceNotice", notice);
            return Ok(GetStatus());
        }

        private MaintenanceStatusDTO GetStatus() => new()
        {
            Notice = _state.GetCurrent(),
            ConnectedUsers = SignalRHub.ConnectedUsersCount,
            ConnectedConnections = SignalRHub.ConnectedConnectionsCount,
            AppOfflineFileExists = _appOfflineService.Exists()
        };
    }
}
