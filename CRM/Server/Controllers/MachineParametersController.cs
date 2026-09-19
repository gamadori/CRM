using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Quello che una macchina puo' fare da sola: vedere le proprie matricole, scaricare l'ultimo
    /// backup, caricarne uno nuovo.
    /// <para>
    /// <see cref="AllowAnonymous"/> perche' l'autenticazione non e' quella del CRM: la macchina non
    /// fa login, si presenta con una chiave nell'intestazione <c>X-Api-Key</c> e ogni azione la
    /// verifica prima di fare qualsiasi cosa. Stesso schema di <see cref="ExternalTicketsController"/>.
    /// </para>
    /// <para>
    /// <b>La chiave delimita cio' che esiste.</b> Ogni chiave e' intestata a una ditta, e da qui si
    /// vedono soltanto le macchine di quella ditta: una chiave senza ditta non apre niente. Prima il
    /// perimetro non c'era - l'elenco rispondeva con le macchine di tutti i clienti, e scaricare o
    /// caricare un backup bastava conoscerne il numero. Finche' nessuno ha emesso chiavi di questo
    /// ambito non e' successo nulla; la prima chiave installata da un cliente avrebbe aperto a quel
    /// cliente i backup di tutti gli altri.
    /// </para>
    /// <para>
    /// <b>Si parla per matricola, non per id.</b> Una macchina conosce il numero stampato sulla sua
    /// targhetta, non il numero di riga che quella macchina ha nel nostro database. Chiedendo l'id
    /// interno, per caricare il proprio backup una macchina doveva prima scaricare l'elenco di tutte
    /// le altre e cercarsi dentro.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    [Route("api/machine")]
    [ApiController]
    public class MachineParametersController : ControllerBase
    {
        private const string ApiKeyHeader = "X-Api-Key";
        private readonly ApplicationDbContext _context;
        private readonly IApiKeyService _apiKeys;
        private readonly IMachineBackupsService _backups;
        private readonly IMachineStatusService _status;
        private readonly IRemoteSupportService _remoteSupport;

        public MachineParametersController(
            ApplicationDbContext context,
            IApiKeyService apiKeys,
            IMachineBackupsService backups,
            IMachineStatusService status,
            IRemoteSupportService remoteSupport)
        {
            _context = context;
            _apiKeys = apiKeys;
            _backups = backups;
            _status = status;
            _remoteSupport = remoteSupport;
        }

        /// <summary>
        /// Il pannello si auto-registra per l'assistenza remota: chiede lui il codice
        /// di abbinamento con la sua chiave macchina e la sua matricola, cosi' nessuno
        /// deve digitare a mano sul touch una stringa di 43 caratteri. Poi il pannello
        /// lo usa subito per aprire il tunnel. Richiede una chiave in scrittura.
        /// </summary>
        [HttpPost("articles/{serialNumber}/remote-support/code")]
        public async Task<ActionResult<RemoteSupportCodeDTO>> IssueRemoteSupportCode(string serialNumber)
        {
            var (apiKey, article, error) = await ResolveArticleAsync(serialNumber, ApiKeyPermission.ReadWrite);
            if (error != null)
                return error;

            try
            {
                var actor = $"machine:{article!.SerialNumber}";
                return Ok(await _remoteSupport.IssueCodeAsync(article.Id, actor, HttpContext.RequestAborted));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("articles")]
        public async Task<ActionResult<List<MachineArticleDTO>>> GetArticles([FromQuery] MachineArticleListFilter filter)
        {
            var apiKey = await Authorize(ApiKeyPermission.ReadOnly);
            if (apiKey == null)
            {
                return Unauthorized();
            }

            var query = ArticlesOfKey(apiKey).Include(x => x.Product).Include(x => x.Company);
            var filtered = query.AsQueryable();

            if (filter.IdProduct != null) filtered = filtered.Where(x => x.IdProduct == filter.IdProduct);
            if (!string.IsNullOrWhiteSpace(filter.ProductCode)) filtered = filtered.Where(x => x.Product != null && x.Product.Code == filter.ProductCode);
            if (!string.IsNullOrWhiteSpace(filter.SerialNumber)) filtered = filtered.Where(x => x.SerialNumber.Contains(filter.SerialNumber));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                filtered = filtered.Where(x => x.SerialNumber.Contains(search) || x.Name.Contains(search) ||
                    (x.Product != null && (x.Product.Name.Contains(search) || x.Product.Code.Contains(search))));
            }

            var take = Math.Clamp(filter.Take.GetValueOrDefault(50), 1, 200);
            var articles = await filtered.OrderBy(x => x.Product.Name).ThenBy(x => x.SerialNumber)
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

        /// <summary>L'ultimo backup di questa macchina.</summary>
        [HttpGet("articles/{serialNumber}/backups/latest")]
        public async Task<ActionResult> GetLatestArticleBackup(string serialNumber)
        {
            var (_, article, error) = await ResolveArticleAsync(serialNumber, ApiKeyPermission.ReadOnly);
            if (error != null) return error;

            var backup = await _backups.GetLatestAsync(MachineBackupOwnerType.Article, article!.Id);
            return backup == null ? NotFound() : Ok(backup);
        }

        /// <summary>
        /// L'ultimo backup di riferimento del modello di questa macchina: la configurazione di
        /// fabbrica da cui ripartire. Si arriva anche qui dalla matricola, che e' l'unica cosa che
        /// la macchina sa di se stessa.
        /// </summary>
        [HttpGet("articles/{serialNumber}/model-backups/latest")]
        public async Task<ActionResult> GetLatestModelBackup(string serialNumber)
        {
            var (_, article, error) = await ResolveArticleAsync(serialNumber, ApiKeyPermission.ReadOnly);
            if (error != null) return error;

            if (article!.IdProduct == null)
                return NotFound();

            var backup = await _backups.GetLatestAsync(MachineBackupOwnerType.Product, article.IdProduct.Value);
            return backup == null ? NotFound() : Ok(backup);
        }

        [HttpGet("backups/{id:int}/file")]
        public async Task<IActionResult> Download(int id)
        {
            var apiKey = await Authorize(ApiKeyPermission.ReadOnly);
            if (apiKey == null) return Unauthorized();

            // Il numero di un backup si conosce solo perche' l'ha detto questa stessa API, ma non
            // basta averlo: si scarica un backup solo se e' di una macchina della propria ditta, o
            // il riferimento di un modello che quella ditta possiede.
            if (!await CanReachBackupAsync(apiKey, id))
                return NotFound();

            var file = await _backups.DownloadAsync(id);
            return file == null ? NotFound() : File(file.Value.Content, file.Value.ContentType, file.Value.FileName, true);
        }

        [RequestSizeLimit(536_870_912)]
        [HttpPost("articles/{serialNumber}/backups")]
        public async Task<ActionResult<MachineBackupDTO>> UploadArticleBackup(
            string serialNumber,
            IFormFile file,
            [FromForm] string? description,
            [FromForm] string? externalReference,
            CancellationToken cancellationToken)
        {
            var (apiKey, article, error) = await ResolveArticleAsync(serialNumber, ApiKeyPermission.ReadWrite);
            if (error != null) return error;

            if (file == null || file.Length == 0) return BadRequest("Select a non-empty backup file.");

            await using var stream = file.OpenReadStream();
            var created = await _backups.UploadAsync(
                MachineBackupOwnerType.Article,
                article!.Id,
                file.FileName,
                file.ContentType,
                stream,
                new MachineBackupUploadMetadata { Description = description, ExternalReference = externalReference },
                MachineBackupSource.MachineApi,
                $"api-key:{apiKey!.Id}",
                cancellationToken);
            return Ok(created);
        }

        /// <summary>
        /// La fotografia della macchina: componenti con le loro versioni, e i totalizzatori del
        /// giorno. Il CRM confronta con l'ultima ricevuta e registra da se' i cambi di versione.
        /// <para>
        /// Si manda l'elenco <b>completo</b> dei componenti, non solo quelli cambiati: e' cio' che
        /// permette di recuperare un aggiornamento fatto mentre la rete era giu'.
        /// </para>
        /// </summary>
        [HttpPost("articles/{serialNumber}/status")]
        public async Task<ActionResult<MachineStatusResponse>> PostStatus(
            string serialNumber,
            MachineStatusRequest request,
            CancellationToken cancellationToken)
        {
            var (apiKey, article, error) = await ResolveArticleAsync(serialNumber, ApiKeyPermission.ReadWrite);
            if (error != null) return error;

            if (request == null || (request.Components == null && request.Counters == null))
                return BadRequest("Send at least one of: components, counters.");

            var esito = await _status.ApplyAsync(article!, request, $"api-key:{apiKey!.Id}", cancellationToken);
            return Ok(esito);
        }

        /// <summary>
        /// Dalla matricola alla macchina, dentro il perimetro della chiave. Una matricola di un'altra
        /// ditta risponde "non trovata" come una inesistente: dire "esiste ma non e' tua" sarebbe
        /// gia' un'informazione sul parco macchine altrui.
        /// </summary>
        private async Task<(ApiKey? Key, Article? Article, ActionResult? Error)> ResolveArticleAsync(
            string serialNumber,
            ApiKeyPermission permission)
        {
            var apiKey = await Authorize(permission);
            if (apiKey == null)
                return (null, null, Unauthorized());

            var serial = serialNumber?.Trim();
            if (string.IsNullOrWhiteSpace(serial))
                return (null, null, BadRequest("Serial number is required."));

            var matches = await ArticlesOfKey(apiKey)
                .Where(x => x.SerialNumber == serial)
                .Take(2)
                .ToListAsync();

            if (matches.Count == 0)
                return (null, null, NotFound());

            // Sulla matricola non c'e' un vincolo di unicita' in archivio. Se ne saltano fuori due,
            // scegliere la prima vorrebbe dire scrivere il backup di una macchina sopra un'altra.
            if (matches.Count > 1)
                return (null, null, Conflict($"Piu' di una macchina con matricola {serial}."));

            return (apiKey, matches[0], null);
        }

        /// <summary>Le macchine che questa chiave puo' vedere: quelle della sua ditta, e basta.</summary>
        private IQueryable<Article> ArticlesOfKey(ApiKey apiKey)
            => _context.Articles.AsNoTracking().Where(x => x.IdCompany == apiKey.IdCompany!.Value);

        /// <summary>
        /// Se il backup e' raggiungibile da questa chiave: o e' di una sua macchina, o e' il
        /// riferimento di un modello di cui la ditta possiede almeno una macchina.
        /// </summary>
        private async Task<bool> CanReachBackupAsync(ApiKey apiKey, int idBackup)
        {
            var backup = await _context.MachineBackups.AsNoTracking()
                .Where(x => x.Id == idBackup)
                .Select(x => new { x.OwnerType, x.IdArticle, x.IdProduct })
                .FirstOrDefaultAsync();

            if (backup == null)
                return false;

            if (backup.OwnerType == MachineBackupOwnerType.Article)
                return backup.IdArticle != null
                    && await ArticlesOfKey(apiKey).AnyAsync(x => x.Id == backup.IdArticle.Value);

            return backup.IdProduct != null
                && await ArticlesOfKey(apiKey).AnyAsync(x => x.IdProduct == backup.IdProduct.Value);
        }

        /// <summary>
        /// La chiave, se vale per questo ambito e porta con se' una ditta. Senza ditta non si
        /// prosegue: sarebbe una chiave che vede il parco macchine di chiunque.
        /// </summary>
        private async Task<ApiKey?> Authorize(ApiKeyPermission permission)
        {
            var value = Request.Headers.TryGetValue(ApiKeyHeader, out var values) ? values.FirstOrDefault() : null;
            var key = await _apiKeys.ValidateAsync(value, ApiKeyScope.Machine, permission);

            return key?.IdCompany == null ? null : key;
        }
    }
}
