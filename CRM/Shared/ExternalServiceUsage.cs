using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>Il fornitore a cui arriva la fattura.</summary>
    public enum ExternalServiceProvider
    {
        Anthropic = 1,
        Azure = 2,
        OpenAI = 3
    }

    /// <summary>
    /// La funzione del CRM che ha speso. E' il campo che giustifica l'intera tabella: <b>quanto</b>
    /// si spende lo dicono gia' le console dei fornitori, e gratis; <b>chi</b> lo sta spendendo no,
    /// perche' al fornitore arrivano chiamate tutte uguali. Senza questa colonna il consumo resta
    /// un totale che non si puo' mettere in discussione.
    /// </summary>
    public enum ExternalServiceFeature
    {
        /// <summary>Smistamento del ticket al gruppo.</summary>
        TicketRouting = 1,

        /// <summary>Triage delle email in arrivo (riassunto + verdetto).</summary>
        InboundEmailTriage = 2,

        /// <summary>Assistente AI: il giro completo di una domanda, tool compresi.</summary>
        Assistant = 3,

        /// <summary>Assistente AI: il "giudice" che verifica la pertinenza dei ticket citati.</summary>
        AssistantRerank = 4,

        /// <summary>Riassunto di un ticket chiuso.</summary>
        TicketSummary = 5,

        /// <summary>Traduzione di un template email.</summary>
        EmailTemplateTranslation = 6,

        /// <summary>Tipologia di una nota spese, quando i livelli deterministici non bastano.</summary>
        ExpenseCategory = 7,

        /// <summary>Lettura OCR di uno scontrino o di una fattura.</summary>
        ReceiptOcr = 8,

        /// <summary>Lettura OCR di un biglietto da visita raccolto in fiera.</summary>
        BusinessCardOcr = 9
    }

    /// <summary>
    /// Una chiamata a un servizio esterno a pagamento.
    /// <para>
    /// Registro a sola aggiunta: nessuna riga viene mai modificata dopo l'inserimento. Il costo e'
    /// una <b>stima</b> calcolata con il listino in configurazione al momento della chiamata, non
    /// un importo fatturato: serve a ripartire la spesa tra le funzioni, non a fare contabilita'.
    /// La riconciliazione con le fatture vere resta un passo separato.
    /// </para>
    /// <para>
    /// <see cref="IdUser"/> e' una colonna semplice, <b>senza</b> chiave esterna: la storia dei
    /// consumi deve sopravvivere alla cancellazione dell'utente che li ha causati, altrimenti il
    /// totale del mese cambierebbe da solo ogni volta che qualcuno lascia l'azienda.
    /// </para>
    /// </summary>
    public class ExternalServiceUsage
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Momento della chiamata, in UTC: i totali per giorno non devono spostarsi con l'ora legale.</summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        public ExternalServiceProvider Provider { get; set; }

        public ExternalServiceFeature Feature { get; set; }

        /// <summary>Modello (per Claude) oppure operazione (per Azure): e' l'unita' a cui si applica il listino.</summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>
        /// Chi ha causato la chiamata. Nullo per i servizi in background (posta in arrivo,
        /// riassunti automatici), e va bene cosi': quella spesa non e' di nessuno in particolare.
        /// </summary>
        [MaxLength(450)]
        public string? IdUser { get; set; }

        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        /// <summary>Token letti dalla cache: si pagano circa un decimo dell'input, ma si pagano.</summary>
        public long CacheReadTokens { get; set; }

        /// <summary>Token scritti in cache: costano piu' dell'input normale, e vanno contati a parte.</summary>
        public long CacheWriteTokens { get; set; }

        /// <summary>
        /// Quantita' per i servizi che non si pagano a token: pagine analizzate, documenti, messaggi.
        /// Per le chiamate a token resta 1, cioe' "una chiamata".
        /// </summary>
        public int Units { get; set; } = 1;

        /// <summary>
        /// Costo stimato. <b>Nullo</b> quando il listino non conosce il modello o l'operazione:
        /// meglio un buco dichiarato che uno zero, che nel totale sparirebbe senza dire niente.
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal? EstimatedCost { get; set; }

        /// <summary>Valuta del listino usato, congelata sulla riga: un cambio di listino non riscrive lo storico.</summary>
        [MaxLength(3)]
        public string? Currency { get; set; }

        /// <summary>
        /// Esito della chiamata. Le chiamate fallite si registrano lo stesso: quelle che falliscono
        /// dopo aver generato token sono spesa a vuoto, ed e' la prima cosa da cercare quando il
        /// totale non torna.
        /// </summary>
        public bool Success { get; set; } = true;

        public int DurationMs { get; set; }
    }
}
