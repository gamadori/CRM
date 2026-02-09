using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    internal class TicketInterventionDTO
    {
        
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        public string IdUser { get; set; }


        [Display(Name = "Tipo Supporto")]
        public int SupportType { get; set; }

        [Required]
        [Display(Name = "Attività svolte")]
        public string Activities { get; set; }

        [Display(Name = "Parti Sostituite o Montate")]
        public string MountedParts { get; set; }

        [Display(Name = "Nota")]
        public string? Note { get; set; }

        [Display(Name = "Data e Orario di Inizio")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "Orario di Fine")]
        public DateTime EndDateTime { get; set; }

        public bool HasAttachments { get; set; }

        [NotMapped]
        public bool AttachmentExist { get; set; }

        [Display(Name = "Minute")]
        public int Minute { get; set; }

        /// <summary>
        /// Firma del cliente in formato Base64 (PNG)
        /// </summary>
        [Display(Name = "Firma Cliente")]
        public string? CustomerSignature { get; set; }

        /// <summary>
        /// Data e ora in cui è stata apposta la firma digitale
        /// </summary>
        [Display(Name = "Data Firma")]
        public DateTime? SignatureDate { get; set; }

        /// <summary>
        /// Nome completo di chi ha firmato il documento
        /// </summary>
        [Display(Name = "Nome Firmatario")]
        public string? SignatureName { get; set; }

        /// <summary>
        /// Email del firmatario per conferma
        /// </summary>
        [Display(Name = "Email Firmatario")]
        public string? SignatureEmail { get; set; }

        /// <summary>
        /// Stato verifica firma: Pending, Verified, Rejected
        /// </summary>
        [Display(Name = "Stato Firma")]
        public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Pending;

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
        public List<int> InterventionsTypesId { get; set; } = new List<int>();

        public int Permits { get; set; }

        [NotMapped]
        public List<UserModel> AssignedUsers { get; set; }

        [NotMapped]
        public string SupportTypeDesc { get; set; }

        [NotMapped]
        public List<TicketInterventionArticleModel> InterventionArticles { get; set; } = new List<TicketInterventionArticleModel>();

        public Ticket Ticket { get; set; }

           
        [Display(Name = "Tipi di Intervento")]
        public virtual ICollection<InterventionType> TicketInterventionsTypes { get; set; }

            
        [Display(Name = "Orari")]
        public List<TicketInterventionTime> TicketInterventionTime { get; set; }
       

    }
}
