using System.Linq.Dynamic.Core;
using System.Reflection;

namespace CRM.Server.Services
{
    /// <summary>
    /// Ordinamento richiesto da una griglia Radzen. Le griglie ordinano per i nomi di proprieta' del
    /// DTO, i servizi interrogano l'entita': un criterio come "CompanyName asc" su Order non esiste,
    /// Dynamic LINQ solleva un'eccezione, il catch di FilterItems la inghiotte restituendo null e
    /// l'elenco sparisce invece di riordinarsi. Qui i nomi noti si traducono e gli altri si scartano:
    /// un ordinamento sbagliato, da qualunque client arrivi, al massimo non viene applicato.
    /// </summary>
    internal static class GridSort
    {
        /// <summary>
        /// Applica l'ordinamento chiesto dal client, tradotto e verificato. Se non resta alcun
        /// criterio valido si usa <paramref name="fallback"/>, l'ordinamento di default dell'elenco.
        /// </summary>
        internal static IQueryable<TEntity> Apply<TEntity>(
            IQueryable<TEntity> items,
            string? orderBy,
            IReadOnlyDictionary<string, string> columns,
            Func<IQueryable<TEntity>, IQueryable<TEntity>> fallback)
        {
            var translated = Translate<TEntity>(orderBy, columns);
            if (translated != null)
            {
                try
                {
                    return items.OrderBy(translated);
                }
                catch (Exception)
                {
                    // Criterio non applicabile all'entita': si prosegue con l'ordinamento di default.
                }
            }

            return fallback(items);
        }

        /// <summary>
        /// Converte "CompanyName asc, Date desc" nei percorsi dell'entita', un criterio alla volta.
        /// Restituisce null se non ne resta nessuno valido.
        /// </summary>
        internal static string? Translate<TEntity>(string? orderBy, IReadOnlyDictionary<string, string> columns)
        {
            if (string.IsNullOrWhiteSpace(orderBy))
                return null;

            var terms = new List<string>();
            foreach (var raw in orderBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var property = parts[0];
                var direction = parts.Length > 1 ? parts[1] : string.Empty;

                if (columns.TryGetValue(property, out var mapped))
                    property = mapped;
                else if (!IsEntityProperty<TEntity>(property))
                    continue;   // colonna che l'entita' non ha e che non si sa tradurre: si scarta

                terms.Add(string.IsNullOrEmpty(direction) ? property : $"{property} {direction}");
            }

            return terms.Count > 0 ? string.Join(", ", terms) : null;
        }

        /// <summary>
        /// True se il criterio parte da una proprieta' dell'entita'. Sui percorsi annidati
        /// (es. "Company.RagioneSociale") si controlla la radice: il resto lo valida Dynamic LINQ.
        /// </summary>
        private static bool IsEntityProperty<TEntity>(string property)
        {
            var root = property.Split('.')[0];
            return typeof(TEntity).GetProperty(
                root,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null;
        }
    }
}
