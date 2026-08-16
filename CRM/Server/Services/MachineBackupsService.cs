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

                // Stesso file dell'ultima volta: non e' una versione nuova, e' la stessa
                // configurazione rimandata. L'impronta si conosce solo dopo aver scritto il file,
                // quindi la versione appena creata si butta ora. Chi ha caricato riceve la versione
                // buona, cosi' rimandare lo stesso backup non e' un errore: semplicemente non
                // cambia niente.
                var precedente = await FindPreviousIdenticalAsync(backup, cancellationToken);
                if (precedente != null)
                {
                    _context.MachineBackups.Remove(backup);
                    await _context.SaveChangesAsync(cancellationToken);
                    _archive.Delete(backup.Id, backup.FileName);
                    return (await GetAsync(precedente.Id))!;
                }

                await ApplyRetentionAsync(ownerType, ownerId, cancellationToken);

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

        /// <summary>
        /// La versione precedente con la stessa impronta, se il file appena arrivato e' identico a
        /// quello di prima. Si guarda solo l'ultima: due backup uguali a distanza di mesi, con
        /// altri diversi in mezzo, raccontano che la configurazione e' tornata indietro - e questo
        /// va tenuto.
        /// </summary>
        private async Task<MachineBackup?> FindPreviousIdenticalAsync(MachineBackup backup, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(backup.Sha256))
                return null;

            var ownerId = backup.IdArticle ?? backup.IdProduct ?? 0;

            var ultimo = await FilterByOwner(backup.OwnerType, ownerId)
                .AsNoTracking()
                .Where(x => x.Id != backup.Id)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            return ultimo != null && ultimo.Sha256 == backup.Sha256 ? ultimo : null;
        }

        /// <summary>
        /// Tiene le ultime N versioni e butta le eccedenti. La PRIMA non si tocca mai: e' la
        /// configurazione con cui la macchina e' partita, e serve per capire cosa e' cambiato da
        /// allora. Con l'impostazione a zero non si cancella niente.
        /// </summary>
        private async Task ApplyRetentionAsync(MachineBackupOwnerType ownerType, int ownerId, CancellationToken cancellationToken)
        {
            var keep = (await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken))?.MachineBackupKeepVersions ?? 0;
            if (keep <= 0)
                return;

            var versioni = await FilterByOwner(ownerType, ownerId)
                .OrderByDescending(x => x.Version)
                .ToListAsync(cancellationToken);

            if (versioni.Count <= keep)
                return;

            var primaVersione = versioni[^1].Id;
            var daButtare = versioni
                .Skip(keep)
                .Where(x => x.Id != primaVersione)
                .ToList();

            if (daButtare.Count == 0)
                return;

            _context.MachineBackups.RemoveRange(daButtare);
            await _context.SaveChangesAsync(cancellationToken);

            // I file dopo il salvataggio: se qui qualcosa va storto restano dei file orfani, che
            // costano spazio. Nell'ordine inverso resterebbero righe che puntano a file spariti, e
            // un backup che non si scarica piu' e' peggio.
            foreach (var backup in daButtare)
                _archive.Delete(backup.Id, backup.FileName);
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
