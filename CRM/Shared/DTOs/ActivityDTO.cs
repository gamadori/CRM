using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CRM.Shared.DTOs
{
    public class ActivityDTO
    {
        public int Id { get; set; }

        [Display(Name = "Tipo")]
        public ActivityKind Kind { get; set; } = ActivityKind.Note;

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Oggetto")]
        public string Subject { get; set; } = string.Empty;

        [Display(Name = "Descrizione")]
        public string? Description { get; set; }

        [Display(Name = "Entita'")]
        public ActivityEntityType EntityType { get; set; }

        [Display(Name = "Id entita'")]
        public int EntityId { get; set; }

        [Display(Name = "Creata da")]
        public string? IdUser { get; set; }

        [Display(Name = "Assegnata a")]
        public string? IdAssignee { get; set; }

        [Display(Name = "Scadenza")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Completata il")]
        public DateTime? DoneDate { get; set; }

        [Display(Name = "Esito")]
        public string? Outcome { get; set; }

        [Display(Name = "Relazione di chiusura")]
        public string? CompletionNotes { get; set; }

        [Display(Name = "Prossima azione")]
        public string? NextStep { get; set; }

        [Display(Name = "Completata da")]
        public string? IdCompletedBy { get; set; }

        [Display(Name = "Stato")]
        public ActivityState State { get; set; }

        [Display(Name = "Promemoria")]
        public DateTime? ReminderAt { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Id dell'email collegata (se l'attività rappresenta una comunicazione email registrata).</summary>
        public int? IdEmailSent { get; set; }

        /// <summary>Id dell'email in ingresso collegata (attività da email ricevuta).</summary>
        public int? IdInboundEmail { get; set; }

        /// <summary>Iniziativa (fiera, trasferta) durante la quale l'attività è avvenuta.</summary>
        public int? IdInitiative { get; set; }

        /// <summary>Stato di engagement dell'email collegata (risolto per la timeline).</summary>
        public EmailEngagementStatus? EmailEngagement { get; set; }

        public int EmailOpenCount { get; set; }

        public int EmailClickCount { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string AssigneeName { get; set; } = string.Empty;

        public string CompletedByName { get; set; } = string.Empty;

        public List<string> ParticipantIds { get; set; } = new();

        public List<string> ParticipantNames { get; set; } = new();

        /// <summary>Nome dell'entità collegata (azienda/contatto/deal/ticket), per l'agenda.</summary>
        public string EntityName { get; set; } = string.Empty;

        public int Permits { get; set; }
    }

    public static class ActivityHelper
    {
        public static ActivityDTO? ToDTO(this Activity a)
        {
            if (a == null) return null;

            return new ActivityDTO
            {
                Id = a.Id,
                Kind = a.Kind,
                Subject = a.Subject,
                Description = a.Description,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                IdUser = a.IdUser,
                IdAssignee = a.IdAssignee,
                DueDate = a.DueDate,
                DoneDate = a.DoneDate,
                Outcome = a.Outcome,
                CompletionNotes = a.CompletionNotes,
                NextStep = a.NextStep,
                IdCompletedBy = a.IdCompletedBy,
                State = a.State,
                ReminderAt = a.ReminderAt,
                CreatedAt = a.CreatedAt,
                IdEmailSent = a.IdEmailSent,
                IdInboundEmail = a.IdInboundEmail,
                IdInitiative = a.IdInitiative,
                UserName = a.User != null ? a.User.NameComplete : string.Empty,
                AssigneeName = a.Assignee != null ? a.Assignee.NameComplete : string.Empty,
                CompletedByName = a.CompletedBy != null ? a.CompletedBy.NameComplete : string.Empty,
                ParticipantIds = a.Participants?.Select(p => p.IdUser).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList() ?? new List<string>(),
                ParticipantNames = a.Participants?.Select(p => p.User != null ? p.User.NameComplete : string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList() ?? new List<string>()
            };
        }

        public static Activity ToEntity(this ActivityDTO dto)
        {
            if (dto == null) return null;

            return new Activity
            {
                Id = dto.Id,
                Kind = dto.Kind,
                Subject = dto.Subject,
                Description = dto.Description,
                EntityType = dto.EntityType,
                EntityId = dto.EntityId,
                IdUser = dto.IdUser,
                IdAssignee = dto.IdAssignee,
                DueDate = dto.DueDate,
                DoneDate = dto.DoneDate,
                Outcome = dto.Outcome,
                CompletionNotes = dto.CompletionNotes,
                NextStep = dto.NextStep,
                IdCompletedBy = dto.IdCompletedBy,
                State = dto.State,
                ReminderAt = dto.ReminderAt,
                CreatedAt = dto.CreatedAt,
                IdInitiative = dto.IdInitiative,
                Participants = dto.ParticipantIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .Select(id => new ActivityParticipant { IdUser = id })
                    .ToList()
            };
        }
    }

    /// <summary>
    /// Che cosa ha prodotto una visita: le opportunita' e i preventivi che ne sono nati.
    /// <para>
    /// E' la meta' che rende utile il collegamento: da sola l'origine sull'opportunita' risponde
    /// a "quante ne nascono dalle visite", questa risponde a "questa visita a cosa ha portato".
    /// </para>
    /// </summary>
    public class ActivityOutcomeDTO
    {
        public List<ActivityGeneratedItemDTO> Deals { get; set; } = new();

        public List<ActivityGeneratedItemDTO> Quotes { get; set; } = new();

        public bool Any => Deals.Count > 0 || Quotes.Count > 0;
    }

    public class ActivityGeneratedItemDTO
    {
        public int Id { get; set; }

        /// <summary>Nome dell'opportunita' o numero del preventivo.</summary>
        public string Label { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime Date { get; set; }

        /// <summary>Stato in chiaro, per il colore del badge.</summary>
        public string State { get; set; } = string.Empty;
    }

    /// <summary>Seguito da dare alla visita appena chiusa.</summary>
    public enum ActivityFollowUpTarget
    {
        /// <summary>Nessun seguito commerciale.</summary>
        None = 0,

        /// <summary>Ne nasce un'opportunita'.</summary>
        Deal = 1,

        /// <summary>Il cliente ha chiesto un preventivo.</summary>
        Quote = 2
    }

    public class ActivityCompletionRequest
    {
        /// <summary>
        /// Cosa segue la visita. Il server non se ne serve: e' la maschera che, chiusa
        /// l'attivita', porta al modulo giusto gia' compilato. Sta qui perche' questo oggetto e'
        /// il risultato del dialogo di chiusura, e il seguito e' parte di com'e' andata.
        /// </summary>
        public ActivityFollowUpTarget FollowUpTarget { get; set; } = ActivityFollowUpTarget.None;

        [Display(Name = "Esito")]
        public string? Outcome { get; set; }

        [Display(Name = "Relazione di chiusura")]
        public string? CompletionNotes { get; set; }

        [Display(Name = "Prossima azione")]
        public string? NextStep { get; set; }

        public bool CreateFollowUp { get; set; }

        [Display(Name = "Tipo follow-up")]
        public ActivityKind FollowUpKind { get; set; } = ActivityKind.Task;

        [Display(Name = "Oggetto follow-up")]
        public string? FollowUpSubject { get; set; }

        [Display(Name = "Scadenza follow-up")]
        public DateTime? FollowUpDueDate { get; set; }

        [Display(Name = "Assegnatario follow-up")]
        public string? FollowUpAssigneeId { get; set; }
    }
}
