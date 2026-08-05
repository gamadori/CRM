using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Il ponte fra l'app di cattura biglietti e il CRM.
    /// <para>
    /// Regola tenuta ovunque qui dentro: quello che arriva dal campo <b>non si perde</b>. Un
    /// biglietto e' passato per una persona reale allo stand, e un errore lato server che lo
    /// scarta e' peggio di un dato incompleto salvato.
    /// </para>
    /// </summary>
    public class FieldApiService : IFieldApiService
    {
        /// <summary>
        /// Quante iniziative offrire all'app. Non tutto lo storico: sul telefono si sceglie da una
        /// tendina, e un elenco lungo si scorre peggio di quanto aiuti.
        /// </summary>
        private const int MaxInitiatives = 30;

        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        private readonly IBusinessCardAnalyzer _cardAnalyzer;
        private readonly ILogEventService _logEventService;

        public FieldApiService(
            ApplicationDbContext context,
            IArchiveService archiveService,
            IBusinessCardAnalyzer cardAnalyzer,
            ILogEventService logEventService)
        {
            _context = context;
            _archiveService = archiveService;
            _cardAnalyzer = cardAnalyzer;
            _logEventService = logEventService;
        }

        // -----------------------------------------------------------------------------------
        // Uso dal campo
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Le iniziative che si possono scegliere dall'app: non annullate, non troppo vecchie,
        /// dalla piu' recente. Non solo quelle "in corso" perche' i biglietti si caricano anche
        /// il giorno dopo, a fiera gia' chiusa.
        /// </summary>
        public async Task<List<FieldInitiativeDTO>> GetInitiativesAsync()
        {
            var today = DateTime.Today;
            var floor = today.AddMonths(-2);

            var items = await _context.Initiatives
                .AsNoTracking()
                .Where(x => x.State != InitiativeState.Cancelled && x.DateTo >= floor)
                .OrderBy(x => x.DateFrom >= today ? 1 : 0)   // prima quelle in corso o appena finite
                .ThenByDescending(x => x.DateFrom)
                .Take(MaxInitiatives)
                .Select(x => new FieldInitiativeDTO
                {
                    Id = x.Id,
                    Name = x.Name,
                    Location = x.Location,
                    DateFrom = x.DateFrom,
                    DateTo = x.DateTo
                })
                .ToListAsync();

            foreach (var item in items)
                item.IsCurrent = item.DateFrom.Date <= today && item.DateTo.Date >= today;

            return items;
        }

        public async Task<FieldLeadResponse> CreateLeadAsync(
            ApiKey apiKey,
            FieldLeadRequest request,
            byte[]? photo,
            string? photoFileName)
        {
            try
            {
                // Idempotenza: se la stessa cattura arriva due volte - l'app ha rimandato perche'
                // la risposta si e' persa - si restituisce il lead gia' creato invece di un gemello.
                if (!string.IsNullOrWhiteSpace(request.ClientId))
                {
                    var existing = await _context.Leads
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.FieldClientId == request.ClientId);

                    if (existing != null)
                    {
                        return new FieldLeadResponse
                        {
                            Ok = true,
                            IdLead = existing.Id,
                            Duplicate = true,
                            Message = "Biglietto gia' ricevuto."
                        };
                    }
                }

                var initiative = request.IdInitiative > 0
                    ? await _context.Initiatives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.IdInitiative)
                    : null;

                if (request.IdInitiative > 0 && initiative == null)
                    return Fail("Iniziativa non trovata.");

                int? idAttachment = null;
                if (photo is { Length: > 0 })
                    idAttachment = await SavePhotoAsync(apiKey, photo, photoFileName);

                // OCR di recupero: chi ha raccolto il biglietto senza rete non ha potuto farlo sul
                // telefono. Se non riesce non e' un errore - la foto resta, i campi si completano
                // a mano nel triage della sera.
                if (request.AutoFillFromCard && photo is { Length: > 0 })
                    await FillFromCardAsync(request, photo, photoFileName);

                var capturedAt = request.CapturedAt ?? DateTime.Now;
                var lead = new Lead
                {
                    Name = BuildName(request, capturedAt),
                    CompanyName = Clean(request.CompanyName),
                    JobTitle = Clean(request.JobTitle),
                    Email = Clean(request.Email),
                    Phone = Clean(request.Phone),
                    Note = Clean(request.Note),
                    Score = Math.Clamp(request.Score, 0, 100),
                    Status = LeadStatus.New,
                    Source = LeadSource.Event,
                    IdInitiative = initiative?.Id,
                    IdBusinessCard = idAttachment,
                    IdUser = apiKey.IdUser,
                    FieldClientId = Clean(request.ClientId),

                    // La data e' quella dello STAND, non quella dell'invio: un biglietto puo'
                    // restare in coda giorni, e datarlo al rientro lo sposterebbe fuori dalla fiera.
                    CreatedAt = capturedAt
                };

                _context.Leads.Add(lead);
                await _context.SaveChangesAsync();

                return new FieldLeadResponse { Ok = true, IdLead = lead.Id, Message = "Biglietto registrato." };
            }
            catch (Exception ex)
            {
                _context.ChangeTracker.Clear();
                await _logEventService.RegisterAsync(nameof(FieldApiService), nameof(CreateLeadAsync), LogEvent.EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio del biglietto.");
            }
        }

        /// <summary>
        /// Salva la foto come allegato, con lo stesso schema dell'upload dal CRM (un contenitore
        /// per file, intestato a chi ha la chiave).
        /// </summary>
        private async Task<int> SavePhotoAsync(ApiKey apiKey, byte[] photo, string? fileName)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? "biglietto.jpg" : Path.GetFileName(fileName);

            var attachment = new Attachment
            {
                IdParent = 0,
                AttchmentType = AttachmentTypes.ExpenseReceipts,
                Name = $"Biglietto da visita {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                CreatedOn = DateTime.UtcNow,
                IdUser = apiKey.IdUser,
                CanEdit = true,
                CanDelete = true,
                Visibility = AttachmentVisibilities.Private
            };

            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            var file = new AttachmentFile
            {
                Name = name,
                Size = photo.Length,
                IdAttachment = attachment.Id,
                ContentType = GuessContentType(name)
            };

            _context.AttachmentFiles.Add(file);
            await _context.SaveChangesAsync();

            _archiveService.SaveAttachments(file.Id, Path.GetExtension(name), photo);
            return file.Id;
        }

        /// <summary>Riempie solo i campi rimasti vuoti: quello che ha scritto la persona vince sempre.</summary>
        private async Task FillFromCardAsync(FieldLeadRequest request, byte[] photo, string? fileName)
        {
            try
            {
                var result = await _cardAnalyzer.AnalyzeAsync(photo, fileName ?? "biglietto.jpg");
                if (!result.Success)
                    return;

                request.Name = Prefer(request.Name, result.FullName);
                request.CompanyName = Prefer(request.CompanyName, result.CompanyName);
                request.JobTitle = Prefer(request.JobTitle, result.JobTitle);
                request.Email = Prefer(request.Email, result.Email);
                request.Phone = Prefer(request.Phone, result.Phone);
            }
            catch (Exception ex)
            {
                // Non blocca: senza lettura il biglietto arriva comunque, con la sua foto.
                await _logEventService.RegisterAsync(nameof(FieldApiService), nameof(FillFromCardAsync), LogEvent.EventsTypes.Warning, ex);
            }
        }

        /// <summary>
        /// Un nome ci vuole (e' obbligatorio sull'entita'), ma allo stand puo' mancare: se c'e' la
        /// foto il contatto e' recuperabile, quindi si mette un segnaposto invece di rifiutare.
        /// E' lo stesso testo che il triage riconosce come "nome mancante".
        /// </summary>
        private static string BuildName(FieldLeadRequest request, DateTime capturedAt)
        {
            var candidate = new[] { request.Name, request.CompanyName, request.Email, request.Phone }
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return string.IsNullOrWhiteSpace(candidate)
                ? $"Biglietto delle {capturedAt:HH:mm}"
                : candidate.Trim();
        }

        private static string? Prefer(string? typed, string? read)
            => string.IsNullOrWhiteSpace(typed) ? Clean(read) : typed;

        private static string? Clean(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static FieldLeadResponse Fail(string message)
            => new() { Ok = false, Message = message };

        private static string GuessContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
