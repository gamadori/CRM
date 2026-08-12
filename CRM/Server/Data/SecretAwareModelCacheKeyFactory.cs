using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CRM.Server.Data
{
    /// <summary>
    /// EF costruisce il modello una volta sola e poi lo riusa per tutti i contesti dello stesso
    /// tipo. Qui il modello <b>non</b> e' sempre lo stesso: contiene o non contiene il convertitore
    /// che cifra i segreti, a seconda di come e' stato costruito il contesto.
    /// <para>
    /// Senza questa chiave vincerebbe il primo modello costruito nel processo: un contesto aperto
    /// per leggere i valori grezzi si ritroverebbe il convertitore attivo (e decifrerebbe cio' che
    /// voleva vedere cifrato), oppure - peggio - un contesto di produzione erediterebbe il modello
    /// in chiaro di un contesto di servizio e scriverebbe le password senza cifrarle.
    /// </para>
    /// </summary>
    public sealed class SecretAwareModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(),
                (context as ApplicationDbContext)?.SecretsProtected ?? false,
                designTime);
    }
}
