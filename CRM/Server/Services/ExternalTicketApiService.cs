using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
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

        public ExternalTicketApiService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        public async Task<ExternalTicketResponse> CreateTicketAsync(ApiKey apiKey, ExternalTicketCreateRequest request)
        {
            // L'azienda su una chiave di questo ambito e' garantita da chi la crea; qui si
            // ricontrolla perche' il campo e' facoltativo sulla tabella condivisa, e un ticket
            // senza azienda sarebbe invisibile a tutti.
            var idCompany = apiKey.IdCompany
                ?? throw new InvalidOperationException("La chiave non e' associata a nessuna azienda.");

            await ValidateTicketReferencesAsync(idCompany, request);

            var now = DateTime.Now;
            var state = await _context.TicketStates.FirstOrDefaultAsync(x => x.State == (int)eTicketStates.Created);
            var ownerUserId = await ResolveOwnerUserIdAsync();
            var ticket = new Ticket
            {
                IdCompany = idCompany,
                IdType = request.IdType,
                IdArticle = request.IdArticle,
                IdProduct = request.IdProduct,
                IdContact = request.IdContact,
                IdUserOpened = ownerUserId,
                IdState = state?.Id,
                Priority = (int)request.Priority,
                Description = BuildDescription(request),
                DateOpened = now,
                Date = request.Date ?? now,
                DateEnd = request.DateEnd,
                DateExpired = request.DateExpired ?? await CalculateExpirationDateAsync(request.IdType, request.Date ?? now),
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

        private IQueryable<Ticket> QueryTickets(ApiKey apiKey)
        {
            return _context.Tickets
                .AsNoTracking()
                .Include(x => x.Company)
                .Include(x => x.State)
                .Where(x => x.IdCompany == apiKey.IdCompany);
        }

        private async Task ValidateTicketReferencesAsync(int idCompany, ExternalTicketCreateRequest request)
        {
            var typeExists = await _context.TicketTypes.AnyAsync(x => x.Id == request.IdType);
            if (!typeExists)
            {
                throw new InvalidOperationException("Tipo ticket non trovato.");
            }

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

            return days > 0 ? date.AddDays(days) : null;
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
