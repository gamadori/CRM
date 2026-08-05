using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>Un biglietto raccolto e non ancora arrivato al server.</summary>
    public class PendingLead
    {
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? CompanyName { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Note { get; set; }

        public int Score { get; set; }

        public int IdInitiative { get; set; }

        /// <summary>
        /// La foto in base64, dentro la coda. Non un id di allegato: allo stand la rete puo' non
        /// esserci, quindi il caricamento del file e' un'operazione da fare al momento dell'invio,
        /// non prima di mettere il contatto al sicuro.
        /// </summary>
        public string? PhotoDataUrl { get; set; }

        public string? PhotoFileName { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public interface IOfflineLeadQueue : IAsyncDisposable
    {
        Task<int> CountAsync();
        Task EnqueueAsync(PendingLead lead);
        Task<List<PendingLead>> ListAsync();
        Task RemoveAsync(long id);
        Task<bool> IsOnlineAsync();
        Task WatchOnlineAsync<T>(DotNetObjectReference<T> callbackTarget) where T : class;

        /// <summary>Riduce una foto prima di metterla in coda. Ritorna l'originale se non ci riesce.</summary>
        Task<string> ShrinkAsync(string dataUrl, int maxSide = 1600, double quality = 0.85);
    }

    /// <summary>
    /// Coda locale dei biglietti, appoggiata a IndexedDB.
    /// <para>
    /// Ogni metodo e' tollerante al fallimento tranne <see cref="EnqueueAsync"/>: se la coda non
    /// riesce a scrivere, chi sta registrando DEVE saperlo, perche' e' l'unico momento in cui il
    /// contatto esiste solo sullo schermo.
    /// </para>
    /// </summary>
    public class OfflineLeadQueue : IOfflineLeadQueue
    {
        private readonly IJSRuntime _js;
        private IJSObjectReference? _module;

        public OfflineLeadQueue(IJSRuntime js) => _js = js;

        private async Task<IJSObjectReference> ModuleAsync()
            => _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/leadQueue.js");

        public async Task<int> CountAsync()
        {
            try
            {
                return await (await ModuleAsync()).InvokeAsync<int>("count");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Coda biglietti non leggibile: {ex.Message}");
                return 0;
            }
        }

        public async Task EnqueueAsync(PendingLead lead)
        {
            // Nessun catch: un fallimento qui va propagato alla pagina, che smette di svuotare il
            // modulo e lo dice. Inghiottirlo significherebbe cancellare i campi di un contatto che
            // non e' stato salvato da nessuna parte.
            await (await ModuleAsync()).InvokeVoidAsync("enqueue", lead);
        }

        public async Task<List<PendingLead>> ListAsync()
        {
            try
            {
                return await (await ModuleAsync()).InvokeAsync<List<PendingLead>>("list") ?? new List<PendingLead>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Coda biglietti non leggibile: {ex.Message}");
                return new List<PendingLead>();
            }
        }

        public async Task RemoveAsync(long id)
        {
            try
            {
                await (await ModuleAsync()).InvokeVoidAsync("remove", id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Elemento {id} non rimosso dalla coda: {ex.Message}");
            }
        }

        public async Task<bool> IsOnlineAsync()
        {
            try
            {
                return await (await ModuleAsync()).InvokeAsync<bool>("isOnline");
            }
            catch
            {
                // Senza risposta si prova comunque a spedire: un tentativo fallito costa un
                // messaggio, un invio saltato costa un contatto.
                return true;
            }
        }

        public async Task WatchOnlineAsync<T>(DotNetObjectReference<T> callbackTarget) where T : class
        {
            try
            {
                await (await ModuleAsync()).InvokeVoidAsync("watchOnline", callbackTarget);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ritorno della rete non sorvegliato: {ex.Message}");
            }
        }

        public async Task<string> ShrinkAsync(string dataUrl, int maxSide = 1600, double quality = 0.85)
        {
            try
            {
                return await (await ModuleAsync()).InvokeAsync<string>("shrink", dataUrl, maxSide, quality);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Foto non ridotta: {ex.Message}");
                return dataUrl;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_module == null)
                return;

            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Pagina gia' chiusa: niente da rilasciare.
            }
        }
    }
}
