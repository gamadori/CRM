using System.Threading;
using System.Threading.Tasks;
using CRM.Shared;

namespace CRM.Server.Services.Usage
{
    /// <summary>
    /// I token di una chiamata. E' un tipo a parte e non quello dell'SDK perche' il registro deve
    /// restare neutro rispetto al fornitore, e perche' i giri con piu' iterazioni (l'assistente e
    /// i suoi tool) devono poterli <b>sommare</b> prima di scrivere una riga sola.
    /// </summary>
    public readonly record struct TokenUsage(long Input, long Output, long CacheRead, long CacheWrite)
    {
        public bool IsEmpty => Input == 0 && Output == 0 && CacheRead == 0 && CacheWrite == 0;

        public static TokenUsage operator +(TokenUsage a, TokenUsage b)
            => new(a.Input + b.Input, a.Output + b.Output, a.CacheRead + b.CacheRead, a.CacheWrite + b.CacheWrite);
    }

    /// <summary>
    /// Registra i consumi dei servizi esterni a pagamento.
    /// <para>
    /// Ogni metodo e' <b>non bloccante per il chiamante</b>: qualunque cosa vada storta nella
    /// registrazione viene assorbita e loggata. Misurare una funzione non puo' essere il motivo
    /// per cui quella funzione smette di lavorare.
    /// </para>
    /// <para>
    /// Nessun metodo accetta un <see cref="CancellationToken"/>, e non e' una dimenticanza: quando
    /// il chiamante viene annullato - l'utente chiude la pagina a meta' risposta - i token sono
    /// gia' stati consumati e gia' fatturati. Propagare l'annullamento fin qui cancellerebbe la
    /// riga proprio delle chiamate piu' lunghe, cioe' le piu' care.
    /// </para>
    /// </summary>
    public interface IUsageRecorder
    {
        /// <summary>Una chiamata pagata a token (Claude).</summary>
        Task RecordTokensAsync(
            ExternalServiceFeature feature,
            string? model,
            TokenUsage tokens,
            bool success,
            long elapsedMs);

        /// <summary>Una chiamata pagata a unita': pagine analizzate, documenti, messaggi.</summary>
        Task RecordUnitsAsync(
            ExternalServiceProvider provider,
            ExternalServiceFeature feature,
            string operation,
            int units,
            bool success,
            long elapsedMs);
    }
}
