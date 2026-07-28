using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// Fotografia dello smistamento AI per la pagina di configurazione: se e' operativo, quanto
    /// e' curata la descrizione dei gruppi e come sta andando negli ultimi trenta giorni.
    /// </summary>
    public class TicketRoutingStatusDTO
    {
        /// <summary>False quando manca la chiave API: lo smistamento resta inerte, i ticket vanno in coda.</summary>
        public bool AiAvailable { get; set; }

        /// <summary>Modello effettivamente usato dalle chiamate.</summary>
        public string Model { get; set; } = string.Empty;

        public int GroupsTotal { get; set; }

        /// <summary>Gruppi con le competenze compilate: sotto il totale, l'AI sceglie alla cieca.</summary>
        public int GroupsWithHints { get; set; }

        /// <summary>Tipi di ticket senza alcun gruppo collegato: con il filtro attivo restano senza candidati.</summary>
        public int TicketTypesWithoutGroups { get; set; }

        // ─── Andamento ultimi 30 giorni ─────────────────────────────────────────
        public int RoutedLast30Days { get; set; }

        public int AutoAssignedLast30Days { get; set; }

        public int AcceptedLast30Days { get; set; }

        public int CorrectedLast30Days { get; set; }

        public int PendingLast30Days { get; set; }

        public int DismissedLast30Days { get; set; }

        /// <summary>Percentuale di suggerimenti confermati sul totale di quelli con esito noto (accettati + corretti).</summary>
        public double? AccuracyLast30Days =>
            (AcceptedLast30Days + CorrectedLast30Days) == 0
                ? null
                : (double)AcceptedLast30Days / (AcceptedLast30Days + CorrectedLast30Days);
    }

    /// <summary>Riga della tabella "competenze dei gruppi" nella pagina di configurazione.</summary>
    public class TicketRoutingGroupDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? AiRoutingHints { get; set; }

        public int UsersCount { get; set; }

        /// <summary>Tipi di ticket per cui il gruppo e' abilitato: sono i casi in cui puo' essere scelto.</summary>
        public List<string> TicketTypes { get; set; } = new();
    }

    /// <summary>Aggiornamento delle sole competenze di un gruppo, dalla pagina di configurazione.</summary>
    public class TicketRoutingHintsRequest
    {
        [MaxLength(2000)]
        public string? AiRoutingHints { get; set; }
    }

    /// <summary>Prova di smistamento su un testo qualsiasi, senza creare nulla.</summary>
    public class TicketRoutingPreviewRequest
    {
        [Required]
        public int IdTicketType { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Description { get; set; } = string.Empty;
    }

    public class TicketRoutingPreviewResult
    {
        public int? IdGroup { get; set; }

        public string? GroupName { get; set; }

        public double? Confidence { get; set; }

        public string? Reason { get; set; }

        /// <summary>True se, con la soglia attuale, questo suggerimento assegnerebbe il gruppo da solo.</summary>
        public bool WouldAutoAssign { get; set; }

        /// <summary>Gruppi effettivamente proposti al modello: se e' vuoto il problema non e' l'AI.</summary>
        public List<string> Candidates { get; set; } = new();

        /// <summary>Motivo per cui la prova non ha prodotto un suggerimento (chiave assente, nessun candidato, errore).</summary>
        public string? Error { get; set; }
    }
}
