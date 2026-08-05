namespace CRM.Shared.DTOs
{
    /// <summary>
    /// Quello che si e' riusciti a leggere da un biglietto da visita.
    /// <para>
    /// Ogni campo e' facoltativo perche' l'estrazione e' una COMODITA', non la fonte: la fonte e' la
    /// foto, che resta allegata al lead. Se l'analisi fallisce, o non e' configurata, o si e'
    /// offline, il biglietto e' comunque salvato e si legge dopo - non si perde niente.
    /// </para>
    /// </summary>
    public class BusinessCardExtractionResult
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public string? FullName { get; set; }

        public string? CompanyName { get; set; }

        public string? JobTitle { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Website { get; set; }

        /// <summary>Confidenza media dei campi letti (0-1). Null quando non e' determinabile.</summary>
        public float? AverageConfidence { get; set; }

        public long ProcessingTimeMs { get; set; }

        /// <summary>Vero se e' stato letto almeno un campo utile: sotto questa soglia si compila a mano.</summary>
        public bool HasAnyField =>
            !string.IsNullOrWhiteSpace(FullName)
            || !string.IsNullOrWhiteSpace(CompanyName)
            || !string.IsNullOrWhiteSpace(Email)
            || !string.IsNullOrWhiteSpace(Phone);
    }
}
