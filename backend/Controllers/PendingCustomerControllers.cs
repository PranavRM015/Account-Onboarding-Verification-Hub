using AccountSecurity;
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tickets;
using TicketIntakeNamespace;

namespace PendingCustomerNamespace
{
    // The admin picks the account type; the customer never chose one, so it is not part of what they signed up with.
    public class PendingSubmitInformation
    {
        public string typeOfAccount { get; set; } = string.Empty;
        public int? jointCustomerId { get; set; }
    }

    // Customers who signed themselves up but have no ticket yet. Every admin sees the same list, oldest first, and opening one
    // shows exactly what the customer entered, so an admin only adds the account type and opens the ticket.
    [ApiController]
    [Authorize(Roles = Roles.Admin)]
    [Route("api/admins/pending-customers")]
    public class PendingCustomersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PendingCustomersController> _logger;

        public PendingCustomersController(AppDbContext db, ILogger<PendingCustomersController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // The database stores UTC without saying so; marking it lets the browser show the time in the viewer's own zone.
        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var rows = await _db.Customers
                .Where(c => !_db.Tickets.Any(t => t.CustomerId == c.CustomerId))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.CustomerId)
                .Select(c => new { c.CustomerId, c.FirstName, c.MiddleName, c.LastName, c.CreatedAt })
                .ToListAsync();
            return Ok(rows.Select(c => new
            {
                customerId = c.CustomerId,
                firstName = c.FirstName,
                middleName = c.MiddleName,
                lastName = c.LastName,
                createdAt = Utc(c.CreatedAt),
            }));
        }

        // Only what the customer gave. The full tax ID is never stored (just a hash and the last four digits), and the password,
        // recovery answer and hashes are never sent.
        [HttpGet("{customerId:int}")]
        public async Task<IActionResult> Details(int customerId)
        {
            var customer = await _db.Customers
                .Where(c => c.CustomerId == customerId)
                .Select(c => new
                {
                    c.CustomerId, c.FirstName, c.MiddleName, c.LastName, c.DateOfBirth, c.Gender, c.TaxIdType,
                    c.TaxIdLastFourDigits, c.ResidentialAddress, c.CountryOfResidence, c.CountryOfCitizenship,
                    c.Occupation, c.Employer, c.CreatedAt,
                })
                .FirstOrDefaultAsync();
            if (customer is null)
            {
                return NotFound("That customer does not exist.");
            }
            if (await _db.Tickets.AnyAsync(t => t.CustomerId == customerId))
            {
                return Conflict("That customer has already been onboarded.");
            }
            return Ok(new
            {
                customerId = customer.CustomerId,
                firstName = customer.FirstName,
                middleName = customer.MiddleName,
                lastName = customer.LastName,
                dateOfBirth = customer.DateOfBirth.ToString("yyyy-MM-dd"),
                gender = customer.Gender,
                taxIdType = customer.TaxIdType,
                taxIdLastFourDigits = customer.TaxIdLastFourDigits,
                residentialAddress = customer.ResidentialAddress,
                countryOfResidence = customer.CountryOfResidence,
                countryOfCitizenship = customer.CountryOfCitizenship,
                occupation = customer.Occupation,
                employer = customer.Employer,
                createdAt = Utc(customer.CreatedAt),
            });
        }

        // Opens the ticket for the customer. Nothing about the customer changes, including their login.
        [HttpPost("{customerId:int}/submit")]
        public async Task<IActionResult> Submit(int customerId, [FromBody] PendingSubmitInformation info)
        {
            var assignedTo = User.Identity?.Name;
            if (string.IsNullOrEmpty(assignedTo))
            {
                return Unauthorized("You must be logged in.");
            }
            var accountType = info.typeOfAccount?.Trim() ?? string.Empty;
            if (!TicketValues.AccountTypes.Contains(accountType))
            {
                return BadRequest("Account type must be Individual or Joint.");
            }
            var jointCustomerId = accountType == "Joint" ? info.jointCustomerId : null;
            if (accountType == "Joint" && (jointCustomerId is null || !await _db.Customers.AnyAsync(c => c.CustomerId == jointCustomerId)))
            {
                return BadRequest("The joint customer does not exist.");
            }
            try
            {
                // Locking this one customer row makes a second admin opening the same customer wait, then see the ticket and get a 409.
                using var transaction = await _db.Database.BeginTransactionAsync();
                var customer = await _db.Customers
                    .FromSqlInterpolated($"SELECT * FROM Customers WITH (UPDLOCK, ROWLOCK) WHERE CustomerId = {customerId}")
                    .FirstOrDefaultAsync();
                if (customer is null)
                {
                    return NotFound("That customer does not exist.");
                }
                if (await _db.Tickets.AnyAsync(t => t.CustomerId == customerId))
                {
                    return Conflict("That customer has already been onboarded.");
                }
                var (error, body) = TicketIntake.Open(_db, customer, accountType, jointCustomerId, assignedTo);
                if (error is not null)
                {
                    return error;
                }
                await transaction.CommitAsync();
                return Ok(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open a ticket for pending customer {CustomerId}.", customerId);
                return Problem("Could not open the ticket.");
            }
        }
    }
}
