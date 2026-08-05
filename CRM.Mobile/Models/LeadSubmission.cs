namespace CRM.Mobile.Models;

public sealed class LeadSubmission
{
    public string Name { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int Source { get; set; } = 7;

    public int Status { get; set; } = 0;

    public int Score { get; set; }

    public string? Note { get; set; }

    public int? IdInitiative { get; set; }

    public int? IdBusinessCard { get; set; }

    public DateTime CreatedAt { get; set; }
}
