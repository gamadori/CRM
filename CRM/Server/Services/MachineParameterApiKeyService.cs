using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CRM.Server.Services
{
    public class MachineParameterApiKeyService : IMachineParameterApiKeyService
    {
        private const string Prefix = "crmbk";
        private readonly ApplicationDbContext _context;

        public MachineParameterApiKeyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<MachineParameterApiKeyDTO>> GetListAsync()
        {
            var keys = await _context.MachineParameterApiKeys
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return keys.Select(x => x.ToDto()).ToList();
        }

        public async Task<MachineParameterApiKeyCreateResponse> CreateAsync(MachineParameterApiKeyCreateRequest request)
        {
            var plainTextKey = $"{Prefix}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "")}";
            var key = new MachineParameterApiKey
            {
                Name = request.Name?.Trim(),
                KeyHash = Hash(plainTextKey),
                KeyPrefix = plainTextKey.Length > 12 ? plainTextKey.Substring(0, 12) : plainTextKey,
                Permission = request.Permission,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                Notes = request.Notes?.Trim()
            };

            _context.MachineParameterApiKeys.Add(key);
            await _context.SaveChangesAsync();

            return new MachineParameterApiKeyCreateResponse
            {
                ApiKey = key.ToDto(),
                PlainTextKey = plainTextKey
            };
        }

        public async Task<bool> RevokeAsync(int id)
        {
            var key = await _context.MachineParameterApiKeys.FirstOrDefaultAsync(x => x.Id == id);
            if (key == null)
            {
                return false;
            }

            key.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<MachineParameterApiKey?> ValidateAsync(string? plainTextKey, MachineParameterApiKeyPermission requiredPermission)
        {
            if (string.IsNullOrWhiteSpace(plainTextKey))
            {
                return null;
            }

            var hash = Hash(plainTextKey.Trim());
            var key = await _context.MachineParameterApiKeys.FirstOrDefaultAsync(x => x.KeyHash == hash);
            if (key == null || !key.IsActive)
            {
                return null;
            }

            if (key.ExpiresAt != null && key.ExpiresAt.Value < DateTime.UtcNow)
            {
                return null;
            }

            if (requiredPermission == MachineParameterApiKeyPermission.ReadWrite &&
                key.Permission != MachineParameterApiKeyPermission.ReadWrite)
            {
                return null;
            }

            key.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return key;
        }

        private static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }
    }

    internal static class MachineParameterApiKeyMapping
    {
        public static MachineParameterApiKeyDTO ToDto(this MachineParameterApiKey key)
        {
            return new MachineParameterApiKeyDTO
            {
                Id = key.Id,
                Name = key.Name,
                KeyPrefix = key.KeyPrefix,
                Permission = key.Permission,
                IsActive = key.IsActive,
                CreatedAt = key.CreatedAt,
                ExpiresAt = key.ExpiresAt,
                LastUsedAt = key.LastUsedAt,
                Notes = key.Notes
            };
        }
    }
}
