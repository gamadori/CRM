using System;

namespace CRM.Shared.Models
{
    /// <summary>
    /// DTO per ricevere firma con nome firmatario
    /// </summary>
    public class SignatureData
    {
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO per firma in attesa di verifica OTP
    /// </summary>
    public class SignaturePendingData
    {
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;

        /// <summary>Email del firmatario: usata per l'invio dell'OTP e per risalire al recapito.</summary>
        public string SignerEmail { get; set; } = string.Empty;

        /// <summary>Cellulare del firmatario (preferito per l'OTP via SMS).</summary>
        public string SignerPhone { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risposta richiesta OTP
    /// </summary>
    public class OtpRequestResponse
    {
        public bool Success { get; set; }
        public string ChallengeId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string SentTo { get; set; } = string.Empty;

        /// <summary>Canale usato per l'invio dell'OTP: "sms" oppure "email".</summary>
        public string Channel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Richiesta verifica OTP
    /// </summary>
    public class OtpVerifyRequest
    {
        public string ChallengeId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risposta verifica OTP
    /// </summary>
    public class OtpVerifyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO per salvare firma con email
    /// </summary>
    public class SignatureDataWithEmail
    {
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
        public string SignerEmail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Risposta salvataggio firma
    /// </summary>
    public class SignatureSaveResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool ConfirmationRequired { get; set; }
    }

    /// <summary>
    /// Richiesta rinvio email conferma
    /// </summary>
    public class ResendEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    // ===== Firma remota (link inviato al cliente) =====

    /// <summary>Esito dell'invio del link di firma remota.</summary>
    public class RemoteSignatureRequestResponse
    {
        public bool Success { get; set; }
        public string SentTo { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty; // "sms" | "email"
    }

    /// <summary>Info restituite alla pagina pubblica di firma per validare il token.</summary>
    public class RemoteSignatureInfoResponse
    {
        public bool Valid { get; set; }
        public int TicketId { get; set; }
        public string Company { get; set; } = string.Empty;
        public bool AlreadySigned { get; set; }
    }

    /// <summary>Firma inviata dal cliente dalla pagina pubblica.</summary>
    public class RemoteSignatureSubmit
    {
        public string Token { get; set; } = string.Empty;
        public int InterventionId { get; set; }
        public string Signature { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
    }
}
