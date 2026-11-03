namespace Intake.DTOs;

// Request body for POST /api/users/signup
// Field names match exactly what the Angular user-signup.ts component sends.
public class CustomerSignupDto
{
    // ── Credentials (passed to Identity service; not stored by Intake) ──────
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string passwordRecoveryQuestion { get; set; } = string.Empty;
    public string passwordRecoveryAnswer { get; set; } = string.Empty;

    // ── Profile fields (Intake stores these) ────────────────────────────────
    public string firstName { get; set; } = string.Empty;
    public string? middleName { get; set; }
    public string lastName { get; set; } = string.Empty;
    public DateTime dateOfBirth { get; set; }
    public string gender { get; set; } = string.Empty;
    public string taxIdType { get; set; } = string.Empty;
    public string taxIdNumber { get; set; } = string.Empty;
    public string residentialAddress { get; set; } = string.Empty;
    public string countryOfResidence { get; set; } = string.Empty;
    public string countryOfCitizenship { get; set; } = string.Empty;
    public string occupation { get; set; } = string.Empty;
    public string employer { get; set; } = string.Empty;
}
