using CRM.Shared;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>
    /// Lo snapshot dell'utente collegato con i suoi permessi globali (api/UserSigned),
    /// caricato una volta sola e riusato per tutta la sessione.
    /// <para>
    /// I permessi qui dentro servono solo a decidere cosa mostrare: il controllo che conta
    /// resta lato server. Non aggiungere qui logica di autorizzazione, ma un campo in piu'
    /// allo snapshot quando al client manca un'informazione.
    /// </para>
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// L'utente collegato. La prima chiamata interroga il server, le successive
        /// restituiscono la copia in memoria. Null se il caricamento non riesce.
        /// <para>
        /// ⚠️ L'istanza e' condivisa da tutti i chiamanti: non modificarla. Chi deve cambiare
        /// i dati dell'utente passi dall'API e poi chiami <see cref="Invalidate"/>.
        /// </para>
        /// </summary>
        Task<ApplicationUser?> Get();

        /// <summary>
        /// Scarta la copia in cache: la prossima <see cref="Get"/> ricarica dal server.
        /// Da usare dopo aver cambiato ruoli o azienda dell'utente collegato.
        /// </summary>
        void Invalidate();
    }
}
