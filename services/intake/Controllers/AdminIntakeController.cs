using Intake.Data;
using Intake.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intake.Controllers;

// Admin-driven customer intake. All endpoints require a valid admin JWT from Identity.
// Role name is "admin" (lowercase) as Identity defines it.
[ApiController]
[Authorize(Roles = "admin")]
public class AdminIntakeController : ControllerBase
{
    private readonly IntakeDbContext _db;
    private readonly ILogger<AdminIntakeController> _logger;

    public AdminIntakeController(IntakeDbContext db, ILogger<AdminIntakeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Checks whether a customer profile exists. Called by the input form before
    // submitting a joint account.
    [HttpGet("api/admins/inputForm/customers/{customerId:int}/exists")]
    public IActionResult CustomerExists(int customerId)
    {
        try
        {
            return Ok(new { exists = _db.Customers.Any(c => c.CustomerId == customerId) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check customer {CustomerId} exists.", customerId);
            return Problem("Could not check the customer.");
        }
    }

    // INTEGRATION BLOCKER: Risk and Review services not yet available.
    // All validation runs, but no database writes are made until ticket creation
    // can be completed atomically. Returns 501 until the Risk service API is confirmed.
    [HttpPost("api/admins/inputForm")]
    public IActionResult InputForm([FromBody] InputFormDto dto)
    {
        var assignedTo = User.Identity?.Name;
        if (string.IsNullOrEmpty(assignedTo)) return Unauthorized("You must be logged in.");

        var taxIdNumber = dto.taxIdNumber?.Trim() ?? string.Empty;
        if (taxIdNumber.Length == 0) return BadRequest("A tax ID number is required.");

        var accountType = dto.typeOfAccount?.Trim() ?? string.Empty;
        if (accountType != "Individual" && accountType != "Joint")
            return BadRequest("Account type must be Individual or Joint.");

        var jointCustomerId = accountType == "Joint" ? dto.jointCustomerId : null;
        if (accountType == "Joint" &&
            (jointCustomerId is null || !_db.Customers.Any(c => c.CustomerId == jointCustomerId)))
            return BadRequest("The joint customer does not exist.");

        var username = dto.username?.Trim() ?? string.Empty;
        if (username.Length == 0) return BadRequest("A username is required.");
        if (string.IsNullOrWhiteSpace(dto.password)) return BadRequest("A password is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryQuestion)) return BadRequest("A password recovery question is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryAnswer)) return BadRequest("A password recovery answer is required.");

        // Validation passed. No database writes have been made.
        // Replace this return with profile creation + Identity call + Risk service call
        // once the Risk/Review API contract is confirmed.
        return StatusCode(501, "Ticket creation is pending Risk and Review service integration.");
    }

    // Lists self-registered customers who have no ticket yet, oldest first.

    [HttpGet("api/admins/pending-customers")]
    public async Task<IActionResult> ListPending()
    {
        try
        {
            var rows = await _db.Customers
                .Where(c => !_db.Tickets.Any(t => t.CustomerId == c.CustomerId))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.CustomerId)
                .Select(c => new
                {
                    customerId = c.CustomerId,
                    firstName  = c.FirstName,
                    middleName = c.MiddleName,
                    lastName   = c.LastName,
                    createdAt  = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),
                })
                .ToListAsync();
            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list pending customers.");
            return Problem("Could not retrieve the pending customer list.");
        }
    }

    // Returns a pending customer's profile details. No credential columns are included.
    [HttpGet("api/admins/pending-customers/{customerId:int}")]
    public async Task<IActionResult> PendingDetails(int customerId)
    {
        try
        {
            var c = await _db.Customers
                .Where(x => x.CustomerId == customerId)
                .Select(x => new
                {
                    x.CustomerId, x.FirstName, x.MiddleName, x.LastName,
                    x.DateOfBirth, x.Gender, x.TaxIdType, x.TaxIdLastFourDigits,
                    x.ResidentialAddress, x.CountryOfResidence, x.CountryOfCitizenship,
                    x.Occupation, x.Employer, x.CreatedAt,
                })
                .FirstOrDefaultAsync();

            if (c is null) return NotFound("That customer does not exist.");
            if (await _db.Tickets.AnyAsync(t => t.CustomerId == customerId))
                return Conflict("That customer has already been onboarded.");

            return Ok(new
            {
                customerId           = c.CustomerId,
                firstName            = c.FirstName,
                middleName           = c.MiddleName,
                lastName             = c.LastName,
                dateOfBirth          = c.DateOfBirth.ToString("yyyy-MM-dd"),
                gender               = c.Gender,
                taxIdType            = c.TaxIdType,
                taxIdLastFourDigits  = c.TaxIdLastFourDigits,
                residentialAddress   = c.ResidentialAddress,
                countryOfResidence   = c.CountryOfResidence,
                countryOfCitizenship = c.CountryOfCitizenship,
                occupation           = c.Occupation,
                employer             = c.Employer,
                createdAt            = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending customer {CustomerId}.", customerId);
            return Problem("Could not retrieve the customer.");
        }
    }

    // Validates the customer and account type, then hands off to Risk/Review to open the ticket.
    // INTEGRATION BLOCKER: ticket creation (Risk + Review) is not yet implemented.
    // Returns 501 after row-locking and validating until the Risk service API is confirmed.
    [HttpPost("api/admins/pending-customers/{customerId:int}/submit")]
    public async Task<IActionResult> Submit(int customerId, [FromBody] PendingSubmitDto dto)
    {
        var assignedTo = User.Identity?.Name;
        if (string.IsNullOrEmpty(assignedTo)) return Unauthorized("You must be logged in.");

        var accountType = dto.typeOfAccount?.Trim() ?? string.Empty;
        if (accountType != "Individual" && accountType != "Joint")
            return BadRequest("Account type must be Individual or Joint.");

        var jointCustomerId = accountType == "Joint" ? dto.jointCustomerId : null;
        if (accountType == "Joint" &&
            (jointCustomerId is null || !await _db.Customers.AnyAsync(c => c.CustomerId == jointCustomerId)))
            return BadRequest("The joint customer does not exist.");

        try
        {
            var exists = await _db.Customers.AnyAsync(c => c.CustomerId == customerId);
            if (!exists) return NotFound("That customer does not exist.");

            if (await _db.Tickets.AnyAsync(t => t.CustomerId == customerId))
                return Conflict("That customer has already been onboarded.");

            // INTEGRATION BLOCKER: replace this return with an HTTP call to the Risk service
            // to run checks and open the ticket. The row-lock (UPDLOCK, ROWLOCK) must also
            // be reintroduced here inside a transaction when that call is wired up.
            _logger.LogWarning("Submit for CustomerId={Id}: validated; ticket pending Risk/Review.", customerId);
            return StatusCode(501, new
            {
                message = "Customer validated. Ticket creation is pending Risk and Review service integration.",
                customerId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process submit for CustomerId={CustomerId}.", customerId);
            return Problem("Could not open the ticket.");
        }
    }

}
