using AnthropicMessage = Anthropic.Models.Messages.Message;
using AnthropicUsage = Anthropic.Models.Messages.Usage;

namespace CRM.Server.Services.Usage
{
    /// <summary>
    /// Traduce il consumo riportato dall'SDK Anthropic nel tipo neutro del registro.
    /// <para>
    /// Il dato arriva gia' in ogni risposta: fino a qui non lo leggeva nessuno.
    /// </para>
    /// </summary>
    public static class AnthropicUsageExtensions
    {
        public static TokenUsage ToTokenUsage(this AnthropicUsage? usage)
            => usage == null
                ? default
                : new TokenUsage(
                    usage.InputTokens,
                    usage.OutputTokens,
                    usage.CacheReadInputTokens ?? 0,
                    usage.CacheCreationInputTokens ?? 0);

        /// <summary>Consumo di una risposta completa (chiamata non in streaming).</summary>
        public static TokenUsage TokenUsageOf(this AnthropicMessage? message)
            => message?.Usage.ToTokenUsage() ?? default;
    }
}
