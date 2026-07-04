using System;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// DTO sintetico (pubblico) di un intervento tecnico, per letture leggere
    /// (es. assistente dati). Espone solo i campi essenziali, senza firme/OTP/estrazioni.
    /// </summary>
    public class TicketInterventionSummaryDTO
    {
        public int Id { get; set; }

        public int IdTicket { get; set; }

        public int SupportType { get; set; }

        public string Activities { get; set; } = string.Empty;

        public string? MountedParts { get; set; }

        public string? Note { get; set; }

        public DateTime StartDateTime { get; set; }

        public DateTime EndDateTime { get; set; }

        public int Minute { get; set; }
    }
}
