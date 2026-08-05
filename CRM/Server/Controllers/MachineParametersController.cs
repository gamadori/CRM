using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    [AllowAnonymous]
    [Route("api/machine")]
    [ApiController]
    public class MachineParametersController : ControllerBase
    {
        private const string ApiKeyHeader = "X-Api-Key";
        private readonly ApplicationDbContext _context;
        private readonly IApiKeyService _apiKeys;
        private readonly IMachineBackupsService _backups;

        public MachineParametersController(
            ApplicationDbContext context,
            IApiKeyService apiKeys,
            IMachineBackupsService backups)
        {
            _context = context;
            _apiKeys = apiKeys;
            _backups = backups;
        }

        [HttpGet("articles")]
        public async Task<ActionResult<List<MachineArticleDTO>>> GetArticles([FromQuery] MachineArticleListFilter filter)
        {
            if (!await IsAuthorized(ApiKeyPermission.ReadOnly))
            {
                return Unauthorized();
            }

            var query = _context.Articles.AsNoTracking().Include(x => x.Product).Include(x => x.Company).AsQueryable();
            if (filter.IdProduct != null) query = query.Where(x => x.IdProduct == filter.IdProduct);
            if (!string.IsNullOrWhiteSpace(filter.ProductCode)) query = query.Where(x => x.Product != null && x.Product.Code == filter.ProductCode);
            if (!string.IsNullOrWhiteSpace(filter.SerialNumber)) query = query.Where(x => x.SerialNumber.Contains(filter.SerialNumber));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(x => x.SerialNumber.Contains(search) || x.Name.Contains(search) ||
                    (x.Product != null && (x.Product.Name.Contains(search) || x.Product.Code.Contains(search))));
            }

            var take = Math.Clamp(filter.Take.GetValueOrDefault(50), 1, 200);
            var articles = await query.OrderBy(x => x.Product.Name).ThenBy(x => x.SerialNumber)
                .Skip(Math.Max(filter.Skip.GetValueOrDefault(), 0)).Take(take).ToListAsync();
            var ids = articles.Select(x => x.Id).ToList();
            var dates = await _context.MachineBackups.AsNoTracking()
                .Where(x => x.OwnerType == MachineBackupOwnerType.Article && x.IdArticle != null && ids.Contains(x.IdArticle.Value))
                .GroupBy(x => x.IdArticle!.Value)
                .Select(x => new { Id = x.Key, Date = x.Max(y => y.CreatedAt) })
                .ToDictionaryAsync(x => x.Id, x => x.Date);

            return Ok(articles.Select(x => new MachineArticleDTO
            {
                Id = x.Id,
                IdProduct = x.IdProduct,
                ProductCode = x.Product?.Code,
                ProductName = x.Product?.Name,
                SerialNumber = x.SerialNumber,
                Name = x.Name,
                Year = x.Year,
                CompanyName = x.Company?.RagioneSociale,
                CompleteName = $"{x.Product?.Name} - {x.SerialNumber}",
                LastBackupAt = dates.GetValueOrDefault(x.Id)
            }).ToList());
        }

        [HttpGet("products/{idProduct:int}/backups/latest")]
        public Task<ActionResult> GetLatestProductBackup(int idProduct) => GetLatest(MachineBackupOwnerType.Product, idProduct);

        [HttpGet("articles/{idArticle:int}/backups/latest")]
        public Task<ActionResult> GetLatestArticleBackup(int idArticle) => GetLatest(MachineBackupOwnerType.Article, idArticle);

        [HttpGet("backups/{id:int}/file")]
        public async Task<IActionResult> Download(int id)
        {
            if (!await IsAuthorized(ApiKeyPermission.ReadOnly)) return Unauthorized();
            var file = await _backups.DownloadAsync(id);
            return file == null ? NotFound() : File(file.Value.Content, file.Value.ContentType, file.Value.FileName, true);
        }

        [RequestSizeLimit(536_870_912)]
        [HttpPost("articles/{idArticle:int}/backups")]
        public async Task<ActionResult<MachineBackupDTO>> UploadArticleBackup(
            int idArticle,
            IFormFile file,
            [FromForm] string? description,
            [FromForm] string? externalReference,
            CancellationToken cancellationToken)
        {
            var apiKey = await Authorize(ApiKeyPermission.ReadWrite);
            if (apiKey == null) return Unauthorized();
            if (file == null || file.Length == 0) return BadRequest("Select a non-empty backup file.");

            await using var stream = file.OpenReadStream();
            var created = await _backups.UploadAsync(
                MachineBackupOwnerType.Article,
                idArticle,
                file.FileName,
                file.ContentType,
                stream,
                new MachineBackupUploadMetadata { Description = description, ExternalReference = externalReference },
                MachineBackupSource.MachineApi,
                $"api-key:{apiKey.Id}",
                cancellationToken);
            return Ok(created);
        }

        private async Task<ActionResult> GetLatest(MachineBackupOwnerType ownerType, int ownerId)
        {
            if (!await IsAuthorized(ApiKeyPermission.ReadOnly)) return Unauthorized();
            var backup = await _backups.GetLatestAsync(ownerType, ownerId);
            return backup == null ? NotFound() : Ok(backup);
        }

        private async Task<bool> IsAuthorized(ApiKeyPermission permission) => await Authorize(permission) != null;

        private Task<ApiKey?> Authorize(ApiKeyPermission permission)
        {
            var value = Request.Headers.TryGetValue(ApiKeyHeader, out var values) ? values.FirstOrDefault() : null;
            return _apiKeys.ValidateAsync(value, ApiKeyScope.MachineBackup, permission);
        }
    }
}
