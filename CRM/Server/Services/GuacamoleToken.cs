using System;
using System.Linq;
using System.Security.Cryptography;

namespace CRM.Server.Services
{
    /// <summary>
    /// Codifica il token dell'estensione Apache guacamole-auth-json:
    /// HMAC-SHA256(JSON) || JSON, cifrato in AES-128-CBC con IV nullo, poi Base64.
    /// Va passato a Guacamole nel parametro di query <c>data</c>.
    /// </summary>
    internal static class GuacamoleToken
    {
        internal static bool IsValidKey(string? value) => value is { Length: 32 }
            && value.All(Uri.IsHexDigit)
            && value.Any(c => c != '0');

        internal static string Encode(byte[] json, string secret)
        {
            if (!IsValidKey(secret))
                throw new ArgumentException("La chiave Guacamole deve avere 32 caratteri esadecimali e non essere nulla.", nameof(secret));

            var key = Convert.FromHexString(secret);
            try
            {
                var signature = HMACSHA256.HashData(key, json);
                var signed = new byte[signature.Length + json.Length];
                signature.CopyTo(signed, 0);
                json.CopyTo(signed, signature.Length);
                using var aes = Aes.Create();
                aes.Key = key;
                return Convert.ToBase64String(aes.EncryptCbc(signed, new byte[16], PaddingMode.PKCS7));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
