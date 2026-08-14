using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    /// <summary>
    /// Stato di verifica della firma digitale.
    /// </summary>
    /// <remarks>
    /// I valori sono interi in tabella: aggiungere sempre in coda, mai in mezzo.
    /// </remarks>
    public enum SignatureStatus
    {
        /// <summary>
        /// Firma in attesa di conferma via email
        /// </summary>
        [Display(Name = "In attesa")]
        Pending = 0,

        /// <summary>
        /// Firma confermata dal cliente
        /// </summary>
        [Display(Name = "Confermata")]
        Verified = 1,

        /// <summary>
        /// Firma rifiutata dal cliente
        /// </summary>
        [Display(Name = "Rifiutata")]
        Rejected = 2,

        /// <summary>
        /// Nessuna firma prevista per questo intervento, oppure prevista ma non ancora chiesta a
        /// nessuno.
        /// <para>
        /// Mancava, ed e' il motivo per cui ogni intervento nasceva "in attesa di firma": il valore
        /// di partenza di un enum e' lo zero, e lo zero era <see cref="Pending"/>. Da qui la
        /// segnalazione sul verbale di una telefonata e il conteggio in dashboard corretto a mano
        /// aggiungendo "...e la firma pero' c'e'".
        /// </para>
        /// </summary>
        [Display(Name = "Non richiesta")]
        NotRequired = 3
    }

    /// <summary>
    /// Che firma serve per un intervento. Si configura per tipo di intervento e viene poi
    /// congelata sul singolo intervento: cambiare le impostazioni non deve riscrivere il
    /// significato dei verbali gia' chiusi.
    /// </summary>
    public enum SignatureRequirement
    {
        /// <summary>Nessuna firma: il verbale non la chiede e non la segnala mancante.</summary>
        [Display(Name = "Nessuna firma")]
        None = 0,

        /// <summary>
        /// Firma sul dispositivo del tecnico, con conferma OTP. Il cliente e' davanti a lui.
        /// Resta comunque possibile ripiegare sulla firma remota, perche' il cliente puo'
        /// essersene andato prima.
        /// </summary>
        [Display(Name = "Firma sul dispositivo")]
        OnDevice = 1,

        /// <summary>Firma da remoto: al cliente arriva un link e firma per conto suo.</summary>
        [Display(Name = "Firma remota")]
        Remote = 2
    }

    public class TicketIntervention
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        public string IdUser { get; set; }

        [Display(Name = nameof(TicketIntervention.SupportType), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public int SupportType { get; set; }

        [Display(Name = nameof(TicketIntervention.Activities), ResourceType = typeof(Resources.Models.TicketIntervention))]
        [Required]
        public string Activities { get; set; }

        [Display(Name = nameof(TicketIntervention.MountedParts), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public string MountedParts { get; set; }

        [Display(Name = nameof(TicketIntervention.Note), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public string? Note { get; set; }

        [Display(Name = nameof(TicketIntervention.StartDateTime), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public DateTime StartDateTime { get; set; }

        [Display(Name = nameof(TicketIntervention.EndDateTime), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public DateTime EndDateTime { get; set; }

        public bool HasAttachments { get; set; }

        [NotMapped]
        public bool AttachmentExist { get; set; }

        [Display(Name = "Minute")]
        public int Minute { get; set; }

        /// <summary>
        /// Firma del cliente in formato Base64 (PNG)
        /// </summary>
        [Display(Name = nameof(TicketIntervention.CustomerSignature), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public string? CustomerSignature { get; set; }

        /// <summary>
        /// Data e ora in cui è stata apposta la firma digitale
        /// </summary>
        [Display(Name = nameof(TicketIntervention.SignatureDate), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public DateTime? SignatureDate { get; set; }

        /// <summary>
        /// Nome completo di chi ha firmato il documento
        /// </summary>
        [Display(Name = nameof(TicketIntervention.SignatureName), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public string? SignatureName { get; set; }

        /// <summary>
        /// Email del firmatario per conferma
        /// </summary>
        [Display(Name = nameof(TicketIntervention.SignatureEmail), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public string? SignatureEmail { get; set; }

        /// <summary>
        /// Stato verifica firma. Parte da <see cref="SignatureStatus.NotRequired"/>: finche' non
        /// si chiede niente a nessuno, non c'e' nessuna attesa da segnalare.
        /// </summary>
        [Display(Name = nameof(TicketIntervention.SignatureStatus), ResourceType = typeof(Resources.Models.TicketIntervention))]
        public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.NotRequired;

        /// <summary>
        /// Che firma serve per questo intervento, copiata dalle impostazioni del tipo intervento
        /// al momento della creazione.
        /// <para>
        /// E' congelata di proposito: se domani si cambia la configurazione, i verbali gia' chiusi
        /// non devono cambiare significato ne' rigenerarsi con un'altra segnalazione. Si ricalcola
        /// solo finche' non c'e' ancora nessuna firma, cioe' se il tecnico corregge il tipo.
        /// </para>
        /// </summary>
        [Display(Name = "Firma richiesta")]
        public SignatureRequirement SignatureRequirement { get; set; } = SignatureRequirement.None;

        // ✅ NUOVI CAMPI PER ESTRAZIONE AUTOMATICA RECEIPT/FATTURE (Azure Form Recognizer)

        /// <summary>
        /// ID del file attachment contenente lo scontrino/fattura (se presente)
        /// </summary>
        [Display(Name = "ID Receipt Attachment")]
        public int? ReceiptAttachmentFileId { get; set; }

        /// <summary>
        /// Importo totale estratto automaticamente dallo scontrino
        /// </summary>
        [Display(Name = "Importo Totale Estratto")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ExtractedTotalAmount { get; set; }

        /// <summary>
        /// Importo IVA estratto
        /// </summary>
        [Display(Name = "IVA Estratta")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ExtractedTaxAmount { get; set; }

        /// <summary>
        /// Data transazione estratta dallo scontrino
        /// </summary>
        [Display(Name = "Data Transazione Estratta")]
        public DateTime? ExtractedTransactionDate { get; set; }

        /// <summary>
        /// Nome commerciante estratto
        /// </summary>
        [Display(Name = "Commerciante Estratto")]
        [MaxLength(200)]
        public string? ExtractedMerchantName { get; set; }

        /// <summary>
        /// Descrizione estratta automaticamente (commerciante + data + totale)
        /// </summary>
        [Display(Name = "Descrizione Estratta")]
        [MaxLength(500)]
        public string? ExtractedDescription { get; set; }

        /// <summary>
        /// Valuta estratta (EUR, USD, ecc.)
        /// </summary>
        [Display(Name = "Valuta")]
        [MaxLength(10)]
        public string? ExtractedCurrency { get; set; }

        /// <summary>
        /// Confidence media dell'estrazione (0-1)
        /// </summary>
        [Display(Name = "Confidence Estrazione")]
        public float? ExtractionConfidence { get; set; }

        /// <summary>
        /// Data e ora dell'elaborazione automatica
        /// </summary>
        [Display(Name = "Data Elaborazione Receipt")]
        public DateTime? ReceiptProcessedDate { get; set; }

        /// <summary>
        /// Se true, i dati estratti sono stati confermati dall'utente
        /// </summary>
        [Display(Name = "Dati Estratti Confermati")]
        public bool ExtractionConfirmed { get; set; } = false;

        /// <summary>
        /// JSON raw dei campi estratti (per debug/audit)
        /// </summary>
        public string? ExtractedFieldsJson { get; set; }

        /// <summary>
        /// Token univoco per conferma firma via email
        /// </summary>
        public string? SignatureConfirmationToken { get; set; }

        /// <summary>
        /// Scadenza del token di firma. Un link mandato al cliente vale una finestra di giorni
        /// (<see cref="GlobalSetting.SignatureLinkValidityDays"/>), non per sempre: un verbale di
        /// marzo non deve poter essere firmato a ottobre da chi ritrova quella email.
        /// <para>
        /// Vuota vale <b>scaduto</b>, non "senza scadenza": un percorso che dimenticasse di
        /// impostarla tornerebbe altrimenti in silenzio al link eterno. Le righe che avevano gia'
        /// un token quando la colonna e' stata aggiunta hanno ricevuto una finestra di grazia.
        /// </para>
        /// </summary>
        public DateTime? SignatureTokenExpiry { get; set; }

        /// <summary>
        /// Data conferma firma
        /// </summary>
        [Display(Name = "Data Conferma Firma")]
        public DateTime? SignatureConfirmedDate { get; set; }

        /// <summary>
        /// Hash OTP per verifica firma (temporaneo, cancellato dopo verifica)
        /// </summary>
        public string? SignatureOtpHash { get; set; }

        /// <summary>
        /// Scadenza OTP firma
        /// </summary>
        public DateTime? SignatureOtpExpiry { get; set; }

        /// <summary>
        /// Numero tentativi OTP falliti
        /// </summary>
        public int SignatureOtpAttempts { get; set; }

        /// <summary>
        /// ID univoco challenge OTP (UUID)
        /// </summary>
        public string? SignatureOtpChallengeId { get; set; }

        /// <summary>
        /// Firma in attesa di verifica OTP (Base64 temporaneo)
        /// </summary>
        public string? PendingSignature { get; set; }

        /// <summary>
        /// Nome firmatario in attesa di verifica OTP
        /// </summary>
        public string? PendingSignatureName { get; set; }

        [Display(Name = "Tipi di Intervento")]
        [NotMapped]
        public List<int> InterventionsTypesId { get; set; } = new List<int>();

        [NotMapped]
        public int Permits { get; set; }

        [NotMapped]
        public string UserName { get; set; }

        [NotMapped]
        public string SupportTypeDesc { get; set; }

        /// <summary>
        /// Su cosa si e' lavorato. Riempiti dall'elenco interventi: guardando cosa ha fatto una
        /// persona in un giorno, sapere le ore senza sapere su quale ticket e per quale cliente non
        /// serve a niente.
        /// </summary>
        [NotMapped]
        public string TicketNumero { get; set; } = string.Empty;

        [NotMapped]
        public string Cliente { get; set; } = string.Empty;

        [NotMapped]
        public string Commessa { get; set; } = string.Empty;

        [NotMapped]
        public List<TicketInterventionArticleModel> InterventionArticles { get; set; } = new List<TicketInterventionArticleModel>();

        [NotMapped]
        public List<UserModel> Users { get; set; } = new List<UserModel>();

        public virtual Ticket Ticket { get; set; }

        /// <summary>
        /// ✅ FONTE DI VERITÀ: Collezione di tutti gli utenti assegnati all'intervento
        /// Il primo elemento è considerato l'utente principale
        /// </summary>
        [JsonIgnore]
        public virtual ICollection<TicketInterventionUser> AssignedUsers { get; set; } = new List<TicketInterventionUser>();

        [Display(Name = "Tipi di Intervento")]
        public virtual ICollection<InterventionType> TicketInterventionsTypes { get; set; }

        [Display(Name = "Dispositivi")]
        public virtual ICollection<TicketInterventionArticle> TicketInterventionArticles { get; set; }

        [Display(Name = "Orari")]
        public virtual ICollection<TicketInterventionTime> TicketInterventionTime { get; set; }

        /// <summary>
        /// ✅ NUOVO: Collezione delle note spese associate a questo intervento
        /// </summary>
        [Display(Name = "Note Spese")]
        [JsonIgnore]
        public virtual ICollection<ExpenseReceipt> ExpenseReceipts { get; set; } = new List<ExpenseReceipt>();
    }

    public class TicketInterventionFilter: PagingParameterModel
    {
        public int? IdTicket { get; set; }

        public string IdUser { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public int? SupportType { get; set; }

        public bool? SignaturePending { get; set; } = null;
    }
}
