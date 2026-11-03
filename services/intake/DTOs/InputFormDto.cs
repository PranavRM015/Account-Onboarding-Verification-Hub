namespace Intake.DTOs;

// Request body for POST /api/admins/inputForm.
// Field names match what Angular's inputForm.ts sends.
// The admin provides both profile and credential fields because an admin-created
// customer does not go through self-signup.
public class InputFormDto
{
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
    public string typeOfAccount { get; set; } = string.Empty;
    public int? jointCustomerId { get; set; }

    // Credentials — forwarded to Identity.
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string passwordRecoveryQuestion { get; set; } = string.Empty;
    public string passwordRecoveryAnswer { get; set; } = string.Empty;
}
