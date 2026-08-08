using CRM.Server.Services;

namespace CRM.Tests;

/// <summary>
/// La valuta letta dal TESTO del documento, non dai campi degli importi.
/// <para>
/// Il caso che ha portato a scrivere questi test e' un conto d'albergo americano in cui la valuta
/// e' stampata due volte - "Rate: $127.00" in testata e "Balance 0.00 USD" in fondo - e nessuna
/// delle due sta dentro un campo strutturato: il campo Total contiene "104.92" e basta. Risultato,
/// il documento risultava "valuta non rilevata" pur avendola scritta sopra.
/// </para>
/// </summary>
public class ReceiptCurrencyTextTests
{
    /// <summary>Il testo che l'OCR restituisce per quel conto, ridotto all'essenziale.</summary>
    private const string ContoContoso = """
        Contoso
        Contoso Inn
        5600 148th Ave NE,
        Redmond, WA 98052
        Telephone: 987-654-4321
        Alex Morgan                     Room: 515
        5600 148th Ave NE               Room Type: STQT
        Redmond, WA 98052               Number of Guests: 1
        Contoso                         Rate: $127.00        Clerk: MVB
        Arrive: 27Mar21  Time: 05:02PM  Depart: 28Mar21  Time: 02:52PM  Folio Number: 12345
        DATE      DESCRIPTION      CHARGES   CREDITS
        27Mar21   Room Charge        88.00
        27Mar21   County Tax 6%       5.28
        27Mar21   State Tax 6%        5.28
        27Mar21   Daily Parking       6.00
        27Mar21   Parking Tax         0.36
        28Mar21   American Express             104.92
        Total    104.92   104.92
        Balance    0.00 USD
        Contoso Account # XXXXX1234. Your Contoso points will be credited to your account.
        Visit Contoso.com
        """;

    [Fact]
    public void Il_codice_scritto_in_fondo_al_conto_viene_letto()
    {
        var (code, symbol) = ReceiptCurrencyText.Read(ContoContoso);

        Assert.Equal("USD", code);
        Assert.Equal("$", symbol);
    }

    [Fact]
    public void L_indirizzo_americano_propone_comunque_USD()
    {
        // Seconda strada per lo stesso documento: serve quando il codice non c'e' proprio.
        Assert.Equal("USD", ReceiptCurrencyText.GuessFromAddress("5600 148th Ave NE, Redmond, WA 98052"));
    }

    [Fact]
    public void Due_codici_diversi_nello_stesso_documento_non_decidono_niente()
    {
        // Uno dei due non e' la valuta della spesa (nota di cambio, condizioni di pagamento) e da
        // fuori non c'e' modo di sapere quale: meglio "da indicare" che una delle due a caso.
        var (code, _) = ReceiptCurrencyText.Read("Totale 120,00 EUR - controvalore 130,00 USD");

        Assert.Null(code);
    }

    [Fact]
    public void Le_sigle_che_non_sono_valute_non_vengono_scambiate_per_tali()
    {
        // "IVA" e' tre lettere maiuscole come "USD": il confronto sta su una lista chiusa.
        var (code, symbol) = ReceiptCurrencyText.Read("SCONTRINO FISCALE\nIVA 22%\nTOTALE 24,40");

        Assert.Null(code);
        Assert.Null(symbol);
    }

    [Fact]
    public void Un_documento_senza_codice_lascia_parlare_il_simbolo()
    {
        // Qui il codice non c'e': la valuta la conclude il simbolo, che per l'euro non è ambiguo.
        var (code, symbol) = ReceiptCurrencyText.Read("TOTALE €18,00");

        Assert.Null(code);
        Assert.Equal("€", symbol);
        Assert.Equal("EUR", ReceiptCurrencyText.UnambiguousSymbols[symbol]);
    }

    [Fact]
    public void Un_indirizzo_italiano_non_suggerisce_nessuna_valuta()
    {
        // Il CAP italiano e' a 5 cifre come quello americano: senza la sigla di stato davanti
        // ("WA 98052") la regola non deve scattare, altrimenti proporrebbe USD a mezza Italia.
        Assert.Null(ReceiptCurrencyText.GuessFromAddress("Via Roma 12, 40100 Bologna BO"));
    }
}
