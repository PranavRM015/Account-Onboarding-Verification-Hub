namespace Intake.DTOs;

// Response body from POST /internal/tickets and GET /internal/tickets/by-customer/{id}.
// Fields match the TicketResponse anonymous object returned by InternalTicketsController.
public class ReviewTicketResponseDto
{
    public string Message { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int TicketId { get; set; }
    public string? RiskRating { get; set; }
    public bool Verified { get; set; }
    public string[] Flags { get; set; } = Array.Empty<string>();
}
