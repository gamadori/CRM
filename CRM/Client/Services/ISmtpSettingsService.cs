using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared;

namespace CRM.Client.Services
{
    public interface ISmtpSettingsService : IDataService<SmtpSetting>
    {
        /// <summary>Tutti i canali, in ordine di priorità.</summary>
        Task<List<SmtpSetting>> GetListAsync();

        /// <summary>Singolo canale per id (per la pagina di modifica).</summary>
        Task<SmtpSetting?> GetItemAsync(int id);

        Task<bool> DeleteAsync(int id);

        /// <summary>Riassegna le priorità secondo l'ordine degli id indicato.</summary>
        Task<bool> ReorderAsync(List<int> orderedIds);
    }
}
