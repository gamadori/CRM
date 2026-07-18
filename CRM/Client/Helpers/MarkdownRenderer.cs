using Markdig;
using Microsoft.AspNetCore.Components;

namespace CRM.Client.Helpers
{
    /// <summary>
    /// Rendering Markdown → HTML per le risposte dell'assistente AI.
    /// L'HTML grezzo nel testo sorgente è disabilitato (viene mostrato come testo):
    /// il contenuto arriva da un modello che elabora dati del CRM, non deve poter
    /// iniettare markup arbitrario nella pagina.
    /// </summary>
    public static class MarkdownRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()   // tabelle pipe, liste attività, autolink…
            .DisableHtml()
            .Build();

        public static MarkupString ToHtml(string? markdown)
            => string.IsNullOrWhiteSpace(markdown)
                ? default
                : (MarkupString)Markdown.ToHtml(markdown, Pipeline);
    }
}
