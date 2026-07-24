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
        private const long MaximumChunkSize = 16L * 1024 * 1024;
        private const long MaximumChunkedFileSize = 5L * 1024 * 1024 * 1024;
        private const int ChunkRequestSizeLimit = 20 * 1024 * 1024;
        private readonly IMachineBackupsService _service;
        private readonly IPermitsService _permits;
        private readonly IWebHostEnvironment _hostEnvironment;

        public MachineBackupsController(
            IMachineBackupsService service,
            IPermitsService permits,
            IWebHostEnvironment hostEnvironment)
        {
            _service = service;
            _permits = permits;
            _hostEnvironment = hostEnvironment;
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
        [RequestSizeLimit(ChunkRequestSizeLimit)]
        [HttpPost("{ownerType}/{ownerId:int}/chunks")]
        public async Task<ActionResult<MachineBackupChunkUploadResult>> UploadChunk(
            MachineBackupOwnerType ownerType,
            int ownerId,
            IFormFile chunk,
            [FromForm] string uploadId,
            [FromForm] int chunkIndex,
            [FromForm] int totalChunks,
            [FromForm] long totalSize,
            [FromForm] string fileName,
            [FromForm] string? contentType,
            [FromForm] string? description,
            [FromForm] string? externalReference,
            CancellationToken cancellationToken)
        {
            if (chunk == null || chunk.Length == 0)
            {
                return BadRequest("Select a non-empty backup chunk.");
            }

            if (!Guid.TryParse(uploadId, out var parsedUploadId))
            {
                return BadRequest("Invalid upload id.");
            }

            if (chunkIndex < 0 || totalChunks <= 0 || chunkIndex >= totalChunks)
            {
                return BadRequest("Invalid chunk index.");
            }

            if (chunk.Length > MaximumChunkSize)
            {
                return BadRequest($"Chunk size exceeds {MaximumChunkSize} bytes.");
            }

            if (totalSize <= 0 || totalSize > MaximumChunkedFileSize)
            {
                return BadRequest($"File size exceeds {MaximumChunkedFileSize} bytes.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("File name is required.");
            }

            var chunkDirectory = GetChunkDirectory(parsedUploadId);
            CleanupStaleChunkDirectories(TimeSpan.FromDays(1));
            Directory.CreateDirectory(chunkDirectory);
            var chunkPath = GetChunkPath(chunkDirectory, chunkIndex);

            await using (var source = chunk.OpenReadStream())
            await using (var destination = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            var receivedChunks = Directory.EnumerateFiles(chunkDirectory, "*.part").Count();
            if (receivedChunks < totalChunks)
            {
                return Ok(new MachineBackupChunkUploadResult
                {
                    IsComplete = false,
                    ReceivedChunks = receivedChunks
                });
            }

            var assembledPath = Path.Combine(chunkDirectory, "assembled.upload");
            try
            {
                await using (var assembled = new FileStream(assembledPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    for (var index = 0; index < totalChunks; index++)
                    {
                        var partPath = GetChunkPath(chunkDirectory, index);
                        if (!System.IO.File.Exists(partPath))
                        {
                            return BadRequest($"Missing chunk {index}.");
                        }

                        await using var part = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                        await part.CopyToAsync(assembled, cancellationToken);
                    }
                }

                var assembledInfo = new FileInfo(assembledPath);
                if (assembledInfo.Length != totalSize)
                {
                    return BadRequest("Assembled file size does not match the declared size.");
                }

                await using var finalStream = new FileStream(assembledPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                var created = await _service.UploadAsync(
                    ownerType,
                    ownerId,
                    Path.GetFileName(fileName),
                    string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                    finalStream,
                    new MachineBackupUploadMetadata { Description = description, ExternalReference = externalReference },
                    MachineBackupSource.Manual,
                    await _permits.IdUser(),
                    cancellationToken);

                return Ok(new MachineBackupChunkUploadResult
                {
                    IsComplete = true,
                    ReceivedChunks = totalChunks,
                    Backup = created
                });
            }
            finally
            {
                TryDeleteDirectory(chunkDirectory);
            }
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

        private string GetChunkDirectory(Guid uploadId)
        {
            return Path.Combine(
                GetChunkRootDirectory(),
                uploadId.ToString("N"));
        }

        private string GetChunkRootDirectory()
        {
            return Path.Combine(_hostEnvironment.ContentRootPath, "archives", "Temp", "MachineBackups");
        }

        private static string GetChunkPath(string chunkDirectory, int chunkIndex)
        {
            return Path.Combine(chunkDirectory, $"{chunkIndex:D8}.part");
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Temporary chunks can be removed by a later cleanup if deletion is blocked.
            }
        }

        private void CleanupStaleChunkDirectories(TimeSpan maxAge)
        {
            var root = GetChunkRootDirectory();
            if (!Directory.Exists(root))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.Subtract(maxAge);
            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                try
                {
                    var info = new DirectoryInfo(directory);
                    if (info.LastWriteTimeUtc < cutoff)
                    {
                        info.Delete(true);
                    }
                }
                catch
                {
                    // Best-effort cleanup; active uploads and locked files are left untouched.
                }
            }
        }
    }
}
