using CNM.Authorize;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MachineBackupsController : ControllerBase
    {
        private readonly IMachineBackupsService _service;
        private readonly IPermitsService _permits;

        public MachineBackupsController(IMachineBackupsService service, IPermitsService permits)
        {
            _service = service;
            _permits = permits;
        }

        [HttpGet]
        public async Task<ActionResult<MachineBackupListDTO>> GetList([FromQuery] MachineBackupFilter filter)
        {
            if (!await CanAccessAsync(filter.OwnerType, filter.OwnerId))
            {
                return Forbid();
            }

            return Ok(await _service.GetListAsync(filter));
        }

        [HttpGet("{id:int}/file")]
        public async Task<IActionResult> Download(int id)
        {
            var backup = await _service.GetAsync(id);
            if (backup == null)
            {
                return NotFound();
            }

            var ownerId = backup.IdProduct ?? backup.IdArticle ?? 0;
            if (!await CanAccessAsync(backup.OwnerType, ownerId))
            {
                return Forbid();
            }

            var file = await _service.DownloadAsync(id);
            return file == null
                ? NotFound()
                : File(file.Value.Content, file.Value.ContentType, file.Value.FileName, enableRangeProcessing: true);
        }

        [AuthorizeRole(ePolicy.AdminRole)]
        [RequestSizeLimit(536_870_912)]
        [HttpPost("{ownerType}/{ownerId:int}")]
        public async Task<ActionResult<MachineBackupDTO>> Upload(
            MachineBackupOwnerType ownerType,
            int ownerId,
            IFormFile file,
            [FromForm] string? description,
            [FromForm] string? externalReference,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Select a non-empty backup file.");
            }

            await using var stream = file.OpenReadStream();
            var created = await _service.UploadAsync(
                ownerType,
                ownerId,
                file.FileName,
                file.ContentType,
                stream,
                new MachineBackupUploadMetadata { Description = description, ExternalReference = externalReference },
                MachineBackupSource.Manual,
                await _permits.IdUser(),
                cancellationToken);

            return CreatedAtAction(nameof(Download), new { id = created.Id }, created);
        }

        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _service.DeleteAsync(id) ? NoContent() : NotFound();
        }

        private async Task<bool> CanAccessAsync(MachineBackupOwnerType ownerType, int ownerId)
        {
            return ownerType == MachineBackupOwnerType.Product || await _permits.ArticleCanAccess(ownerId);
        }
    }
}
