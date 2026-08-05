namespace CRM.Mobile.Models;

/// <summary>Una fiera fra cui scegliere. Rispecchia <c>FieldInitiativeDTO</c> lato CRM.</summary>
public sealed class FieldInitiative
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Location { get; set; }

    public DateTime DateFrom { get; set; }

    public DateTime DateTo { get; set; }

    public bool IsCurrent { get; set; }

    /// <summary>Come compare nell'elenco a tendina: nome, luogo e periodo bastano a distinguerle.</summary>
    public string Display => string.IsNullOrWhiteSpace(Location)
        ? $"{Name} ({DateFrom:dd/MM} - {DateTo:dd/MM})"
        : $"{Name} · {Location} ({DateFrom:dd/MM} - {DateTo:dd/MM})";

    public override string ToString() => Display;
}

/// <summary>Esito della verifica di configurazione.</summary>
public sealed class FieldPingResponse
{
    public bool Ok { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? KeyName { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Il biglietto come viaggia verso il CRM.</summary>
public sealed class FieldLeadRequest
{
    public int IdInitiative { get; set; }

    public string? Name { get; set; }

    public string? CompanyName { get; set; }

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Note { get; set; }

    public int Score { get; set; } = 50;

    public DateTime? CapturedAt { get; set; }

    public string? ClientId { get; set; }

    public bool AutoFillFromCard { get; set; }
}

public sealed class FieldLeadResponse
{
    public bool Ok { get; set; }

    public int IdLead { get; set; }

    public string? Message { get; set; }

    public bool Duplicate { get; set; }
}
