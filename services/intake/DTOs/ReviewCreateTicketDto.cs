namespace Intake.DTOs;

// Request body for POST /internal/tickets on the Review service.
// Field names and constraints match Review's CreateTicketRequest exactly.
public class ReviewCreateTicketDto
{
    // Idempotency key — Intake generates a fresh Guid before every call.
    // Review uses this to detect duplicate submissions and replay them safely.
    public Guid RequestId { get; set; }

    public int CustomerId { get; set; }

    // "Individual" or "Joint"
    public string AccountType { get; set; } = string.Empty;

    public int? JointCustomerId { get; set; }

    // Required for Joint accounts — a display snapshot of the second holder's name.
    public string? JointCustomerName { get; set; }

    // Profile snapshot — Review stores this as immutable JSON; no FK to Intake's DB.
    public ReviewCustomerSnapshotDto Customer { get; set; } = new();

    // "Low", "Medium", or "High" — must match the flags (Review validates the combination).
    public string RiskRating { get; set; } = string.Empty;

    // Violation-type strings from Risk. No database IDs or timestamps — Review creates those.
    public string[] Flags { get; set; } = Array.Empty<string>();

    public ReviewVendorAssessmentDto VendorResponse { get; set; } = new();
}

// Snapshot of the customer profile fields that Review stores with the ticket.
// Field names match Review's CustomerDetailsDto exactly.
public class ReviewCustomerSnapshotDto
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string TaxIdType { get; set; } = string.Empty;
    public string ResidentialAddress { get; set; } = string.Empty;
    public string CountryOfResidence { get; set; } = string.Empty;
    public string CountryOfCitizenship { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string Employer { get; set; } = string.Empty;
}

// Vendor assessment fields forwarded from Risk's response to Review.
// Field names match Review's VendorAssessment exactly.
public class ReviewVendorAssessmentDto
{
    public string NameMatch { get; set; } = string.Empty;
    public string DateOfBirthMatch { get; set; } = string.Empty;
    public string AddressMatch { get; set; } = string.Empty;
    public string TaxIdMatch { get; set; } = string.Empty;
    public int Score { get; set; }
    public string RiskCodes { get; set; } = string.Empty;
}
