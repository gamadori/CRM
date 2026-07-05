namespace CRM.Server.Services
{
    /// <summary>Esito dell'invio a SdI tramite provider.</summary>
    public record EInvoiceSendResult(bool Success, string? ProviderReference, string? Message);

    /// <summary>
    /// Astrazione del provider di fatturazione elettronica (SdI). La parte indipendente del CRM
    /// genera l'XML FatturaPA; la trasmissione/firma/conservazione sono delegate a un adapter
    /// specifico del provider adottato dall'azienda (Aruba, InfoCert, TeamSystem, Zucchetti, ...).
    /// </summary>
    public interface IEInvoiceProvider
    {
        /// <summary>Nome del provider (per log/UI).</summary>
        string Name { get; }

        /// <summary>True se il provider e' configurato e pronto a trasmettere.</summary>
        bool IsConfigured { get; }

        /// <summary>Trasmette l'XML FatturaPA a SdI tramite il provider.</summary>
        Task<EInvoiceSendResult> SendAsync(string fatturaPaXml, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provider di default: nessuna integrazione configurata. Permette al resto del sistema di
    /// funzionare (generazione/anteprima XML) finche' non si registra un adapter reale.
    /// </summary>
    public class NullEInvoiceProvider : IEInvoiceProvider
    {
        public string Name => "Nessuno";

        public bool IsConfigured => false;

        public Task<EInvoiceSendResult> SendAsync(string fatturaPaXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new EInvoiceSendResult(
                false,
                null,
                "Nessun provider di fatturazione elettronica configurato. Registrare un adapter IEInvoiceProvider."));
    }
}
