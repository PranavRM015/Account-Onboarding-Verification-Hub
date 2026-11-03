namespace Risk.Api.Contracts
{
    public record VendorResult(
        string NameMatch,
        string DateOfBirthMatch,
        string AddressMatch,
        string TaxIdMatch,
        int Score,
        string RiskCodes);

    // Flags are violation types, not database rows. The calling service creates the ticket and records attribution.
    public record AnalyzeResponse(string RiskRating, string[] Flags, VendorResult VendorResponse);
}
