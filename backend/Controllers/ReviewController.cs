using AdminRepo;
using AuditTrails;
using Data;
using Decisions;
using Flags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketDtos;
using Tickets;

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
    [Authorize(Roles = "Admin")]
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

        private const string TicketBusy = "Another reviewer is working on this ticket right now. Reload it and try again.";

        // Takes the ticket's row lock for the rest of the open transaction. NOWAIT: if another reviewer already holds it,
        // this fails at once (SQL error 1222) instead of queueing, so the second reviewer is turned away rather than waiting
        // to act on a ticket whose state the first one is changing. Call it first, then check the ticket's state.
        private Ticket? LockTicket(int ticketId) => _db.Tickets
            .FromSqlInterpolated($"SELECT * FROM Tickets WITH (UPDLOCK, ROWLOCK, NOWAIT) WHERE TicketId = {ticketId}")
            .AsEnumerable()
            .FirstOrDefault();

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
                using var transaction = _db.Database.BeginTransaction();
                var ticket = LockTicket(ticketId);
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
            catch (SqlException ex) when (ex.Number == 1222)
            {
                _db.ChangeTracker.Clear();
                return Conflict(TicketBusy);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                return Conflict("The ticket was changed by someone else. Reload it and try again.");
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
                var customer = _db.Customers.AsNoTracking()
                    .Where(c => c.CustomerId == ticket.CustomerId)
                    .Select(c => new CustomerDetailsDto
                    {
                        FirstName = c.FirstName,
                        MiddleName = c.MiddleName,
                        LastName = c.LastName,
                        DateOfBirth = c.DateOfBirth,
                        Gender = c.Gender,
                        TaxIdType = c.TaxIdType,
                        ResidentialAddress = c.ResidentialAddress,
                        CountryOfResidence = c.CountryOfResidence,
                        CountryOfCitizenship = c.CountryOfCitizenship,
                        Occupation = c.Occupation,
                        Employer = c.Employer,
                    })
                    .First();
                var jointCustomerName = ticket.JointCustomerId is null ? null : _db.Customers.AsNoTracking()
                    .Where(c => c.CustomerId == ticket.JointCustomerId)
                    .Select(c => c.FirstName + " " + c.LastName)
                    .FirstOrDefault();
                var flags = _db.Flags.AsNoTracking().Where(f => f.TicketId == ticketId)
                    .Select(f => new FlagDto
                    {
                        FlagId = f.FlagId,
                        ViolationType = f.ViolationType,
                        RaisedAt = f.RaisedAt,
                        Cleared = f.ClearedAt != null,
                        ClearedAt = f.ClearedAt,
                        ClearedBy = f.ClearedByAdmin != null ? f.ClearedByAdmin.Username : null,
                        ClearedReason = f.ClearedReason,
                    })
                    .ToList();
                var vendorResponse = _db.VendorResponses.AsNoTracking()
                    .Where(v => v.TicketId == ticketId).OrderByDescending(v => v.VRTimeStamp)
                    .Select(v => new VendorResponseDto
                    {
                        NameMatch = v.NameMatch,
                        DateOfBirthMatch = v.DateOfBirthMatch,
                        AddressMatch = v.AddressMatch,
                        TaxIdMatch = v.TaxIdMatch,
                        Score = v.Score,
                        RiskCodes = v.RiskCodes,
                        VRTimeStamp = v.VRTimeStamp,
                    })
                    .FirstOrDefault();
                var decision = _db.Decisions.AsNoTracking()
                    .Where(d => d.TicketId == ticketId)
                    .Select(d => new DecisionDto
                    {
                        DecisionType = d.DecisionType,
                        DecidedBy = d.DecidedBy,
                        Reason = d.Reason,
                        DecidedAt = d.DecidedAt,
                    })
                    .FirstOrDefault();
                return Ok(new TicketDetailsDto
                {
                    TicketId = ticket.TicketId,
                    CustomerId = ticket.CustomerId,
                    TicketStatus = ticket.TicketStatus,
                    RiskRating = ticket.RiskRating,
                    AccountType = ticket.AccountType,
                    CreatedAt = ticket.CreatedAt,
                    VerificationDeadline = ticket.VerificationDeadline,
                    AssignedTo = ticket.AssignedTo,
                    FirstApprovedBy = ticket.FirstApprovedBy,
                    Verified = flags.Count == 0,
                    Customer = customer,
                    JointCustomerName = jointCustomerName,
                    Flags = flags,
                    VendorResponse = vendorResponse,
                    Decision = decision,
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
                if (!_db.Flags.Any(f => f.FlagId == flagId && f.TicketId == ticketId))
                {
                    return NotFound("That flag does not exist on this ticket.");
                }
                using var transaction = _db.Database.BeginTransaction();
                var ticket = LockTicket(ticketId);
                if (ticket is null)
                {
                    return NotFound("That ticket does not exist.");
                }
                // Read the flag only now that the ticket is locked, so a clear by another reviewer is seen.
                var flag = _db.Flags.First(f => f.FlagId == flagId && f.TicketId == ticketId);
                if (ticket.TicketStatus != "Review")
                {
                    return Conflict("Flags can only be cleared on tickets in Review.");
                }
                if (flag.ClearedAt is not null)
                {
                    return Conflict("That flag has already been cleared.");
                }
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
            catch (SqlException ex) when (ex.Number == 1222)
            {
                _db.ChangeTracker.Clear();
                return Conflict(TicketBusy);
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
                using var transaction = _db.Database.BeginTransaction();
                var ticket = LockTicket(ticketId);
                if (ticket is null)
                {
                    return NotFound("That ticket does not exist.");
                }
                if (ticket.TicketStatus != "Review")
                {
                    return Conflict("Only tickets in Review can be rejected.");
                }
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
            catch (SqlException ex) when (ex.Number == 1222)
            {
                _db.ChangeTracker.Clear();
                return Conflict(TicketBusy);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                return Conflict("The ticket was changed by someone else. Reload it and try again.");
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
