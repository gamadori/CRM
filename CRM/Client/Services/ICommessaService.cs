using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ICommessaService : IDataService<Commessa, CommessaDTO, int, CommessaFilter, int>
    {
        Task<APIResponseMessage<CommessaDTO>> ChangeStateAsync(int id, CommessaStates state);

        /// <summary>Avvia produzione: crea le commesse (una per unità) dalla riga d'ordine.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(int orderRowId);

        /// <summary>Avvia produzione interna: commesse senza ordine (magazzino, prototipi, ricambi).</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartInternalProductionAsync(InternalProductionRequestDTO req);

        /// <summary>Apre una commessa a fasi libere dalla riga d'ordine: nessun template, una fase
        /// di lavorazione, ticket aggiunti a mano. Congela consegna e ore stimate come baseline.</summary>
        Task<APIResponseMessage<CommessaDTO>> OpenCommessaAsync(OpenCommessaRequestDTO req);

        Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId);

        /// <summary>Sposta il piano su una nuova consegna: le fasi traslano dello stesso numero di
        /// giorni lavorativi, quelle già avviate solo se la consegna anticipa.</summary>
        Task<APIResponseMessage<CommessaDTO>> RescheduleAsync(int id, DateTime delivery);

        /// <summary>Ricostruisce le fasi dal template del prodotto: scarta le modifiche al piano.</summary>
        Task<APIResponseMessage<CommessaDTO>> RebuildPlanFromTemplateAsync(int id, DateTime delivery);

        Task<List<CommessaDTO>> GetByOrderAsync(int orderId);
    }
}
