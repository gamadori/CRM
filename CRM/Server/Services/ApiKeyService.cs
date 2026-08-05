using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CRM.Server.Services
{
    /// <summary>
    /// Generazione e verifica delle chiavi API, per tutti gli ambiti.
    /// <para>
    /// Lo schema e' quello che le tre tabelle precedenti gia' condividevano - sigla, 32 byte
    /// casuali, impronta SHA-256, prefisso di 12 caratteri - conservato tale e quale proprio
    /// perche' le chiavi gia' distribuite continuassero a funzionare dopo l'unificazione.
    /// </para>
    /// </summary>
    public class ApiKeyService : IApiKeyService
    {
        private readonly ApplicationDbContext _context;

        public ApiKeyService(ApplicationDbContext context) => _context = context;

        /// <summary>La sigla dice l'ambito a colpo d'occhio, anche fuori dal CRM.</summary>
        private static string PrefixFor(ApiKeyScope scope) => scope switch
        {
            ApiKeyScope.MachineBackup => "crmbk",
            ApiKeyScope.ExternalTicket => "crmtk",
            _ => "crmfd"
        };

        public async Task<List<ApiKeyDTO>> GetAsync(ApiKeyScope? scope = null)
        {
            var query = _context.ApiKeys.AsNoTracking()
                .Include(x => x.Company)
                .Include(x => x.User)
                .AsQueryable();

            if (scope != null)
                query = query.Where(x => x.Scope == scope);

            return await query
                .OrderBy(x => x.Scope)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => ToDto(x))
                .ToListAsync();
        }

        public async Task<ApiKeyCreateResponse> CreateAsync(ApiKeyCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("Il nome della chiave e' obbligatorio.");

            // Ogni ambito ha il suo intestatario obbligatorio: e' la regola che impedisce di
            // creare una chiave "fiera" senza persona, cioe' lead che nascono di nessuno.
            switch (request.Scope)
            {
                case ApiKeyScope.ExternalTicket:
                    if (request.IdCompany == null || !await _context.Companies.AnyAsync(x => x.Id == request.IdCompany))
                        throw new InvalidOperationException("Serve un'azienda esistente per una chiave dei ticket esterni.");
                    request.IdUser = null;
                    break;

                case ApiKeyScope.Field:
                    if (string.IsNullOrWhiteSpace(request.IdUser) || !await _context.Users.AnyAsync(x => x.Id == request.IdUser))
                        throw new InvalidOperationException("Serve una persona esistente per una chiave dell'app fiera.");
                    request.IdCompany = null;
                    break;

                default:
                    // Il backup non ha intestatario: eventuali riferimenti si scartano invece di
                    // restare a suggerire un legame che nessuno controlla.
                    request.IdCompany = null;
                    request.IdUser = null;
                    break;
            }

            var plainTextKey = $"{PrefixFor(request.Scope)}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", string.Empty).Replace("/", string.Empty).Replace("=", string.Empty)}";

            var key = new ApiKey
            {
                Scope = request.Scope,
                Name = request.Name.Trim(),
                KeyHash = Hash(plainTextKey),
                KeyPrefix = plainTextKey.Length > 12 ? plainTextKey[..12] : plainTextKey,
                Permission = request.Scope == ApiKeyScope.MachineBackup ? request.Permission : ApiKeyPermission.ReadWrite,
                IdCompany = request.IdCompany,
                IdUser = request.IdUser,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                Notes = request.Notes?.Trim()
            };

            _context.ApiKeys.Add(key);
            await _context.SaveChangesAsync();

            var saved = await _context.ApiKeys.AsNoTracking()
                .Include(x => x.Company)
                .Include(x => x.User)
                .FirstAsync(x => x.Id == key.Id);

            return new ApiKeyCreateResponse { ApiKey = ToDto(saved), PlainTextKey = plainTextKey };
        }

        /// <summary>
        /// Revoca invece di cancellare: la riga resta a dire che quella chiave e' esistita, a chi
        /// era intestata e quando e' stata usata l'ultima volta. Su un dispositivo smarrito e'
        /// l'unica traccia che si ha.
        /// </summary>
        public async Task<bool> RevokeAsync(int id)
        {
            var key = await _context.ApiKeys.FirstOrDefaultAsync(x => x.Id == id);
            if (key == null)
                return false;

            key.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ApiKey?> ValidateAsync(string? plainTextKey, ApiKeyScope scope, ApiKeyPermission? required = null)
        {
            if (string.IsNullOrWhiteSpace(plainTextKey))
                return null;

            var hash = Hash(plainTextKey.Trim());

            // L'ambito entra nella ricerca, non in un controllo dopo: cosi' una chiave di un altro
            // ambito e' indistinguibile da una inesistente, e non si puo' dedurre nulla provandola.
            var key = await _context.ApiKeys
                .Include(x => x.Company)
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.KeyHash == hash && x.Scope == scope);

            if (key == null || !key.IsActive)
                return null;

            if (key.ExpiresAt != null && key.ExpiresAt.Value < DateTime.UtcNow)
                return null;

            if (required == ApiKeyPermission.ReadWrite && key.Permission != ApiKeyPermission.ReadWrite)
                return null;

            // L'ultimo utilizzo si aggiorna al massimo una volta al minuto: un'app in fiera manda
            // una richiesta per biglietto, e una scrittura ogni volta trasformerebbe una tabella di
            // configurazione in una tabella di traffico.
            if (key.LastUsedAt == null || (DateTime.UtcNow - key.LastUsedAt.Value).TotalMinutes >= 1)
            {
                key.LastUsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return key;
        }

        private static ApiKeyDTO ToDto(ApiKey x) => new()
        {
            Id = x.Id,
            Scope = x.Scope,
            Name = x.Name,
            KeyPrefix = x.KeyPrefix,
            Permission = x.Permission,
            IdCompany = x.IdCompany,
            CompanyName = x.Company != null ? x.Company.RagioneSociale : null,
            IdUser = x.IdUser,
            UserName = x.User != null ? x.User.NameComplete : null,
            IsActive = x.IsActive,
            CreatedAt = x.CreatedAt,
            ExpiresAt = x.ExpiresAt,
            LastUsedAt = x.LastUsedAt,
            Notes = x.Notes
        };

        private static string Hash(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
