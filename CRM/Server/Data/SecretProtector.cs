using System;
using Microsoft.AspNetCore.DataProtection;

namespace CRM.Server.Data
{
    /// <summary>
    /// Cifratura dei segreti salvati in chiaro sul database (password SMTP/IMAP, chiavi dei
    /// provider, token dei webhook). Serve a rendere inutile un backup rubato: senza le chiavi di
    /// cifratura quelle colonne non dicono niente.
    /// </summary>
    public interface ISecretProtector
    {
        /// <summary>Vero se la cifratura e' attiva. Falso solo nel banco di prova.</summary>
        bool Enabled { get; }

        string Protect(string value);

        string Unprotect(string value);
    }

    /// <summary>
    /// Cifratore vero, appoggiato a Data Protection di ASP.NET Core.
    /// <para>
    /// I valori cifrati portano il prefisso <c>enc:v1:</c>. Il marcatore fa due lavori che senza di
    /// lui costerebbero entrambi cari: distingue <b>a colpo d'occhio</b> una riga gia' cifrata da
    /// una rimasta in chiaro - cosi' la conversione delle righe vecchie non deve tentare di
    /// decifrare e leggere il fallimento come risposta - e lascia aperta la strada a un
    /// <c>v2</c> il giorno in cui si cambiasse algoritmo o si ruotassero le chiavi.
    /// </para>
    /// </summary>
    public sealed class DataProtectionSecretProtector : ISecretProtector
    {
        /// <summary>Marcatore di valore cifrato. Non cambiarlo: le righe salvate lo contengono.</summary>
        public const string Prefix = "enc:v1:";

        /// <summary>
        /// Scopo della protezione. Fa parte della derivazione della chiave: cambiarlo rende
        /// illeggibile tutto quello che e' gia' stato cifrato.
        /// </summary>
        private const string Purpose = "CRM.Secrets.v1";

        private readonly IDataProtector _protector;

        public DataProtectionSecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public bool Enabled => true;

        public string Protect(string value)
        {
            if (string.IsNullOrEmpty(value) || IsProtected(value))
                return value;

            return Prefix + _protector.Protect(value);
        }

        public string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Nessun marcatore: e' una riga scritta prima della cifratura, si legge com'e'.
            // Il convertitore la ricifra al primo salvataggio, e il passaggio all'avvio le
            // sistema tutte insieme.
            if (!IsProtected(value))
                return value;

            try
            {
                return _protector.Unprotect(value.Substring(Prefix.Length));
            }
            catch (Exception)
            {
                // Chiavi diverse da quelle con cui si e' cifrato: il valore non e' recuperabile.
                // Si risponde "non c'e'" invece di restituire il testo cifrato, perche' un segreto
                // illeggibile spacciato per buono verrebbe usato come password e fallirebbe con un
                // errore che non c'entra niente. Vuoto, invece, la maschera lo mostra come "nessuna
                // password salvata" e l'amministratore la reinserisce.
                return string.Empty;
            }
        }

        public static bool IsProtected(string? value)
            => value != null && value.StartsWith(Prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Cifratore spento: restituisce i valori come sono. Esiste per i test e per la lettura del
    /// valore <b>grezzo</b> durante la conversione delle righe vecchie - non va usato altrove, ed
    /// e' il motivo per cui <see cref="ISecretProtector.Enabled"/> esiste.
    /// </summary>
    public sealed class DisabledSecretProtector : ISecretProtector
    {
        public static readonly DisabledSecretProtector Instance = new();

        public bool Enabled => false;

        public string Protect(string value) => value;

        public string Unprotect(string value) => value;
    }
}
