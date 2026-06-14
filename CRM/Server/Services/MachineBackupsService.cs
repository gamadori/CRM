using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CRM.Server.Services
{
    public class MachineBackupsService : IMachineBackupsService
    {
        private const int MaximumPageSize = 200;
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archive;

        public MachineBackupsService(ApplicationDbContext context, IArchiveService archive)
        {
            _context = context;
            _archive = archive;
            _archive.TypeArchive = ArchiveTypes.MachineBackups;
        }

        public async Task<MachineBackupListDTO> GetListAsync(MachineBackupFilter filter)
        {
            var query = FilterByOwner(filter.OwnerType, filter.OwnerId).AsNoTracking();
            var count = await query.CountAsync();
            var take = Math.Clamp(filter.Take, 1, MaximumPageSize);
            var items = await query
                .OrderByDescending(x => x.Version)
                .Skip(Math.Max(filter.Skip, 0))
                .Take(take)
                .Select(Projection)
                .ToListAsync();

            return new MachineBackupListDTO { Items = items, TotalCount = count };
        }

        public async Task<MachineBackupDTO?> GetLatestAsync(MachineBackupOwnerType ownerType, int ownerId)
        {
            return await FilterByOwner(ownerType, ownerId)
                .AsNoTracking()
                .OrderByDescending(x => x.Version)
                .Select(Projection)
                .FirstOrDefaultAsync();
        }

        public async Task<MachineBackupDTO?> GetAsync(int id)
        {
            return await _context.MachineBackups.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(Projection)
                .FirstOrDefaultAsync();
        }

        public async Task<MachineBackupDTO> UploadAsync(
            MachineBackupOwnerType ownerType,
            int ownerId,
            string fileName,
            string contentType,
            Stream content,
            MachineBackupUploadMetadata metadata,
            MachineBackupSource source,
            string? createdBy,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnerExistsAsync(ownerType, ownerId);

            var nextVersion = (await FilterByOwner(ownerType, ownerId).MaxAsync(x => (int?)x.Version) ?? 0) + 1;
            var backup = new MachineBackup
            {
                OwnerType = ownerType,
                IdProduct = ownerType == MachineBackupOwnerType.Product ? ownerId : null,
                IdArticle = ownerType == MachineBackupOwnerType.Article ? ownerId : null,
                FileName = Path.GetFileName(fileName),
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                Version = nextVersion,
                CreatedAt = DateTime.UtcNow,
                Source = source,
                Description = metadata.Description?.Trim(),
                ExternalReference = metadata.ExternalReference?.Trim(),
                CreatedBy = createdBy
            };

            _context.MachineBackups.Add(backup);
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                var stored = await _archive.SaveStreamAsync(backup.Id, backup.FileName, content, cancellationToken);
                backup.Size = stored.Size;
                backup.Sha256 = stored.Sha256;
                await _context.SaveChangesAsync(cancellationToken);
                return (await GetAsync(backup.Id))!;
            }
            catch
            {
                _context.MachineBackups.Remove(backup);
                await _context.SaveChangesAsync(CancellationToken.None);
                _archive.Delete(backup.Id, backup.FileName);
                throw;
            }
        }

        public async Task<(Stream Content, string ContentType, string FileName)?> DownloadAsync(int id)
        {
            var backup = await _context.MachineBackups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (backup == null || string.IsNullOrWhiteSpace(backup.Sha256))
            {
                return null;
            }

            return (_archive.OpenRead(backup.Id, backup.FileName), backup.ContentType, backup.FileName);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var backup = await _context.MachineBackups.FirstOrDefaultAsync(x => x.Id == id);
            if (backup == null)
            {
                return false;
            }

            _context.MachineBackups.Remove(backup);
            await _context.SaveChangesAsync();
            _archive.Delete(backup.Id, backup.FileName);
            return true;
        }

        private IQueryable<MachineBackup> FilterByOwner(MachineBackupOwnerType ownerType, int ownerId)
        {
            return ownerType == MachineBackupOwnerType.Product
                ? _context.MachineBackups.Where(x => x.OwnerType == ownerType && x.IdProduct == ownerId)
                : _context.MachineBackups.Where(x => x.OwnerType == ownerType && x.IdArticle == ownerId);
        }

        private async Task EnsureOwnerExistsAsync(MachineBackupOwnerType ownerType, int ownerId)
        {
            var exists = ownerType == MachineBackupOwnerType.Product
                ? await _context.Products.AnyAsync(x => x.Id == ownerId)
                : await _context.Articles.AnyAsync(x => x.Id == ownerId);

            if (!exists)
            {
                throw new KeyNotFoundException("Product or article not found.");
            }
        }

        private static readonly Expression<Func<MachineBackup, MachineBackupDTO>> Projection = x =>
            new MachineBackupDTO
            {
                Id = x.Id,
                OwnerType = x.OwnerType,
                IdProduct = x.IdProduct,
                IdArticle = x.IdArticle,
                OwnerName = x.OwnerType == MachineBackupOwnerType.Product
                    ? x.Product!.Name
                    : $"{x.Article!.Name} - {x.Article.SerialNumber}",
                FileName = x.FileName,
                ContentType = x.ContentType,
                Size = x.Size,
                Sha256 = x.Sha256,
                Version = x.Version,
                CreatedAt = x.CreatedAt,
                Source = x.Source,
                Description = x.Description,
                ExternalReference = x.ExternalReference,
                CreatedBy = x.CreatedBy
            };
    }
}
