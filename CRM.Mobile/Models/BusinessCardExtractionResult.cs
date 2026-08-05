namespace CRM.Mobile.Models;

public sealed class BusinessCardExtractionResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? FullName { get; set; }

    public string? CompanyName { get; set; }

    public string? JobTitle { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public float? AverageConfidence { get; set; }

    public long ProcessingTimeMs { get; set; }
}
