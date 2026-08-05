namespace CRM.Mobile.Models;

/// <summary>
/// Un biglietto raccolto e non ancora arrivato al CRM.
/// <para>
/// Il patto della coda: il biglietto entra qui <b>prima</b> di qualunque tentativo di rete e ne
/// esce solo quando il server ha confermato. Fra le due cose ci puo' stare una fiera intera senza
/// campo, e non deve fare differenza.
/// </para>
/// </summary>
public sealed class PendingLead
{
    /// <summary>
    /// Identificativo locale, mandato al CRM come <c>ClientId</c>: se l'invio riesce ma la
    /// risposta si perde, il tentativo successivo non crea un doppione.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Note { get; set; }

    public int Score { get; set; } = 50;

    public int InitiativeId { get; set; }

    /// <summary>Nome della fiera al momento della cattura: serve solo a mostrare la coda con senso.</summary>
    public string? InitiativeName { get; set; }

    public string? PhotoPath { get; set; }

    public string? PhotoFileName { get; set; }

    /// <summary>
    /// Vero quando la lettura del biglietto non e' stata possibile allo stand (niente rete): al
    /// primo invio riuscito ci pensa il server, cosi' i campi vuoti si riempiono comunque.
    /// </summary>
    public bool NeedsOcr { get; set; }

    /// <summary>Quando e' stato raccolto. Il CRM data il lead con questa, non con l'ora dell'invio.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Quanti invii sono falliti: si mostra, cosi' un blocco silenzioso diventa visibile.</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public string Display
    {
        get
        {
            var who = string.IsNullOrWhiteSpace(Name) ? "Biglietto" : Name;
            var where = string.IsNullOrWhiteSpace(CompanyName) ? string.Empty : $" · {CompanyName}";
            return $"{who}{where} — {CreatedAt:dd/MM HH:mm}";
        }
    }
}
