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

        public async Task<List<ExternalTicketApiKeyDTO>> GetApiKeysAsync()
        {
            var keys = await _context.ExternalTicketApiKeys
                .AsNoTracking()
                .Include(x => x.Company)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return keys.Select(x => x.ToDto()).ToList();
        }

        public async Task<ExternalTicketApiKeyCreateResponse> CreateApiKeyAsync(ExternalTicketApiKeyCreateRequest request)
        {
            var companyExists = await _context.Companies.AnyAsync(x => x.Id == request.IdCompany);
            if (!companyExists)
            {
                throw new InvalidOperationException("Company non trovata.");
            }

            var plainTextKey = $"{Prefix}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "")}";
            var key = new ExternalTicketApiKey
            {
                Name = request.Name.Trim(),
                KeyHash = Hash(plainTextKey),
                KeyPrefix = plainTextKey.Length > 12 ? plainTextKey.Substring(0, 12) : plainTextKey,
                IdCompany = request.IdCompany,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                Notes = request.Notes?.Trim()
            };

            _context.ExternalTicketApiKeys.Add(key);
            await _context.SaveChangesAsync();

            key.Company = await _context.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == key.IdCompany);

            return new ExternalTicketApiKeyCreateResponse
            {
                ApiKey = key.ToDto(),
                PlainTextKey = plainTextKey
            };
        }

        public async Task<bool> RevokeApiKeyAsync(int id)
        {
            var key = await _context.ExternalTicketApiKeys.FirstOrDefaultAsync(x => x.Id == id);
            if (key == null)
            {
                return false;
            }

            key.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ExternalTicketApiKey?> ValidateApiKeyAsync(string? plainTextKey)
        {
            if (string.IsNullOrWhiteSpace(plainTextKey))
            {
                return null;
            }

            var hash = Hash(plainTextKey.Trim());
            var key = await _context.ExternalTicketApiKeys
                .Include(x => x.Company)
                .FirstOrDefaultAsync(x => x.KeyHash == hash);

            if (key == null || !key.IsActive)
            {
                return null;
            }

            if (key.ExpiresAt != null && key.ExpiresAt.Value < DateTime.UtcNow)
            {
                return null;
            }

            key.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return key;
        }

        public async Task<ExternalTicketResponse> CreateTicketAsync(ExternalTicketApiKey apiKey, ExternalTicketCreateRequest request)
        {
            await ValidateTicketReferencesAsync(apiKey.IdCompany, request);

            var now = DateTime.Now;
            var state = await _context.TicketStates.FirstOrDefaultAsync(x => x.State == (int)eTicketStates.Created);
            var ownerUserId = await ResolveOwnerUserIdAsync();
            var ticket = new Ticket
            {
                IdCompany = apiKey.IdCompany,
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

        public async Task<ExternalTicketResponse?> GetTicketAsync(ExternalTicketApiKey apiKey, int id)
        {
            return await QueryTickets(apiKey)
                .Where(x => x.Id == id)
                .Select(x => ToResponse(x))
                .FirstOrDefaultAsync();
        }

        public async Task<List<ExternalTicketResponse>> GetTicketsAsync(ExternalTicketApiKey apiKey, bool includeClosed, int skip, int top)
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

        private IQueryable<Ticket> QueryTickets(ExternalTicketApiKey apiKey)
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

    internal static class ExternalTicketApiKeyMapping
    {
        public static ExternalTicketApiKeyDTO ToDto(this ExternalTicketApiKey key)
        {
            return new ExternalTicketApiKeyDTO
            {
                Id = key.Id,
                Name = key.Name,
                KeyPrefix = key.KeyPrefix,
                IdCompany = key.IdCompany,
                Company = key.Company?.RagioneSociale,
                IsActive = key.IsActive,
                CreatedAt = key.CreatedAt,
                ExpiresAt = key.ExpiresAt,
                LastUsedAt = key.LastUsedAt,
                Notes = key.Notes
            };
        }
    }
}
