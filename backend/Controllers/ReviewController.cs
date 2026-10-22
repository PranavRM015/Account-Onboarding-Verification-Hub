using AdminRepo;
using AuditTrails;
using Data;
using Decisions;
using Flags;
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

    public class ClearFlagInformation
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RejectInformation
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
                using var transaction = _db.Database.BeginTransaction();

                if (_db.Flags.Any(f => f.TicketId == ticketId && f.ClearedAt == null))

                {
                    return Conflict("Every flag on the ticket must be cleared before it can be approved.");
                }
                if (ticket.RiskRating == "High")
                {
                    if (ticket.FirstApprovedBy is null)
                    {
                        ticket.FirstApprovedBy = approver;
                        _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Submitted", "Review",
                            "First approval recorded", reason));
                        _db.SaveChanges();
                        transaction.Commit();
                        return Ok(new { message = "First approval recorded. A different reviewer must give the second approval.", ticket.TicketId });
                    }
                    if (string.Equals(ticket.FirstApprovedBy, approver, StringComparison.OrdinalIgnoreCase))
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

        [HttpGet("{ticketId:int}")]
        public IActionResult readTicket(int ticketId)
        {
            var approver = User.Identity?.Name;
            if (string.IsNullOrEmpty(approver))
            {
                return Unauthorized("You must be logged in.");
            }
            try
            {
                var ticket = _db.Tickets.AsNoTracking().FirstOrDefault(t => t.TicketId == ticketId);
                if (ticket is null)
                {
                    return NotFound("That ticket does not exist.");
                }
                var flags = _db.Flags.AsNoTracking().Where(f => f.TicketId == ticketId)
                    .Select(f => new
                    {
                        f.FlagId, f.ViolationType, f.RaisedAt,
                        Cleared = f.ClearedAt != null,f.ClearedAt,
                        ClearedBy = f.ClearedByAdmin != null ? f.ClearedByAdmin.Username : null,f.ClearedReason,
                    })
                    .ToList();
                var vendorResponse = _db.VendorResponses.AsNoTracking()
                    .Where(v => v.TicketId == ticketId).OrderByDescending(v => v.VRTimeStamp)
                    .Select(v => new { v.NameMatch, v.DateOfBirthMatch, v.AddressMatch, v.TaxIdMatch, v.Score, v.RiskCodes, v.VRTimeStamp })
                    .FirstOrDefault();
                var decision = _db.Decisions.AsNoTracking()
                    .Where(d => d.TicketId == ticketId).Select(d => new { d.DecisionType, d.DecidedBy, d.Reason, d.DecidedAt })
                    .FirstOrDefault();
                return Ok(new
                {
                    ticket.TicketId,ticket.CustomerId,ticket.TicketStatus,
                    ticket.RiskRating,ticket.AccountType,
                    ticket.CreatedAt,ticket.VerificationDeadline,
                    ticket.AssignedTo,
                    ticket.FirstApprovedBy,
                    verified = flags.Count == 0,
                    flags, vendorResponse,decision,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read ticket {TicketId}.", ticketId);
                return Problem("Could not read the ticket.");
            }
        }

        [HttpPost("{ticketId:int}/flags/{flagId:int}/clear")]
        public IActionResult ClearFlag(int ticketId, int flagId, [FromBody] ClearFlagInformation info)
        {
            var clearer = User.Identity?.Name;
            if (string.IsNullOrEmpty(clearer))
            {
                return Unauthorized("You must be logged in.");
            }
            var admin = _db.Admins.FirstOrDefault(a => a.Username == clearer);
            if (admin is null)
            {
                return Unauthorized("Your admin account no longer exists.");
            }
            var reason = info.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0 || reason.Length > 500)
            {
                return BadRequest("A reason (max 500 characters) is required.");
            }
            try
            {
                var flag = _db.Flags.FirstOrDefault(f => f.FlagId == flagId && f.TicketId == ticketId);
                if (flag is null)
                {
                    return NotFound("That flag does not exist on this ticket.");
                }
                var ticket = _db.Tickets.FirstOrDefault(t => t.TicketId == ticketId);
                if (ticket is null)
                {
                    return NotFound("That ticket does not exist.");
                }
                if (ticket.TicketStatus != "Review")
                {
                    return Conflict("Flags can only be cleared on tickets in Review.");
                }
                if (flag.ClearedAt is not null)
                {
                    return Conflict("That flag has already been cleared.");
                }
                using var transaction = _db.Database.BeginTransaction();
                flag.ClearedBy = admin.AdminId;
                flag.ClearedAt = DateTime.UtcNow;
                flag.ClearedReason = reason;
                var flagValidation = new FlagValidator().Validate(flag);
                if (!flagValidation.IsValid)
                {
                    return BadRequest(string.Join(" ", flagValidation.Errors.Select(e => e.ErrorMessage)));
                }
                _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Review", "Review",
                    $"{flag.ViolationType} flag cleared", reason));
                _db.SaveChanges();
                transaction.Commit();
                return Ok(new { message = "Flag cleared", ticket.TicketId, flag.FlagId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear flag {FlagId} on ticket {TicketId}.", flagId, ticketId);
                return Problem("Could not clear the flag.");
            }
        }

        [HttpGet]
        public IActionResult listTickets()
        {
            var reviewer = User.Identity?.Name;
            if (string.IsNullOrEmpty(reviewer))
            {
                return Unauthorized("You must be logged in.");
            }
            try
            {
                var tickets = _db.Tickets.AsNoTracking()
                    .Where(t => t.TicketStatus == "Review")
                    .OrderBy(t => t.VerificationDeadline)
                    .Select(t => new
                    {
                        t.TicketId,
                        CustomerName = t.Customer.FirstName + " " + t.Customer.LastName,
                        t.RiskRating,
                        t.VerificationDeadline,
                    })
                    .ToList();
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list tickets.");
                return Problem("Could not list the tickets.");
            }
        }

        [HttpPost("{ticketId:int}/reject")]
        public IActionResult Reject(int ticketId, [FromBody] RejectInformation info)
        {
            var rejecter = User.Identity?.Name;
            if (string.IsNullOrEmpty(rejecter))
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
                    return Conflict("Only tickets in Review can be rejected.");
                }
                using var transaction = _db.Database.BeginTransaction();
                var decision = new Decision(ticket.TicketId, "Reject", rejecter, reason);
                var decisionValidation = new DecisionValidator().Validate(decision);
                if (!decisionValidation.IsValid)
                {
                    return BadRequest(string.Join(" ", decisionValidation.Errors.Select(e => e.ErrorMessage)));
                }
                ticket.TicketStatus = "Rejected";
                _db.Decisions.Add(decision);
                _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Review", "Rejected",
                    "Ticket rejected", reason));
                _db.SaveChanges();
                transaction.Commit();
                return Ok(new { message = "Ticket rejected", ticket.TicketId });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                _db.ChangeTracker.Clear();
                return Conflict("That ticket already has a decision.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reject ticket {TicketId}.", ticketId);
                return Problem("Could not reject the ticket.");
            }
        }
    }
}
