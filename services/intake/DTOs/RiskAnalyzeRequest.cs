namespace Intake.DTOs;

// Request body for POST /internal/analyze on the Risk service.
// Fields and constraints match Risk's AnalyzeRequest exactly.
// No credentials, full tax ID, customer ID, or ticket ID are included.
public class RiskAnalyzeRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    // Exactly 4 digits — computed by Intake from the submitted tax ID.
    public string TaxIdLastFourDigits { get; set; } = string.Empty;
    public string ResidentialAddress { get; set; } = string.Empty;
    // 2 uppercase letters — Intake normalises these before sending.
    public string CountryOfResidence { get; set; } = string.Empty;
    public string CountryOfCitizenship { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string Employer { get; set; } = string.Empty;
}
