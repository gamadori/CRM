using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum LeadStatus
    {
        New,
        Contacted,
        Qualified,
        Converted,
        Lost,
        Disqualified
    }

    public enum LeadSource
    {
        Manual,
        Website,
        Email,
        Phone,
        Referral,
        Campaign,
        Social,
        Event,
        Other
    }

    public class Lead
    {
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = "Nome lead")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Azienda")]
        public string? CompanyName { get; set; }

        [Display(Name = "Ruolo")]
        public string? JobTitle { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Telefono")]
        public string? Phone { get; set; }

        [Display(Name = "Fonte")]
        public LeadSource Source { get; set; } = LeadSource.Manual;

        [Display(Name = "Stato")]
        public LeadStatus Status { get; set; } = LeadStatus.New;

        [Range(0, 100)]
        [Display(Name = "Score")]
        public int Score { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = "Valore stimato")]
        public decimal EstimatedValue { get; set; }

        [Display(Name = "Chiusura prevista")]
        public DateTime? ExpectedCloseDate { get; set; }

        [Display(Name = "Note")]
        public string? Note { get; set; }

        [ForeignKey(nameof(Company))]
        public int? IdCompany { get; set; }

        [ForeignKey(nameof(Contact))]
        public int? IdContact { get; set; }

        [ForeignKey(nameof(User))]
        public string? IdUser { get; set; }

        [ForeignKey(nameof(ConvertedDeal))]
        public int? ConvertedDealId { get; set; }

        /// <summary>
        /// Iniziativa da cui il lead arriva: la fiera, il webinar, la campagna.
        /// <para>
        /// <see cref="Source"/> dice il canale, questo dice QUALE occasione - ed e' la seconda meta'
        /// a rendere rispondibile la domanda vera, cioe' se quella fiera e' valsa la spesa.
        /// </para>
        /// <para>
        /// In conversione va propagato sul <see cref="Deal"/>: e' esattamente li' che
        /// l'attribuzione si perde se ci si dimentica.
        /// </para>
        /// </summary>
        [ForeignKey(nameof(Initiative))]
        [Display(Name = "Iniziativa")]
        public int? IdInitiative { get; set; }

        /// <summary>
        /// Foto del biglietto da visita da cui il lead e' nato.
        /// <para>
        /// E' la FONTE, non un allegato decorativo: la lettura automatica dei campi e' una
        /// comodita' che puo' fallire, essere non configurata o non partire perche' allo stand non
        /// c'e' rete. Finche' la foto e' salvata, il contatto e' recuperabile a mano; senza, un
        /// biglietto letto male e' un contatto perso.
        /// </para>
        /// </summary>
        [ForeignKey(nameof(BusinessCard))]
        [Display(Name = "Biglietto da visita")]
        public int? IdBusinessCard { get; set; }

        /// <summary>
        /// Identificativo assegnato dall'app da campo al momento della cattura.
        /// <para>
        /// Serve a non duplicare: un telefono in fiera manda, non riceve la risposta perche' la
        /// rete cade a meta', e riprova. Senza questa chiave il secondo tentativo creerebbe un
        /// secondo lead identico, e il doppione lo si scopre solo la sera contando i biglietti.
        /// </para>
        /// </summary>
        [MaxLength(64)]
        public string? FieldClientId { get; set; }

        public virtual AttachmentFile? BusinessCard { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ConvertedAt { get; set; }

        public virtual Company? Company { get; set; }

        public virtual Contact? Contact { get; set; }

        public virtual ApplicationUser? User { get; set; }

        public virtual Deal? ConvertedDeal { get; set; }

        public virtual Initiative? Initiative { get; set; }

        public virtual ICollection<LeadProductInterest> ProductInterests { get; set; } = new List<LeadProductInterest>();
    }

    public class LeadFilter : PagingParameterModel
    {
        public string? Search { get; set; }

        public string? IdUser { get; set; }

        public LeadStatus? Status { get; set; }

        public LeadSource? Source { get; set; }

        public int? IdInitiative { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }
    }
}
