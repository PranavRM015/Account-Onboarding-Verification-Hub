using AdminRepo;
using AuditTrails;
using Data;
using Decisions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ReviewControlNamespace
{
    public class ApproveInformation
    {
        public string Reason { get; set; } = string.Empty;
    }

    [ApiController]
    [Authorize]
    [Route("api/admins/tickets")]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly string _connectionString;
        private readonly ILogger<ReviewController> _logger;

        public ReviewController(AppDbContext db, IConfiguration configuration, ILogger<ReviewController> logger)
        {
            _db = db;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            _logger = logger;
        }

        [HttpPost("{ticketId:int}/approve")]
        public IActionResult Approve(int ticketId, [FromBody] ApproveInformation info)
        {
            var approver = User.Identity?.Name;
            if (string.IsNullOrEmpty(approver))
            {
                return Unauthorized("You must be logged in.");
            }
            var reason = info.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0 || reason.Length > 500)
            {
                return BadRequest("A reason (max 500 characters) is required.");
            }
            try
            {
                var ticket = _db.Tickets.FirstOrDefault(t => t.TicketId == ticketId);
                if (ticket is null)
                {
                    return NotFound("That ticket does not exist.");
                }
                if (ticket.TicketStatus != "Review")
                {
                    return Conflict("Only tickets in Review can be approved.");
                }
                if (ticket.RiskRating is null)
                {
                    return Conflict("The ticket has not been risk rated yet.");
                }
                var adminRepository = new AdminRepository(_connectionString);
                if (!adminRepository.CheckRemainingFlagCount(ticketId))
                {
                    return Conflict("Every flag on the ticket must be cleared before it can be approved.");
                }

                using var transaction = _db.Database.BeginTransaction();
                if (ticket.RiskRating == "High")
                {
                    if (ticket.FirstApprovedBy is null)
                    {
                        ticket.FirstApprovedBy = approver;
                        _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Review", "Review",
                            "First approval recorded", reason));
                        _db.SaveChanges();
                        transaction.Commit();
                        return Ok(new { message = "First approval recorded. A different reviewer must give the second approval.", ticket.TicketId });
                    }
                    if (ticket.FirstApprovedBy == approver)
                    {
                        return Conflict("A different reviewer must give the second approval.");
                    }
                }

                var decision = new Decision(ticket.TicketId, "Approve", approver, reason);
                var decisionValidation = new DecisionValidator().Validate(decision);
                if (!decisionValidation.IsValid)
                {
                    return BadRequest(string.Join(" ", decisionValidation.Errors.Select(e => e.ErrorMessage)));
                }
                ticket.TicketStatus = "Approved";
                _db.Decisions.Add(decision);
                _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Review", "Approved",
                    "Ticket approved", reason));
                _db.SaveChanges();
                transaction.Commit();
                return Ok(new { message = "Ticket approved", ticket.TicketId });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                _db.ChangeTracker.Clear();
                return Conflict("That ticket already has a decision.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to approve ticket {TicketId}.", ticketId);
                return Problem("Could not approve the ticket.");
            }
        }
    }
}
