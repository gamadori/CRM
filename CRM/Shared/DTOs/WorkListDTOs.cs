using System;
using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// In che gruppo finisce una riga dell'elenco dei lavori. L'ordine dei valori e' l'ordine in cui
    /// i gruppi compaiono a video.
    /// </summary>
    public enum WorkListGroup
    {
        /// <summary>Ticket di assistenza: hanno una data decisa da chi assegna.</summary>
        Assistenza = 0,

        /// <summary>Ticket di una fase di commessa che si puo' cominciare.</summary>
        Commessa = 1,

        /// <summary>
        /// Ticket di una fase che non si puo' ancora cominciare, perche' le fasi precedenti non
        /// sono finite. Restano in fondo: sono lavoro futuro, non lavoro di oggi.
        /// </summary>
        CommessaBloccata = 2
    }

    /// <summary>Una riga dell'elenco dei lavori: cosa fare, per chi, entro quando.</summary>
    public class WorkListItemDTO
    {
        public int IdTicket { get; set; }

        public WorkListGroup Group { get; set; }

        public string Numero { get; set; } = string.Empty;

        public string Descrizione { get; set; } = string.Empty;

        public string Cliente { get; set; } = string.Empty;

        /// <summary>Data pianificata. Valorizzata sui ticket di assistenza.</summary>
        public DateTime? Data { get; set; }

        public DateTime? Scadenza { get; set; }

        /// <summary>Vero se la scadenza e' gia' passata.</summary>
        public bool InRitardo { get; set; }

        /// <summary>Codice della commessa. Vuoto sui ticket di assistenza.</summary>
        public string CommessaCode { get; set; } = string.Empty;

        public int? IdCommessa { get; set; }

        public string FaseName { get; set; } = string.Empty;

        /// <summary>
        /// Vero se il ticket e' del gruppo dell'utente ma non l'ha ancora preso nessuno: a video
        /// diventa il pulsante per prenderlo in carico.
        /// </summary>
        public bool DaPrendere { get; set; }

        /// <summary>Nomi delle fasi che devono finire prima. Vuoto se il lavoro e' avviabile.</summary>
        public List<string> BloccatoDa { get; set; } = new();
    }

    /// <summary>Elenco dei lavori di una persona, gia' raggruppato e ordinato dal server.</summary>
    public class WorkListDTO
    {
        public List<WorkListItemDTO> Items { get; set; } = new();

        public string? ErrorMessage { get; set; }
    }
}
