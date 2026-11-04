using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuditTrails;
using Data;
using Flags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Review.Api.Contracts;
using Tickets;
using VendorResponses;

namespace Review.Api.Controllers
{
    [ApiController]
    [Authorize(Roles = "admin")]
    [Route("internal/tickets")]
    public class InternalTicketsController : ControllerBase
    {
        private readonly ReviewDbContext _db;
        private readonly ILogger<InternalTicketsController> _logger;
        public InternalTicketsController(ReviewDbContext db, ILogger<InternalTicketsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private object TicketResponse(Ticket ticket) => new
        {
            message = "Customer saved", ticket.CustomerId, ticket.TicketId, ticket.RiskRating,
            verified = !_db.Flags.Any(f => f.TicketId == ticket.TicketId),
            flags = _db.Flags.Where(f => f.TicketId == ticket.TicketId).OrderBy(f => f.FlagId).Select(f => f.ViolationType).ToArray(),
            vendorResponse = _db.VendorResponses.Where(v => v.TicketId == ticket.TicketId).OrderByDescending(v => v.VRTimeStamp)
                .Select(v => new { v.Score, v.RiskCodes }).FirstOrDefault(),
        };

        [HttpGet("by-customer/{customerId:int}")]
        public IActionResult Find(int customerId)
        {
            var ticket = _db.Tickets.AsNoTracking().SingleOrDefault(t => t.CustomerId == customerId);
            return ticket is null ? NotFound("No ticket exists for that customer.") : Ok(TicketResponse(ticket));
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateTicketRequest request)
        {
            string? admin = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(admin) || admin.Length > 100
                || !int.TryParse(User.FindFirst("adminId")?.Value, out var adminId) || adminId <= 0)
            {
                return Unauthorized("A valid admin name and id are required in the token.");
            }
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request, admin, adminId }))));
            IActionResult Replay(Ticket existing) => existing.RequestHash == hash
                ? Ok(TicketResponse(existing)) : Conflict("That requestId was already used for different data or another admin.");
            try
            {
                var existing = _db.Tickets.AsNoTracking().SingleOrDefault(t => t.RequestId == request.RequestId);
                if (existing is not null) { return Replay(existing); }
                using var transaction = _db.Database.BeginTransaction();
                var ticket = new Ticket(request.CustomerId, request.AccountType, admin, request.JointCustomerId)
                {
                    TicketStatus = "Review", RiskRating = request.RiskRating,
                    CustomerJson = JsonSerializer.Serialize(request.Customer), JointCustomerName = request.JointCustomerName,
                    RequestId = request.RequestId, RequestHash = hash,
                };
                _db.Tickets.Add(ticket);
                _db.SaveChanges();
                var vendor = request.VendorResponse;
                _db.VendorResponses.Add(new VendorResponse(ticket.TicketId, vendor.NameMatch, vendor.DateOfBirthMatch,
                    vendor.AddressMatch, vendor.TaxIdMatch, vendor.Score, vendor.RiskCodes));
                _db.Flags.AddRange(request.Flags.Select(type => new Flag(ticket.TicketId, type, adminId)));
                _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Submitted", "Review", "Ticket created", "Submission received"));
                _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, ticket.CustomerId, "Review", "Review", "Validation pipeline completed",
                    $"{request.Flags.Length} flag(s) raised; risk rating {ticket.RiskRating}"));
                _db.SaveChanges();
                transaction.Commit();
                return Ok(TicketResponse(ticket));
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                _db.ChangeTracker.Clear();
                var existing = _db.Tickets.AsNoTracking().SingleOrDefault(t => t.RequestId == request.RequestId);
                return existing is not null ? Replay(existing) : Conflict("That customer already has an onboarding ticket.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open ticket for customer {CustomerId}.", request.CustomerId);
                return Problem("Could not create the ticket.");
            }
        }
    }
}
