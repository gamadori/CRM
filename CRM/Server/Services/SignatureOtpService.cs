using System.Security.Cryptography;
using System.Text;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio per generazione e verifica OTP sicuri
    /// </summary>
    public interface ISignatureOtpService
    {
        /// <summary>
        /// Genera un OTP di 6 cifre criptograficamente sicuro
        /// </summary>
        string GenerateOtp();

        /// <summary>
        /// Crea hash HMAC-SHA256 dell'OTP + challengeId
        /// </summary>
        string HashOtp(string otp, string challengeId);

        /// <summary>
        /// Verifica se l'OTP fornito corrisponde all'hash
        /// </summary>
        bool VerifyOtp(string providedOtp, string storedHash, string challengeId);

        /// <summary>
        /// Genera un UUID univoco per challenge
        /// </summary>
        string GenerateChallengeId();
    }

    public class SignatureOtpService : ISignatureOtpService
    {
        private readonly IConfiguration _configuration;
        private readonly byte[] _secretKey;

        public SignatureOtpService(IConfiguration configuration)
        {
            _configuration = configuration;
            
            // Usa secret da appsettings o genera uno casuale (NON in produzione!)
            var secretConfig = _configuration["Security:OtpSecret"];
            if (string.IsNullOrEmpty(secretConfig))
            {
                // ?? ATTENZIONE: In produzione DEVE essere in appsettings/KeyVault!
                secretConfig = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            }
            
            _secretKey = Encoding.UTF8.GetBytes(secretConfig);
        }

        /// <summary>
        /// Genera OTP 6 cifre (100000-999999) con RNG sicuro
        /// </summary>
        public string GenerateOtp()
        {
            // Genera numero casuale criptograficamente sicuro
            var randomNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
            return randomNumber.ToString();
        }

        /// <summary>
        /// Hash HMAC-SHA256(secret, otp + challengeId)
        /// </summary>
        public string HashOtp(string otp, string challengeId)
        {
            if (string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(challengeId))
                throw new ArgumentException("OTP e ChallengeId sono obbligatori");

            using var hmac = new HMACSHA256(_secretKey);
            var dataToHash = Encoding.UTF8.GetBytes(otp + challengeId);
            var hash = hmac.ComputeHash(dataToHash);
            
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verifica OTP con timing-attack resistant comparison
        /// </summary>
        public bool VerifyOtp(string providedOtp, string storedHash, string challengeId)
        {
            if (string.IsNullOrWhiteSpace(providedOtp) || 
                string.IsNullOrWhiteSpace(storedHash) || 
                string.IsNullOrWhiteSpace(challengeId))
                return false;

            try
            {
                var computedHash = HashOtp(providedOtp, challengeId);
                
                // ? Timing-safe comparison
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(storedHash),
                    Convert.FromBase64String(computedHash)
                );
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Genera GUID univoco per challenge
        /// </summary>
        public string GenerateChallengeId()
        {
            return Guid.NewGuid().ToString("N"); // 32 caratteri senza trattini
        }
    }
}
