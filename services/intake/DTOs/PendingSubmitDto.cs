namespace Intake.DTOs;

// Request body for POST /api/admins/pending-customers/{customerId}/submit
// Field names match exactly what Angular's pending-onboarding.ts sends.
public class PendingSubmitDto
{
    public string typeOfAccount { get; set; } = string.Empty;
    public int? jointCustomerId { get; set; }
}
