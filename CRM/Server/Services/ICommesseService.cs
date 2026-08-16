using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ICommesseService
    {
        Task<CommessaDTO?> GetItemAsync(int id);

        Task<PagingResponse<CommessaDTO, int>?> GetSummaryAsync(CommessaFilter? args);

        Task<List<CommessaDTO>?> GetListAsync(CommessaFilter? args = null);

        Task<List<CommessaDTO>> GetByOrderAsync(int orderId);

        Task<APIResponseMessage<CommessaDTO>> PostAsync(Commessa item);

        Task<APIResponseMessage<CommessaDTO>> ChangeStateAsync(int id, CommessaStates state);

        /// <summary>Avvia produzione da una riga d'ordine con le date dell'ordine: una commessa per
        /// unità clonando le fasi del template, all'indietro dalla consegna.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(int orderRowId);

        /// <summary>Come sopra, ma il piano si costruisce dalla data e nel verso indicati: dalla
        /// consegna a ritroso, oppure dalla partenza in avanti con la fine calcolata.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(StartProductionRequestDTO req);

        /// <summary>Avvia produzione interna (magazzino, prototipi, ricambi, rilavorazioni): crea
        /// commesse senza riga d'ordine, dalla consegna a ritroso o dalla partenza in avanti.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartInternalProductionAsync(InternalProductionRequestDTO req);

        /// <summary>
        /// Apre una commessa "a fasi libere" da una riga d'ordine il cui prodotto non ha un template
        /// (sviluppo software, consulenza, servizi su misura): una sola commessa qualunque sia la
        /// quantità, una fase di lavorazione a chiusura manuale, ticket aggiunti a mano. Congela
        /// consegna e ore stimate come baseline del consuntivo.
        /// </summary>
        Task<APIResponseMessage<CommessaDTO>> OpenCommessaFromOrderRowAsync(OpenCommessaRequestDTO req);

        /// <summary>Conferma "pronto" una riga senza produzione (nessuna commessa).</summary>
        Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId);

        /// <summary>
        /// Sposta l'intero piano su una nuova consegna traslando le fasi dello stesso numero di
        /// giorni lavorativi: durate, dipendenze e personalizzazioni restano intatte.
        /// Se la consegna slitta in avanti le fasi gia' avviate o concluse non si toccano; se slitta
        /// indietro si spostano anche quelle, altrimenti le fasi da fare finirebbero sopra di esse.
        /// </summary>
        Task<APIResponseMessage<CommessaDTO>> RescheduleAsync(int id, DateTime newDelivery);

        /// <summary>
        /// Recupera l'anticipo maturato: tira avanti le fasi non ancora avviate fino al primo giorno
        /// utile dopo i loro predecessori, usandone la fine effettiva. Non tocca chi ha gia'
        /// cominciato ne' le fasi senza predecessori. Con <paramref name="preview"/> non salva:
        /// dice solo quanti giorni si recupererebbero, cosi' la decisione resta a chi guarda.
        /// </summary>
        Task<APIResponseMessage<CommessaDTO>> CompactPlanAsync(int id, bool preview);

        /// <summary>
        /// Ricostruisce le fasi dal template del prodotto, ripianificate all'indietro dalla consegna
        /// indicata: butta via ogni modifica fatta sul piano della commessa. Consentito solo se
        /// nessuna fase e' stata avviata; i ticket gia' creati restano ma perdono la fase collegata.
        /// </summary>
        Task<APIResponseMessage<CommessaDTO>> RebuildPlanFromTemplateAsync(int id, DateTime? newDelivery);

        /// <summary>Elimina la commessa e le sue fasi. Il Code distingue non trovata,
        /// non accessibile ed errore, cosi' il controller puo' rispondere con lo stato giusto.</summary>
        Task<APIResponseMessage<bool>> DeleteAsync(int id);
    }
}
