using CRM.Server.Data;
using CRM.Server.Extensions;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CRM.Server.Services
{
    public class ExternalTicketApiService : IExternalTicketApiService
    {
        private const string Prefix = "crmtk";
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IArchiveService _archiveService;

        public ExternalTicketApiService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            IArchiveService archiveService)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _archiveService = archiveService;
        }

        public async Task<ExternalTicketResponse> CreateTicketAsync(ApiKey apiKey, ExternalTicketCreateRequest request)
        {
            // L'azienda su una chiave di questo ambito e' garantita da chi la crea; qui si
            // ricontrolla perche' il campo e' facoltativo sulla tabella condivisa, e un ticket
            // senza azienda sarebbe invisibile a tutti.
            var idCompany = apiKey.IdCompany
                ?? throw new InvalidOperationException("La chiave non e' associata a nessuna azienda.");

            var idType = await ResolveTicketTypeAsync(request);
            var article = await ResolveArticleAsync(idCompany, request);
            await ValidateTicketReferencesAsync(idCompany, request);

            var now = DateTime.Now;

            // Quando ci si lavora. La data chiesta da fuori vale com'e', festiva o no: e' una
            // scelta di chi apre il ticket. Il ripiego invece non puo' cadere in un giorno in cui
            // non c'e' nessuno, e da qui parte anche il calcolo della scadenza.
            var workDate = request.Date ?? now.NextBusinessDay();

            var state = await _context.TicketStates.FirstOrDefaultAsync(x => x.State == (int)eTicketStates.Created);
            var ownerUserId = await ResolveOwnerUserIdAsync();
            var ticket = new Ticket
            {
                IdCompany = idCompany,
                IdType = idType,
                IdArticle = article?.Id ?? request.IdArticle,
                IdProduct = request.IdProduct ?? article?.IdProduct,
                IdContact = request.IdContact,
                IdUserOpened = ownerUserId,
                IdState = state?.Id,
                Priority = (int)request.Priority,
                Description = BuildDescription(request),
                DateOpened = now,
                Date = workDate,
                DateEnd = request.DateEnd,
                DateExpired = await CalculateExpirationDateAsync(idType, workDate),
                Numero = string.Empty,
                CloseDescription = string.Empty,
                CloseNote = string.Empty,
                Support = (int)TypesSupport.Web,
                Progress = 0,
                Closed = false
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            return (await GetTicketAsync(apiKey, ticket.Id))!;
        }

        public async Task<ExternalTicketResponse?> GetTicketAsync(ApiKey apiKey, int id)
        {
            return await QueryTickets(apiKey)
                .Where(x => x.Id == id)
                .Select(x => ToResponse(x))
                .FirstOrDefaultAsync();
        }

        public async Task<List<ExternalTicketResponse>> GetTicketsAsync(ApiKey apiKey, bool includeClosed, int skip, int top)
        {
            top = Math.Clamp(top, 1, 100);
            skip = Math.Max(skip, 0);

            var query = QueryTickets(apiKey);
            if (!includeClosed)
            {
                query = query.Where(x => !x.Closed);
            }

            return await query
                .OrderByDescending(x => x.DateOpened)
                .Skip(skip)
                .Take(top)
                .Select(x => ToResponse(x))
                .ToListAsync();
        }

        // Lo stesso meccanismo degli allegati caricati a mano e di quelli ricevuti via email:
        // un Attachment di tipo Ticket con dentro il file, salvato nell'archivio. Il
        // proprietario e' chi ha aperto il ticket, cioe' l'utente di servizio dei ticket
        // esterni: la chiave API non e' una persona.
        public async Task<ExternalTicketAttachmentResponse?> AttachFileAsync(ApiKey apiKey, int idTicket, string fileName, string contentType, byte[] content, string? description)
        {
            var ticket = await QueryTickets(apiKey).FirstOrDefaultAsync(x => x.Id == idTicket);
            if (ticket == null)
            {
                return null;
            }

            var name = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Nome del file mancante.");
            }

            var ext = Path.GetExtension(name);
            var attachment = new Attachment
            {
                IdParent = ticket.Id,
                AttchmentType = AttachmentTypes.Ticket,
                Name = Truncate(Path.GetFileNameWithoutExtension(name), 100),
                Description = Truncate(description ?? string.Empty, 100),
                CreatedOn = DateTime.Now,
                IdUser = ticket.IdUserOpened ?? await ResolveOwnerUserIdAsync(),
                Visibility = AttachmentVisibilities.Public
            };
            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            var file = new AttachmentFile
            {
                IdAttachment = attachment.Id,
                Name = name,
                ContentType = Truncate(string.IsNullOrWhiteSpace(contentType) ? ext : contentType, 100),
                FileType = ext,
                Size = content.LongLength
            };
            _context.AttachmentFiles.Add(file);
            await _context.SaveChangesAsync();

            _archiveService.TypeArchive = ArchiveTypes.Attachments;
            if (!_archiveService.SaveAttachments(file.Id, ext, content))
            {
                throw new IOException("Salvataggio del file nell'archivio non riuscito.");
            }

            return new ExternalTicketAttachmentResponse
            {
                IdTicket = ticket.Id,
                IdAttachment = attachment.Id,
                IdFile = file.Id,
                FileName = name,
                Size = content.LongLength
            };
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value.Substring(0, max);

        private IQueryable<Ticket> QueryTickets(ApiKey apiKey)
        {
            return _context.Tickets
                .AsNoTracking()
                .Include(x => x.Company)
                .Include(x => x.State)
                .Where(x => x.IdCompany == apiKey.IdCompany);
        }

        // Il tipo lo sceglie il CRM quando il chiamante non lo indica: prima la configurazione,
        // poi il primo tipo aperto ai clienti. Un tipo indicato deve esistere.
        private async Task<int> ResolveTicketTypeAsync(ExternalTicketCreateRequest request)
        {
            if (request.IdType is int requested)
            {
                if (!await _context.TicketTypes.AnyAsync(x => x.Id == requested))
                {
                    throw new InvalidOperationException("Tipo ticket non trovato.");
                }

                return requested;
            }

            if (int.TryParse(_configuration["ExternalTickets:DefaultTicketTypeId"], out var configured)
                && await _context.TicketTypes.AnyAsync(x => x.Id == configured))
            {
                return configured;
            }

            var customerType = await _context.TicketTypes
                .Where(x => x.CustomerEnabled)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            return customerType
                ?? throw new InvalidOperationException("Nessun tipo ticket predefinito per le richieste esterne.");
        }

        // Dalla matricola all'articolo, solo fra quelli dell'azienda della chiave: una
        // matricola sconosciuta e' un errore, non un ticket senza macchina.
        private async Task<Article?> ResolveArticleAsync(int idCompany, ExternalTicketCreateRequest request)
        {
            if (request.IdArticle.HasValue || string.IsNullOrWhiteSpace(request.SerialNumber))
            {
                return null;
            }

            var serialNumber = request.SerialNumber.Trim();
            var article = await _context.Articles
                .AsNoTracking()
                .Where(x => x.IdCompany == idCompany && x.SerialNumber == serialNumber)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            return article
                ?? throw new InvalidOperationException("Matricola non trovata per la company associata alla API key.");
        }

        private async Task ValidateTicketReferencesAsync(int idCompany, ExternalTicketCreateRequest request)
        {
            if (request.IdContact.HasValue)
            {
                var contactExists = await _context.Contacts.AnyAsync(x => x.Id == request.IdContact.Value && x.IdCompany == idCompany);
                if (!contactExists)
                {
                    throw new InvalidOperationException("Contatto non trovato per la company associata alla API key.");
                }
            }

            if (request.IdArticle.HasValue)
            {
                var articleExists = await _context.Articles.AnyAsync(x => x.Id == request.IdArticle.Value && x.IdCompany == idCompany);
                if (!articleExists)
                {
                    throw new InvalidOperationException("Articolo non trovato per la company associata alla API key.");
                }
            }

        }

        private async Task<string> ResolveOwnerUserIdAsync()
        {
            var configuredUserId = _configuration["ExternalTickets:DefaultOwnerUserId"];
            if (!string.IsNullOrWhiteSpace(configuredUserId) &&
                await _context.Users.AnyAsync(x => x.Id == configuredUserId))
            {
                return configuredUserId;
            }

            var admins = await _userManager.GetUsersInRoleAsync(eRoles.Admin.ToString());
            var admin = admins.OrderBy(x => x.Id).FirstOrDefault();
            if (admin != null)
            {
                return admin.Id;
            }

            var firstUser = await _context.Users.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(firstUser))
            {
                return firstUser;
            }

            throw new InvalidOperationException("Nessun utente disponibile per aprire il ticket esterno.");
        }

        private async Task<DateTime?> CalculateExpirationDateAsync(int idType, DateTime date)
        {
            var days = await _context.TicketTypes
                .Where(x => x.Id == idType)
                .Select(x => x.ExpiredDate)
                .FirstOrDefaultAsync();

            if (days <= 0)
            {
                days = await _context.GlobalSettings
                    .Select(x => x.TicketDaysExpired)
                    .FirstOrDefaultAsync();
            }

            // Giorni LAVORATIVI, come ovunque nel CRM: sabato, domenica e i festivi italiani non
            // contano. Qui si contavano solari, quindi un ticket aperto da fuori nasceva con una
            // scadenza che il primo caricamento di elenco gli cambiava sotto - la stessa data
            // raccontata in due modi a seconda di chi la guardava.
            return days > 0 ? date.AddWorkdays(days) : null;
        }

        private static string BuildDescription(ExternalTicketCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ExternalReference))
            {
                return request.Description.Trim();
            }

            return $"{request.Description.Trim()}\n\nExternal reference: {request.ExternalReference.Trim()}";
        }

        private static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static ExternalTicketResponse ToResponse(Ticket ticket)
        {
            return new ExternalTicketResponse
            {
                Id = ticket.Id,
                Numero = ticket.Numero,
                IdCompany = ticket.IdCompany,
                Company = ticket.Company?.RagioneSociale,
                IdType = ticket.IdType,
                IdState = ticket.IdState,
                State = ticket.State?.Description,
                StateColor = ticket.State?.Color,
                Progress = ticket.Progress,
                Closed = ticket.Closed,
                DateOpened = ticket.DateOpened,
                Date = ticket.Date,
                DateEnd = ticket.DateEnd,
                DateExpired = ticket.DateExpired,
                DateClosed = ticket.DateClosed,
                Description = ticket.Description,
                OperationalSummary = ticket.OperationalSummary,
                CloseDescription = ticket.CloseDescription
            };
        }
    }

}
