namespace CRM.Shared
{
    /// <summary>
    /// Rappresenta un elemento del breadcrumb
    /// </summary>
    public class BreadcrumbItem
    {
        /// <summary>
        /// Testo visualizzato nel breadcrumb
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// URL di navigazione (opzionale, se null l'elemento non è cliccabile)
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Costruttore vuoto
        /// </summary>
        public BreadcrumbItem() { }

        /// <summary>
        /// Costruttore con parametri
        /// </summary>
        /// <param name="text">Testo del breadcrumb</param>
        /// <param name="url">URL di navigazione (opzionale)</param>
        public BreadcrumbItem(string text, string? url = null)
        {
            Text = text;
            Url = url;
        }
    }
}
