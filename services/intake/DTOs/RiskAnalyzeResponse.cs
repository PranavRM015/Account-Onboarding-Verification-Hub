namespace Intake.DTOs;

// Response body from POST /internal/analyze on the Risk service.
// Fields match Risk's AnalyzeResponse and VendorResult exactly.
// flags is a plain string array of violation types — no database IDs or timestamps.
// Review is responsible for persisting these as Flag and VendorResponse rows.
public class RiskAnalyzeResponse
{
    public string RiskRating { get; set; } = string.Empty;
    public string[] Flags { get; set; } = Array.Empty<string>();
    public RiskVendorResult VendorResponse { get; set; } = new();
}

public class RiskVendorResult
{
    public string NameMatch { get; set; } = string.Empty;
    public string DateOfBirthMatch { get; set; } = string.Empty;
    public string AddressMatch { get; set; } = string.Empty;
    public string TaxIdMatch { get; set; } = string.Empty;
    public int Score { get; set; }
    public string RiskCodes { get; set; } = string.Empty;
}
