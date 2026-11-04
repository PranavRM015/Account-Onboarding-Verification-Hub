using Intake.Data;
using Intake.DTOs;
using Intake.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace Intake.Controllers;

// Admin-driven customer intake. All endpoints require a valid admin JWT from Identity.
// Role name is "admin" (lowercase) as Identity defines it.
[ApiController]
[Authorize(Roles = "admin")]
public class AdminIntakeController : ControllerBase
{
    private readonly IntakeDbContext _db;
    private readonly RiskServiceClient _risk;
    private readonly ILogger<AdminIntakeController> _logger;

    public AdminIntakeController(IntakeDbContext db, RiskServiceClient risk, ILogger<AdminIntakeController> logger)
    {
        _db = db;
        _risk = risk;
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

    // Validates the form and screens the customer with Risk. Nothing is saved: the profile, the login
    // and the ticket are created together once Review can open the ticket, so a failed or half-finished
    // request never leaves a customer behind.
    // INTEGRATION BLOCKER: Review service not yet available. Returns 501 after Risk has answered.
    [HttpPost("api/admins/inputForm")]
    public async Task<IActionResult> InputForm([FromBody] InputFormDto dto)
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

        var username = dto.username?.Trim() ?? string.Empty;
        if (username.Length == 0) return BadRequest("A username is required.");
        if (string.IsNullOrWhiteSpace(dto.password)) return BadRequest("A password is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryQuestion)) return BadRequest("A password recovery question is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryAnswer)) return BadRequest("A password recovery answer is required.");

        // The same profile rules as self-signup, so Risk is never sent something it will reject.
        var profile = new CustomerSignupDto
        {
            firstName            = dto.FirstName ?? string.Empty,
            middleName           = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName,
            lastName             = dto.LastName ?? string.Empty,
            dateOfBirth          = dto.DateOfBirth,
            gender               = dto.Gender ?? string.Empty,
            taxIdType            = dto.taxIdType ?? string.Empty,
            taxIdNumber          = dto.taxIdNumber ?? string.Empty,
            residentialAddress   = dto.residentialAddress ?? string.Empty,
            countryOfResidence   = dto.countryOfResidence ?? string.Empty,
            countryOfCitizenship = dto.countryOfCitizenship ?? string.Empty,
            occupation           = dto.occupation ?? string.Empty,
            employer             = dto.Employer ?? string.Empty,
        };
        var profileError = ProfileValidator.Validate(profile);
        if (profileError is not null) return BadRequest(profileError);

        var taxDigits = new string(profile.taxIdNumber.Where(char.IsLetterOrDigit).ToArray());
        var (assessment, error) = await ScreenCustomer(new RiskAnalyzeRequest
        {
            FirstName            = profile.firstName.Trim(),
            MiddleName           = profile.middleName?.Trim(),
            LastName             = profile.lastName.Trim(),
            TaxIdLastFourDigits  = taxDigits[^4..],
            ResidentialAddress   = profile.residentialAddress.Trim(),
            CountryOfResidence   = profile.countryOfResidence.Trim().ToUpperInvariant(),
            CountryOfCitizenship = profile.countryOfCitizenship.Trim().ToUpperInvariant(),
            Occupation           = profile.occupation.Trim(),
            Employer             = profile.employer.Trim(),
        });
        if (error is not null) return error;

        // INTEGRATION BLOCKER: replace this return with, in order: save the profile, create the login in
        // Identity (IdentityServiceClient.CreateAccount, as CustomerIntakeController does), then hand the
        // assessment to Review to open the ticket.
        return StatusCode(501, new
        {
            message = "Risk analysis complete. Ticket creation is pending Review service integration.",
            riskRating = assessment!.RiskRating,
            flags = assessment.Flags,
        });
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

            var customer = await _db.Customers.AsNoTracking().FirstAsync(c => c.CustomerId == customerId);
            var (assessment, error) = await ScreenCustomer(new RiskAnalyzeRequest
            {
                FirstName            = customer.FirstName.Trim(),
                MiddleName           = customer.MiddleName?.Trim(),
                LastName             = customer.LastName.Trim(),
                TaxIdLastFourDigits  = customer.TaxIdLastFourDigits.Trim(),
                ResidentialAddress   = customer.ResidentialAddress.Trim(),
                CountryOfResidence   = customer.CountryOfResidence.Trim().ToUpperInvariant(),
                CountryOfCitizenship = customer.CountryOfCitizenship.Trim().ToUpperInvariant(),
                Occupation           = customer.Occupation.Trim(),
                Employer             = customer.Employer.Trim(),
            });
            if (error is not null) return error;

            // INTEGRATION BLOCKER: Review service not yet available. When it is, hand the assessment to
            // Review here to open the ticket. The row-lock (UPDLOCK, ROWLOCK) must also be reintroduced
            // around this call, inside a transaction, so two admins cannot open the same customer.
            _logger.LogWarning("Submit for CustomerId={Id}: screened {Rating}; ticket pending Review.", customerId, assessment!.RiskRating);
            return StatusCode(501, new
            {
                message = "Risk analysis complete. Ticket creation is pending Review service integration.",
                customerId,
                riskRating = assessment.RiskRating,
                flags = assessment.Flags,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process submit for CustomerId={CustomerId}.", customerId);
            return Problem("Could not open the ticket.");
        }
    }

    // The admin's own token is what Risk checks, so it is passed on exactly as it arrived.
    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
    }

    // Asks Risk to screen the customer. Returns the assessment, or the response to send back when screening
    // did not succeed. A failed screening is never treated as Low risk, so every failure stops the request.
    private async Task<(RiskAnalyzeResponse? Assessment, IActionResult? Error)> ScreenCustomer(RiskAnalyzeRequest request)
    {
        var token = BearerToken();
        if (token is null) return (null, Unauthorized("You must be logged in."));

        try
        {
            using var response = await _risk.AnalyzeAsync(request, token);
            if (response.IsSuccessStatusCode)
            {
                var assessment = await response.Content.ReadFromJsonAsync<RiskAnalyzeResponse>();
                if (assessment is not null && !string.IsNullOrEmpty(assessment.RiskRating)) return (assessment, null);
                _logger.LogError("Risk returned an empty assessment.");
                return (null, StatusCode(502, "The risk service returned an unusable answer. Please try again."));
            }

            _logger.LogWarning("Risk returned {Status}: {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync());
            IActionResult error = response.StatusCode switch
            {
                HttpStatusCode.BadRequest         => BadRequest("The risk service rejected the customer details."),
                HttpStatusCode.Unauthorized       => Unauthorized("Your session is not valid for the risk service. Please log in again."),
                HttpStatusCode.Forbidden          => StatusCode(403, "Only an admin can run a risk analysis."),
                HttpStatusCode.ServiceUnavailable => StatusCode(503, "The risk service is temporarily unavailable. Please try again shortly."),
                _                                 => StatusCode(502, "The risk service could not complete the analysis."),
            };
            return (null, error);
        }
        catch (Exception ex)
        {
            // Timeout, connection refused, or a body that was not the expected JSON.
            _logger.LogError(ex, "Could not get an analysis from the Risk service.");
            return (null, StatusCode(503, "The risk service is temporarily unavailable. Please try again shortly."));
        }
    }
}
