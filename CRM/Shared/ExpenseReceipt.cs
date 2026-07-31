using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Una spesa sostenuta da una persona, con la sua prova (scontrino/fattura).
    /// <para>
    /// La spina dorsale e' <see cref="IdUserSpender"/> + <see cref="TransactionDate"/>: chi e
    /// quando ci sono sempre. Il collegamento al lavoro e' invece FACOLTATIVO, perche' nel mondo
    /// reale una spesa nasce anche dove un lavoro non c'e': trasferta pre-vendita, visita di
    /// cortesia, corso, bolletta. Renderlo obbligatorio non impedirebbe quei casi, li farebbe
    /// appendere a un intervento a caso — sporcando proprio il costo del lavoro per cui il
    /// collegamento esisteva.
    /// </para>
    /// </summary>
    public class ExpenseReceipt
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Chi ha sostenuto la spesa. E' l'unico dato sempre presente e la chiave con cui si
        /// risponde alla domanda vera ("quanto devo rimborsare a X questo mese").
        /// Da non confondere con <see cref="ConfirmedByUserId"/>, che e' chi ha validato l'OCR.
        /// </summary>
        [ForeignKey(nameof(UserSpender))]
        public string? IdUserSpender { get; set; }

        public ApplicationUser? UserSpender { get; set; }

        /// <summary>
        /// Intervento a cui la spesa si riferisce. Nullable: vedi il commento della classe.
        /// </summary>
        [ForeignKey("TicketIntervention")]
        public int? TicketInterventionId { get; set; }

        /// <summary>
        /// Navigazione verso l'intervento
        /// </summary>
        public TicketIntervention? TicketIntervention { get; set; }

        /// <summary>
        /// Attivita' a cui la spesa si riferisce, per le spese che non nascono da un intervento:
        /// visita commerciale, visita di cortesia, incontro. Riusa la polimorfia che
        /// <see cref="Activity"/> ha gia' su azienda/contatto/lead/deal/ticket, invece di
        /// aggiungere una chiave esterna per ciascuno.
        /// <para>
        /// Al massimo uno fra intervento e attivita' e' valorizzato; entrambi nulli = costo
        /// generale (bolletta, cancelleria), che e' un caso legittimo e non un dato incompleto.
        /// </para>
        /// </summary>
        [ForeignKey(nameof(Activity))]
        public int? IdActivity { get; set; }

        public Activity? Activity { get; set; }

        /// <summary>
        /// Descrizione testuale della nota spese
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// ID del file attachment contenente lo scontrino/fattura PDF (opzionale)
        /// </summary>
        [ForeignKey("AttachmentFile")]
        public int? AttachmentFileId { get; set; }

        /// <summary>
        /// Navigazione verso il file allegato
        /// </summary>
        public AttachmentFile? AttachmentFile { get; set; }

        /// <summary>
        /// Importo totale estratto automaticamente o inserito manualmente
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }

        /// <summary>
        /// Importo IVA/Tasse estratto o inserito manualmente
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxAmount { get; set; }

        /// <summary>
        /// Data transazione estratta dallo scontrino o inserita manualmente
        /// </summary>
        public DateTime? TransactionDate { get; set; }

        /// <summary>
        /// Nome commerciante estratto o inserito manualmente
        /// </summary>
        [MaxLength(200)]
        public string? MerchantName { get; set; }

        /// <summary>
        /// Valuta dello scontrino (EUR, USD, JPY...). Null quando l'OCR non e' riuscito a
        /// determinarla: e' un campo da compilare, non un valore da indovinare.
        /// </summary>
        [MaxLength(10)]
        public string? Currency { get; set; }

        /// <summary>
        /// Cambio verso la valuta base usato per questa spesa, CONGELATO al momento della
        /// registrazione.
        /// <para>
        /// Non si ricalcola al volo: un totale che cambia ogni volta che apri la pagina non e'
        /// utilizzabile per un rimborso, e il totale del mese scorso deve restare quello di
        /// allora. 1 quando la valuta e' gia' quella base.
        /// </para>
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal? ExchangeRate { get; set; }

        /// <summary>
        /// <see cref="TotalAmount"/> convertito in valuta base. Memorizzato e non derivato, cosi'
        /// i totali sono una somma che non puo' divergere dal cambio salvato.
        /// Null = spesa ancora da convertire: va mostrata come tale, mai inclusa nei totali
        /// e mai scartata in silenzio.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountBase { get; set; }

        /// <summary>
        /// Confidence media dell'estrazione automatica (0-1). Null se inserito manualmente
        /// </summary>
        public float? ExtractionConfidence { get; set; }

        /// <summary>
        /// Data e ora dell'elaborazione automatica. Null se inserito manualmente
        /// </summary>
        public DateTime? ProcessedDate { get; set; }

        /// <summary>
        /// Se true, i dati estratti sono stati confermati dall'utente
        /// </summary>
        public bool IsConfirmed { get; set; } = false;

        /// <summary>
        /// Data conferma da parte dell'utente
        /// </summary>
        public DateTime? ConfirmedDate { get; set; }

        /// <summary>
        /// ID utente che ha confermato
        /// </summary>
        public string? ConfirmedByUserId { get; set; }

        /// <summary>
        /// JSON raw dei campi estratti (per debug/audit)
        /// </summary>
        public string? ExtractedFieldsJson { get; set; }

        /// <summary>
        /// Data creazione record
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Data ultima modifica
        /// </summary>
        public DateTime? LastModifiedDate { get; set; }

        /// <summary>
        /// Note aggiuntive
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        public ICollection<ExpenseReceiptDocument> Documents { get; set; } = new List<ExpenseReceiptDocument>();
    }
}
