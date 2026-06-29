using CNM.Authorize;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LicensesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IRsaLicenseService _rsa;

        public LicensesController(ApplicationDbContext db, IRsaLicenseService rsa)
        {
            _db = db;
            _rsa = rsa;
        }

        // ── Feature Definitions ─────────────────────────────────────────────

        [HttpGet("feature-defs")]
        public async Task<ActionResult<List<ArticleLicenseFeatureDefDTO>>> GetFeatureDefs(
            [FromQuery] int? idProductType = null,
            [FromQuery] int? idProduct = null)
        {
            var q = _db.ArticleLicenseFeatureDefs
                .Include(d => d.ProductType)
                .Include(d => d.Product)
                .AsNoTracking()
                .AsQueryable();

            if (idProductType.HasValue || idProduct.HasValue)
            {
                q = q.Where(d =>
                    (idProductType.HasValue && d.IdProductType == idProductType.Value && d.IdProduct == null) ||
                    (idProduct.HasValue && d.IdProduct == idProduct.Value));
            }

            var list = await q.OrderBy(d => d.IdProductType).ThenBy(d => d.IdProduct).ThenBy(d => d.Label).ToListAsync();
            return list.Select(ToDefDto).ToList();
        }

        [HttpPost("feature-defs")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<ActionResult<ArticleLicenseFeatureDefDTO>> CreateFeatureDef([FromBody] ArticleLicenseFeatureDefDTO dto)
        {
            var def = new ArticleLicenseFeatureDef
            {
                Key = dto.Key.Trim(),
                Label = dto.Label.Trim(),
                Description = dto.Description?.Trim(),
                ValueType = dto.ValueType,
                DefaultValue = dto.DefaultValue ?? "false",
                IdProductType = dto.IdProductType,
                IdProduct = dto.IdProduct,
                IsActive = true
            };
            _db.ArticleLicenseFeatureDefs.Add(def);
            await _db.SaveChangesAsync();
            await _db.Entry(def).Reference(d => d.ProductType).LoadAsync();
            await _db.Entry(def).Reference(d => d.Product).LoadAsync();
            return CreatedAtAction(nameof(GetFeatureDefs), ToDefDto(def));
        }

        [HttpPut("feature-defs/{id:int}")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<ActionResult<ArticleLicenseFeatureDefDTO>> UpdateFeatureDef(int id, [FromBody] ArticleLicenseFeatureDefDTO dto)
        {
            var def = await _db.ArticleLicenseFeatureDefs.FindAsync(id);
            if (def == null) return NotFound();

            def.Key = dto.Key.Trim();
            def.Label = dto.Label.Trim();
            def.Description = dto.Description?.Trim();
            def.ValueType = dto.ValueType;
            def.DefaultValue = dto.DefaultValue ?? "false";
            def.IdProductType = dto.IdProductType;
            def.IdProduct = dto.IdProduct;
            def.IsActive = dto.IsActive;

            await _db.SaveChangesAsync();
            await _db.Entry(def).Reference(d => d.ProductType).LoadAsync();
            await _db.Entry(def).Reference(d => d.Product).LoadAsync();
            return ToDefDto(def);
        }

        [HttpDelete("feature-defs/{id:int}")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<IActionResult> DeleteFeatureDef(int id)
        {
            var def = await _db.ArticleLicenseFeatureDefs.FindAsync(id);
            if (def == null) return NotFound();
            _db.ArticleLicenseFeatureDefs.Remove(def);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ── Licenze ─────────────────────────────────────────────────────────

        [HttpGet("article/{articleId:int}")]
        public async Task<ActionResult<ArticleLicenseDTO>> GetByArticle(int articleId)
        {
            var lic = await _db.ArticleLicenses
                .Include(l => l.Article)
                .Include(l => l.Features).ThenInclude(f => f.FeatureDef)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.IdArticle == articleId);

            if (lic == null) return NotFound();
            return ToDto(lic);
        }

        [HttpPost]
        public async Task<ActionResult<ArticleLicenseDTO>> Save([FromBody] ArticleLicenseSaveRequest req)
        {
            var article = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == req.IdArticle);
            if (article == null) return BadRequest("Article not found");

            var existing = await _db.ArticleLicenses
                .Include(l => l.Features)
                .FirstOrDefaultAsync(l => l.IdArticle == req.IdArticle);

            if (existing == null)
            {
                existing = new ArticleLicense
                {
                    IdArticle = req.IdArticle,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ArticleLicenses.Add(existing);
            }

            existing.StartDate = req.StartDate;
            existing.ExpirationDate = req.ExpirationDate;
            existing.IsActive = req.IsActive;
            existing.Notes = req.Notes?.Trim();
            existing.UpdatedAt = DateTime.UtcNow;

            // Sync features
            var toRemove = existing.Features
                .Where(f => !req.Features.Any(r => r.IdFeatureDef == f.IdFeatureDef))
                .ToList();
            _db.ArticleLicenseFeatures.RemoveRange(toRemove);

            foreach (var reqF in req.Features)
            {
                var feat = existing.Features.FirstOrDefault(f => f.IdFeatureDef == reqF.IdFeatureDef);
                if (feat == null)
                {
                    feat = new ArticleLicenseFeature { IdLicense = existing.Id, IdFeatureDef = reqF.IdFeatureDef };
                    existing.Features.Add(feat);
                }
                feat.Value = reqF.Value ?? "false";
                feat.IsEnabled = reqF.IsEnabled;
            }

            await _db.SaveChangesAsync();

            await _db.Entry(existing).Reference(l => l.Article).LoadAsync();
            await _db.Entry(existing).Collection(l => l.Features).Query()
                .Include(f => f.FeatureDef).LoadAsync();

            return ToDto(existing);
        }

        [HttpDelete("article/{articleId:int}")]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<IActionResult> Delete(int articleId)
        {
            var lic = await _db.ArticleLicenses.FirstOrDefaultAsync(l => l.IdArticle == articleId);
            if (lic == null) return NotFound();
            _db.ArticleLicenses.Remove(lic);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("article/{articleId:int}/download")]
        public async Task<IActionResult> Download(int articleId)
        {
            var lic = await _db.ArticleLicenses
                .Include(l => l.Article)
                .Include(l => l.Features).ThenInclude(f => f.FeatureDef)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.IdArticle == articleId);

            if (lic == null) return NotFound("Nessuna licenza configurata per questa matricola.");
            if (string.IsNullOrEmpty(lic.MachineKey)) return BadRequest("MachineKey non ancora registrata. La macchina deve avviarsi almeno una volta.");

            var payload = _rsa.GenerateLicense(lic, lic.Article.SerialNumber);
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/octet-stream", $"license_{lic.Article.SerialNumber}.lic");
        }

        [HttpGet("public-key")]
        [AllowAnonymous]
        public IActionResult GetPublicKey()
        {
            var pem = _rsa.ExportPublicKeyPem();
            return Content(pem, "text/plain", Encoding.UTF8);
        }

        // ── Mapping ─────────────────────────────────────────────────────────

        private static ArticleLicenseDTO ToDto(ArticleLicense l) => new()
        {
            Id = l.Id,
            IdArticle = l.IdArticle,
            SerialNumber = l.Article?.SerialNumber ?? string.Empty,
            MachineKey = l.MachineKey,
            StartDate = l.StartDate,
            ExpirationDate = l.ExpirationDate,
            IsActive = l.IsActive,
            Notes = l.Notes,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt,
            Features = l.Features?.Select(f => new ArticleLicenseFeatureDTO
            {
                Id = f.Id,
                IdLicense = f.IdLicense,
                IdFeatureDef = f.IdFeatureDef,
                FeatureKey = f.FeatureDef?.Key ?? string.Empty,
                FeatureLabel = f.FeatureDef?.Label ?? string.Empty,
                FeatureDescription = f.FeatureDef?.Description,
                ValueType = f.FeatureDef?.ValueType ?? LicenseFeatureValueType.Bool,
                Value = f.Value,
                IsEnabled = f.IsEnabled
            }).ToList() ?? new()
        };

        private static ArticleLicenseFeatureDefDTO ToDefDto(ArticleLicenseFeatureDef d) => new()
        {
            Id = d.Id,
            Key = d.Key,
            Label = d.Label,
            Description = d.Description,
            ValueType = d.ValueType,
            DefaultValue = d.DefaultValue,
            IdProductType = d.IdProductType,
            ProductTypeName = d.ProductType?.Name,
            IdProduct = d.IdProduct,
            ProductName = d.Product?.Name,
            IsActive = d.IsActive
        };
    }
}
