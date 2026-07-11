using System.Collections.Generic;
using System.Linq;

namespace CRM.Shared
{
    /// <summary>Segnaposto disponibile in un template email (token + descrizione per la UI).</summary>
    public sealed record EmailPlaceholder(string Token, string Description);

    /// <summary>
    /// Catalogo unico dei segnaposto dei template email: sorgente di verità per il rendering
    /// (server) e per la legenda mostrata all'admin (client). I token seguono la convenzione
    /// storica <c>$NOME</c> (dollaro + maiuscolo).
    /// </summary>
    public static class EmailPlaceholders
    {
        public static readonly IReadOnlyList<EmailPlaceholder> All = new List<EmailPlaceholder>
        {
            new("$NAME", "Nome del destinatario"),
            new("$COMPANY", "Azienda"),
            new("$TICKET", "Numero del ticket"),
            new("$URL", "Link / URL"),
            new("$DATE", "Data e ora"),
        };

        public static readonly IReadOnlySet<string> Tokens = All.Select(p => p.Token).ToHashSet();
    }
}
