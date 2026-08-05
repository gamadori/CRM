using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class InitiativeDTO
    {
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Nome")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Tipo")]
        public InitiativeKind Kind { get; set; } = InitiativeKind.Trip;

        [Display(Name = "Stato")]
        public InitiativeState State { get; set; } = InitiativeState.Planned;

        [Display(Name = "Luogo")]
        public string? Location { get; set; }

        [Display(Name = "Dal")]
        public DateTime DateFrom { get; set; }

        [Display(Name = "Al")]
        public DateTime DateTo { get; set; }

        [Display(Name = "Budget previsto")]
        public decimal? BudgetPlanned { get; set; }

        [Display(Name = "Obiettivo")]
        public string? Objective { get; set; }

        [Display(Name = "Relazione finale")]
        public string? ClosingNotes { get; set; }

        [Display(Name = "Responsabile")]
        public string? IdOwner { get; set; }

        public string OwnerName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public List<string> ParticipantIds { get; set; } = new();

        public List<string> ParticipantNames { get; set; } = new();

        public List<InitiativeMemberDTO> Members { get; set; } = new();

        public List<InitiativeScheduleDTO> Schedules { get; set; } = new();

        /// <summary>Consuntivo speso finora, in valuta base. Riempito negli elenchi per la colonna costi.</summary>
        public decimal CostTotal { get; set; }

        /// <summary>Quante attivita' sono agganciate all'iniziativa (colonna d'elenco).</summary>
        public int ActivityCount { get; set; }

        public int Permits { get; set; }
    }

    /// <summary>
    /// Il resoconto di un'iniziativa. Tutto derivato dai dati sotto e mai congelato in colonne:
    /// l'unica parte scritta a mano e' <see cref="InitiativeDTO.ClosingNotes"/>.
    /// </summary>
    public class InitiativeSummaryDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public InitiativeKind Kind { get; set; }

        public InitiativeState State { get; set; }

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public decimal? BudgetPlanned { get; set; }

        // ---- costi -------------------------------------------------------------------

        /// <summary>Somma degli importi gia' convertiti in valuta base.</summary>
        public decimal CostTotal { get; set; }

        public int ExpenseCount { get; set; }

        /// <summary>
        /// Spese ancora senza importo in valuta base (cambio non determinato). Vanno mostrate come
        /// tali: non entrano in <see cref="CostTotal"/> e non si scartano in silenzio, altrimenti
        /// il consuntivo tace una spesa e vale meno di un consuntivo mancante.
        /// </summary>
        public int ExpensePendingConversion { get; set; }

        public List<InitiativeCostByUserDTO> CostByUser { get; set; } = new();

        // ---- cosa e' successo --------------------------------------------------------

        public int ActivityTotal { get; set; }

        public int ActivityDone { get; set; }

        public int ActivityPlanned { get; set; }

        /// <summary>Chi si e' visto, in ordine di data. E' il cuore del resoconto di una trasferta.</summary>
        public List<InitiativeVisitDTO> Visits { get; set; } = new();

        // ---- lead (solo fiere) -------------------------------------------------------

        public int LeadTotal { get; set; }

        public int LeadConverted { get; set; }

        public List<InitiativeLeadStatusDTO> LeadsByStatus { get; set; } = new();

        // ---- cosa si e' aperto -------------------------------------------------------

        public int DealCount { get; set; }

        public decimal DealAmount { get; set; }

        public int DealWonCount { get; set; }

        public decimal DealWonAmount { get; set; }

        public List<ActivityGeneratedItemDTO> Deals { get; set; } = new();

        public int QuoteCount { get; set; }

        public decimal QuoteAmount { get; set; }

        public List<ActivityGeneratedItemDTO> Quotes { get; set; } = new();

        public int OrderCount { get; set; }

        public decimal OrderAmount { get; set; }

        public List<ActivityGeneratedItemDTO> Orders { get; set; } = new();

        // ---- indicatori di ritorno (SOLO fiere e campagne) ---------------------------

        /// <summary>
        /// Costo per lead. Null per le trasferte, e non per pigrizia dell'interfaccia: su un giro
        /// clienti il denominatore non esiste (i clienti c'erano gia').
        /// </summary>
        public decimal? CostPerLead { get; set; }

        /// <summary>
        /// Ritorno sul valore vinto. Null per le trasferte: l'ordine firmato in visita sarebbe
        /// probabilmente arrivato lo stesso e una visita di cortesia non ha ritorno misurabile;
        /// calcolarlo produrrebbe un numero preciso e falso.
        /// </summary>
        public decimal? Roi { get; set; }
    }

    public class InitiativeCostByUserDTO
    {
        public string IdUser { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public int Count { get; set; }

        /// <summary>Spese di questa persona ancora da convertire.</summary>
        public int PendingConversion { get; set; }
    }

    public class InitiativeMemberDTO
    {
        public int Id { get; set; }

        public int IdInitiative { get; set; }

        public string IdUser { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public InitiativeMemberRole Role { get; set; } = InitiativeMemberRole.Participant;

        public string? Notes { get; set; }

        public DateTime AddedAt { get; set; }
    }

    public class InitiativeScheduleDTO
    {
        public int Id { get; set; }

        public int IdInitiative { get; set; }

        public string IdUser { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public InitiativeScheduleType Type { get; set; } = InitiativeScheduleType.Presence;

        public string? Location { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class InitiativeLeadStatusDTO
    {
        public LeadStatus Status { get; set; }

        public int Count { get; set; }
    }

    /// <summary>
    /// Un biglietto raccolto, visto dal triage della sera.
    /// <para>
    /// La cattura allo stand dura trenta secondi e lascia per forza dei buchi: qui si vede cosa
    /// manca e a chi il contatto somiglia fra le aziende gia' a sistema, perche' meta' dei biglietti
    /// di una fiera sono clienti che si hanno gia'.
    /// </para>
    /// </summary>
    public class InitiativeLeadTriageDTO
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        /// <summary>Cosa voleva: il campo che a sera nessuno ricostruisce piu'.</summary>
        public string? Note { get; set; }

        public int Score { get; set; }

        public LeadStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>La foto del biglietto: finche' c'e', il contatto e' recuperabile a mano.</summary>
        public bool HasBusinessCard { get; set; }

        public int? IdCompany { get; set; }

        /// <summary>Cosa manca, in chiaro: "recapito", "azienda", "cosa voleva".</summary>
        public List<string> Missing { get; set; } = new();

        public bool IsIncomplete => Missing.Count > 0;

        /// <summary>Azienda gia' a sistema a cui il lead somiglia; null se non si e' trovato nulla.</summary>
        public int? SuggestedCompanyId { get; set; }

        public string? SuggestedCompanyName { get; set; }

        /// <summary>Perche' e' stata proposta: si mostra, cosi' chi decide sa su cosa sta decidendo.</summary>
        public string? SuggestionReason { get; set; }
    }

    /// <summary>
    /// Una persona impegnata in un'iniziativa in un certo periodo.
    /// <para>
    /// Serve dove la domanda "dov'e'" si pone davvero senza aprire l'agenda: al momento di
    /// assegnare un ticket. Non e' un modulo assenze - ferie, permessi e malattia restano fuori -
    /// ma copre il caso in cui l'informazione oggi si perde di piu', perche' dura giorni e riguarda
    /// piu' persone insieme.
    /// </para>
    /// </summary>
    public class UserAwayDTO
    {
        public string IdUser { get; set; } = string.Empty;

        public int IdInitiative { get; set; }

        public string InitiativeName { get; set; } = string.Empty;

        public InitiativeKind Kind { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public string? Location { get; set; }
    }

    /// <summary>Una tappa del resoconto: chi, quando, com'e' andata.</summary>
    public class InitiativeVisitDTO
    {
        public int IdActivity { get; set; }

        public ActivityKind Kind { get; set; }

        public string Subject { get; set; } = string.Empty;

        public ActivityEntityType EntityType { get; set; }

        public int EntityId { get; set; }

        /// <summary>Nome del cliente/contatto visitato.</summary>
        public string EntityName { get; set; } = string.Empty;

        public DateTime? Date { get; set; }

        public ActivityState State { get; set; }

        public string? Outcome { get; set; }

        public string UserName { get; set; } = string.Empty;
    }

    public static class InitiativeHelper
    {
        public static InitiativeDTO? ToDTO(this Initiative i)
        {
            if (i == null) return null;

            var dto = new InitiativeDTO
            {
                Id = i.Id,
                Name = i.Name,
                Kind = i.Kind,
                State = i.State,
                Location = i.Location,
                DateFrom = i.DateFrom,
                DateTo = i.DateTo,
                BudgetPlanned = i.BudgetPlanned,
                Objective = i.Objective,
                ClosingNotes = i.ClosingNotes,
                IdOwner = i.IdOwner,
                OwnerName = i.Owner != null ? i.Owner.NameComplete : string.Empty,
                CreatedAt = i.CreatedAt,
                ClosedAt = i.ClosedAt,
                Members = i.Members?
                    .Select(m => new InitiativeMemberDTO
                    {
                        Id = m.Id,
                        IdInitiative = m.IdInitiative,
                        IdUser = m.IdUser,
                        UserName = m.User != null ? m.User.NameComplete : string.Empty,
                        Role = m.Role,
                        Notes = m.Notes,
                        AddedAt = m.AddedAt
                    })
                    .Where(m => !string.IsNullOrWhiteSpace(m.IdUser))
                    .GroupBy(m => m.IdUser)
                    .Select(g => g.First())
                    .ToList() ?? new List<InitiativeMemberDTO>(),
                Schedules = i.Schedules?
                    .Select(s => new InitiativeScheduleDTO
                    {
                        Id = s.Id,
                        IdInitiative = s.IdInitiative,
                        IdUser = s.IdUser,
                        UserName = s.User != null ? s.User.NameComplete : string.Empty,
                        Start = s.Start,
                        End = s.End,
                        Type = s.Type,
                        Location = s.Location,
                        Notes = s.Notes,
                        CreatedAt = s.CreatedAt
                    })
                    .OrderBy(s => s.Start)
                    .ToList() ?? new List<InitiativeScheduleDTO>()
            };

            dto.ParticipantIds = dto.Members
                    .Select(p => p.IdUser)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();
            dto.ParticipantNames = dto.Members
                    .Select(p => p.UserName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

            return dto;
        }

        public static Initiative ToEntity(this InitiativeDTO dto)
        {
            if (dto == null) return null;

            return new Initiative
            {
                Id = dto.Id,
                Name = dto.Name,
                Kind = dto.Kind,
                State = dto.State,
                Location = dto.Location,
                DateFrom = dto.DateFrom,
                DateTo = dto.DateTo,
                BudgetPlanned = dto.BudgetPlanned,
                Objective = dto.Objective,
                ClosingNotes = dto.ClosingNotes,
                IdOwner = dto.IdOwner,
                CreatedAt = dto.CreatedAt,
                ClosedAt = dto.ClosedAt,
                Members = (dto.Members.Count > 0
                        ? dto.Members
                        : dto.ParticipantIds.Select(id => new InitiativeMemberDTO { IdUser = id }))
                    .Where(m => !string.IsNullOrWhiteSpace(m.IdUser))
                    .GroupBy(m => m.IdUser)
                    .Select(g =>
                    {
                        var m = g.First();
                        return new InitiativeMember
                        {
                            IdUser = m.IdUser,
                            Role = m.Role,
                            Notes = m.Notes
                        };
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Se per questo tipo di iniziativa il ritorno commerciale sia una domanda sensata.
        /// Vero solo dove l'iniziativa GENERA relazioni che prima non c'erano.
        /// </summary>
        public static bool HasMeaningfulRoi(this InitiativeKind kind) =>
            kind is InitiativeKind.Fair or InitiativeKind.Webinar or InitiativeKind.Mailing or InitiativeKind.Conference;
    }
}
