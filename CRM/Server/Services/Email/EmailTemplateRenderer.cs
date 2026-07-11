using System.Text.RegularExpressions;
using CRM.Shared;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Sostituzione dei segnaposto nel corpo/oggetto di un template email. Passata <b>singola</b>
    /// (evita cascate su valori che contengono a loro volta token) e <b>ancorata</b> al token intero
    /// (niente sostituzioni parziali tra prefissi). I segnaposto noti non forniti vengono resi vuoti
    /// (mai lasciati trapelare al destinatario); le sequenze <c>$PAROLA</c> non presenti nel catalogo
    /// restano invariate (sono contenuto, non token).
    /// </summary>
    public static class EmailTemplateRenderer
    {
        private static readonly Regex TokenRx = new(@"\$[A-Z]+", RegexOptions.Compiled);

        public static string Render(string? template, IReadOnlyDictionary<string, string>? values)
        {
            if (string.IsNullOrEmpty(template))
                return template ?? string.Empty;

            return TokenRx.Replace(template!, match =>
            {
                if (!EmailPlaceholders.Tokens.Contains(match.Value))
                    return match.Value; // non è un segnaposto: contenuto, da lasciare com'è

                return values != null && values.TryGetValue(match.Value, out var v)
                    ? v ?? string.Empty
                    : string.Empty; // segnaposto noto ma non fornito: reso vuoto
            });
        }
    }
}
