namespace Intake.DTOs;

// Request body for POST /api/admins/inputForm
// Field names match exactly what Angular's inputForm.ts component sends.
// The admin fills in both the customer profile AND the customer's login details,
// because an admin-created customer never goes through self-signup.
public class InputFormDto
{
    // ── Profile fields ───────────────────────────────────────────────────────
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string taxIdType { get; set; } = string.Empty;
    public string taxIdNumber { get; set; } = string.Empty;
    public string residentialAddress { get; set; } = string.Empty;
    public string countryOfResidence { get; set; } = string.Empty;
    public string countryOfCitizenship { get; set; } = string.Empty;
    public string occupation { get; set; } = string.Empty;
    public string Employer { get; set; } = string.Empty;

    // ── Intake workflow fields ───────────────────────────────────────────────
    public string typeOfAccount { get; set; } = string.Empty;
    public int? jointCustomerId { get; set; }

    // ── Credential fields (Identity concern; Intake forwards these) ──────────
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string passwordRecoveryQuestion { get; set; } = string.Empty;
    public string passwordRecoveryAnswer { get; set; } = string.Empty;
}
