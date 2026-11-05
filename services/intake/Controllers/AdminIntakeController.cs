using Intake.Data;
using Intake.DTOs;
using Intake.Models;
using Intake.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace Intake.Controllers;

// Admin-driven customer intake. All endpoints require a valid admin JWT from Identity.
// Role name is "admin" (lowercase) as Identity defines it.
[ApiController]
[Authorize(Roles = "admin")]
public class AdminIntakeController : ControllerBase
{
    private readonly IntakeDbContext _db;
    private readonly IdentityServiceClient _identity;
    private readonly RiskServiceClient _risk;
    private readonly ReviewServiceClient _review;
    private readonly string _taxIdPepper;
    private readonly ILogger<AdminIntakeController> _logger;

    public AdminIntakeController(
        IntakeDbContext db,
        IdentityServiceClient identity,
        RiskServiceClient risk,
        ReviewServiceClient review,
        IConfiguration configuration,
        ILogger<AdminIntakeController> logger)
    {
        _db = db;
        _identity = identity;
        _risk = risk;
        _review = review;
        _taxIdPepper = configuration["TaxIdHashPepper"]
            ?? throw new InvalidOperationException("'TaxIdHashPepper' is not configured.");
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

    [HttpPost("api/admins/inputForm")]
    public async Task<IActionResult> InputForm([FromBody] InputFormDto dto)
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

        var token = BearerToken();
        if (token is null) return Unauthorized("A valid admin token is required.");

        // Screen with Risk first — no DB writes if Risk rejects.
        var taxDigits = new string(taxIdNumber.Where(char.IsLetterOrDigit).ToArray());
        var lastFour  = taxDigits.Length >= 4 ? taxDigits[^4..] : taxDigits.PadLeft(4, '0');

        RiskAnalyzeResponse? assessment;
        try
        {
            using var riskResponse = await _risk.AnalyzeAsync(new RiskAnalyzeRequest
            {
                FirstName            = dto.FirstName.Trim(),
                MiddleName           = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim(),
                LastName             = dto.LastName.Trim(),
                TaxIdLastFourDigits  = lastFour,
                ResidentialAddress   = dto.residentialAddress.Trim(),
                CountryOfResidence   = dto.countryOfResidence.Trim().ToUpperInvariant(),
                CountryOfCitizenship = dto.countryOfCitizenship.Trim().ToUpperInvariant(),
                Occupation           = dto.occupation.Trim(),
                Employer             = dto.Employer.Trim(),
            }, token);

            if (!riskResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Risk returned {Status} for input form.", (int)riskResponse.StatusCode);
                return riskResponse.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized       => Unauthorized("Your session is not valid for the risk service."),
                    System.Net.HttpStatusCode.Forbidden          => StatusCode(403, "Only an admin can run a risk analysis."),
                    System.Net.HttpStatusCode.ServiceUnavailable => StatusCode(503, "The risk service is temporarily unavailable. Please try again shortly."),
                    _                                             => StatusCode(502, "The risk service could not complete the analysis."),
                };
            }
            assessment = await riskResponse.Content.ReadFromJsonAsync<RiskAnalyzeResponse>();
            if (assessment is null || string.IsNullOrEmpty(assessment.RiskRating))
            {
                _logger.LogError("Risk returned an empty assessment for input form.");
                return StatusCode(502, "The risk service returned an unusable response. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reach Risk for input form.");
            return StatusCode(503, "The risk service is temporarily unavailable. Please try again shortly.");
        }

        // Save the customer profile.
        var taxIdHash = TaxIdHasher.Hash(taxIdNumber, _taxIdPepper);
        var profile = new CustomerProfile
        {
            FirstName            = dto.FirstName.Trim(),
            MiddleName           = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim(),
            LastName             = dto.LastName.Trim(),
            DateOfBirth          = dto.DateOfBirth.Date,
            Gender               = dto.Gender.Trim(),
            TaxIdType            = dto.taxIdType.Trim().ToUpperInvariant(),
            TaxIdHash            = taxIdHash,
            TaxIdLastFourDigits  = lastFour,
            ResidentialAddress   = dto.residentialAddress.Trim(),
            CountryOfResidence   = dto.countryOfResidence.Trim().ToUpperInvariant(),
            CountryOfCitizenship = dto.countryOfCitizenship.Trim().ToUpperInvariant(),
            Occupation           = dto.occupation.Trim(),
            Employer             = dto.Employer.Trim(),
        };
        try
        {
            _db.Customers.Add(profile);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            if (_db.Customers.Any(c => c.Username == username))
                return Conflict(new { message = "That username is already taken." });
            var existingId = _db.Customers
                .Where(c => c.TaxIdHash == taxIdHash).Select(c => c.CustomerId).FirstOrDefault();
            return Conflict(new { message = "A customer with that tax ID already exists.", customerId = existingId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save input form profile.");
            return Problem("Could not save the customer.");
        }

        return await CompleteOnboarding(profile, accountType, jointCustomerId, username, dto, assessment, token);
    }

    // Lists self-registered customers who have no ticket yet, oldest first.
    [HttpGet("api/admins/pending-customers")]
    public async Task<IActionResult> ListPending()
    {
        var token = BearerToken();
        if (token is null) return Unauthorized("A valid admin token is required.");

        // Fetch the set of CustomerIds that already have a Review ticket.
        // A null result means Review is unreachable or returned an error — never treat it as empty.
        var onboarded = await _review.GetOnboardedCustomerIdsAsync(token);
        if (onboarded is null)
        {
            _logger.LogWarning("Could not retrieve onboarded customer IDs from Review for ListPending.");
            return StatusCode(503, "Could not confirm onboarding status with the review service. Please try again.");
        }

        var onboardedSet = onboarded.ToHashSet();

        try
        {
            var rows = await _db.Customers
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

            return Ok(rows.Where(c => !onboardedSet.Contains(c.customerId)));
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

            var token = BearerToken();
            if (token is null) return Unauthorized("A valid admin token is required.");

            var ticketCheck = await _review.FindTicketByCustomerAsync(customerId, token);
            if (ticketCheck.IsFound)
                return Conflict("That customer has already been onboarded.");
            if (ticketCheck.IsFailed)
                return StatusCode(503, "Could not confirm ticket status with the review service. Please try again.");

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

        var token = BearerToken();
        if (token is null) return Unauthorized("A valid admin token is required.");

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return NotFound("That customer does not exist.");

        // Check Review for an existing ticket — use Review's lookup, not the shared Tickets table,
        // because Review owns its own database.
        var existing = await _review.FindTicketByCustomerAsync(customerId, token);
        if (existing.IsFound)
        {
            return Conflict(new
            {
                message = "That customer has already been onboarded.",
                customerId,
                ticketId = existing.Ticket!.TicketId,
            });
        }
        if (existing.IsFailed)
        {
            _logger.LogWarning("Review lookup failed before submit for CustomerId={Id}.", customerId);
            return StatusCode(503, "Could not confirm ticket status with the review service. Please try again.");
        }

        // Screen with Risk.
        var lastFour = customer.TaxIdLastFourDigits;

        RiskAnalyzeResponse? assessment;
        try
        {
            using var riskResponse = await _risk.AnalyzeAsync(new RiskAnalyzeRequest
            {
                FirstName            = customer.FirstName,
                MiddleName           = customer.MiddleName,
                LastName             = customer.LastName,
                TaxIdLastFourDigits  = lastFour,
                ResidentialAddress   = customer.ResidentialAddress,
                CountryOfResidence   = customer.CountryOfResidence,
                CountryOfCitizenship = customer.CountryOfCitizenship,
                Occupation           = customer.Occupation,
                Employer             = customer.Employer,
            }, token);

            if (!riskResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Risk returned {Status} for submit CustomerId={Id}.", (int)riskResponse.StatusCode, customerId);
                return riskResponse.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized       => Unauthorized("Your session is not valid for the risk service."),
                    System.Net.HttpStatusCode.Forbidden          => StatusCode(403, "Only an admin can run a risk analysis."),
                    System.Net.HttpStatusCode.ServiceUnavailable => StatusCode(503, "The risk service is temporarily unavailable. Please try again shortly."),
                    _                                             => StatusCode(502, "The risk service could not complete the analysis."),
                };
            }
            assessment = await riskResponse.Content.ReadFromJsonAsync<RiskAnalyzeResponse>();
            if (assessment is null || string.IsNullOrEmpty(assessment.RiskRating))
            {
                _logger.LogError("Risk returned an empty assessment for submit CustomerId={Id}.", customerId);
                return StatusCode(502, "The risk service returned an unusable response. Please try again.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reach Risk for submit CustomerId={Id}.", customerId);
            return StatusCode(503, "The risk service is temporarily unavailable. Please try again shortly.");
        }

        // Resolve joint customer name snapshot.
        string? jointCustomerName = null;
        if (jointCustomerId is not null)
        {
            var joint = await _db.Customers
                .Where(c => c.CustomerId == jointCustomerId)
                .Select(c => new { c.FirstName, c.MiddleName, c.LastName })
                .FirstOrDefaultAsync();
            if (joint is not null)
                jointCustomerName = string.Join(" ",
                    new[] { joint.FirstName, joint.MiddleName, joint.LastName }
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
        }

        // No Identity call — credentials already exist for a pending (self-signed-up) customer.
        var ticketRequest = new ReviewCreateTicketDto
        {
            RequestId         = Guid.NewGuid(),
            CustomerId        = customerId,
            AccountType       = accountType,
            JointCustomerId   = jointCustomerId,
            JointCustomerName = jointCustomerName,
            RiskRating        = assessment.RiskRating,
            Flags             = assessment.Flags,
            VendorResponse    = new ReviewVendorAssessmentDto
            {
                NameMatch        = assessment.VendorResponse.NameMatch,
                DateOfBirthMatch = assessment.VendorResponse.DateOfBirthMatch,
                AddressMatch     = assessment.VendorResponse.AddressMatch,
                TaxIdMatch       = assessment.VendorResponse.TaxIdMatch,
                Score            = assessment.VendorResponse.Score,
                RiskCodes        = assessment.VendorResponse.RiskCodes,
            },
            Customer = new ReviewCustomerSnapshotDto
            {
                FirstName            = customer.FirstName,
                MiddleName           = customer.MiddleName,
                LastName             = customer.LastName,
                DateOfBirth          = customer.DateOfBirth,
                Gender               = customer.Gender,
                TaxIdType            = customer.TaxIdType,
                ResidentialAddress   = customer.ResidentialAddress,
                CountryOfResidence   = customer.CountryOfResidence,
                CountryOfCitizenship = customer.CountryOfCitizenship,
                Occupation           = customer.Occupation,
                Employer             = customer.Employer,
            },
        };

        return await CallReview(customerId, ticketRequest, token);
    }

    // Handles Identity + Review calls after InputForm saves the profile.
    private async Task<IActionResult> CompleteOnboarding(
        CustomerProfile profile, string accountType, int? jointCustomerId,
        string username, InputFormDto dto, RiskAnalyzeResponse assessment, string token)
    {
        HttpResponseMessage identityResponse;
        try
        {
            identityResponse = await _identity.CreateAccount(
                "customer", profile.CustomerId, username, dto.password,
                dto.passwordRecoveryQuestion.Trim(), dto.passwordRecoveryAnswer.Trim());
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                _logger.LogWarning("Identity timed out for CustomerId={Id}. Profile preserved.", profile.CustomerId);
            else
                _logger.LogError(ex, "Could not reach Identity for CustomerId={Id}. Profile preserved.", profile.CustomerId);
            return StatusCode(503, "The account service is temporarily unavailable. Please try again shortly.");
        }

        if (!identityResponse.IsSuccessStatusCode)
        {
            var identityBody = await identityResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("Identity rejected CustomerId={Id}: {Status}", profile.CustomerId, (int)identityResponse.StatusCode);
            switch (identityResponse.StatusCode)
            {
                case System.Net.HttpStatusCode.BadRequest:
                case System.Net.HttpStatusCode.Conflict:
                case System.Net.HttpStatusCode.Unauthorized:
                    // Confirmed rejection before any write in Identity — safe to delete profile.
                    await DeleteProfile(profile.CustomerId);
                    if (identityResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        return StatusCode(500, "Internal configuration error: internal API key mismatch.");
                    return identityResponse.StatusCode == System.Net.HttpStatusCode.Conflict
                        ? Conflict(new { message = identityBody }) : BadRequest(identityBody);
                default:
                    _logger.LogError("Identity returned {Status} for CustomerId={Id}. Profile preserved.",
                        (int)identityResponse.StatusCode, profile.CustomerId);
                    return StatusCode(503, "The account service is temporarily unavailable. Please try again shortly.");
            }
        }

        // Resolve joint customer name snapshot for Review.
        string? jointCustomerName = null;
        if (jointCustomerId is not null)
        {
            var joint = await _db.Customers
                .Where(c => c.CustomerId == jointCustomerId)
                .Select(c => new { c.FirstName, c.MiddleName, c.LastName })
                .FirstOrDefaultAsync();
            if (joint is not null)
                jointCustomerName = string.Join(" ",
                    new[] { joint.FirstName, joint.MiddleName, joint.LastName }
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
        }

        var ticketRequest = new ReviewCreateTicketDto
        {
            RequestId = Guid.NewGuid(), CustomerId = profile.CustomerId,
            AccountType = accountType, JointCustomerId = jointCustomerId,
            JointCustomerName = jointCustomerName,
            RiskRating = assessment.RiskRating, Flags = assessment.Flags,
            VendorResponse = new ReviewVendorAssessmentDto
            {
                NameMatch = assessment.VendorResponse.NameMatch,
                DateOfBirthMatch = assessment.VendorResponse.DateOfBirthMatch,
                AddressMatch = assessment.VendorResponse.AddressMatch,
                TaxIdMatch = assessment.VendorResponse.TaxIdMatch,
                Score = assessment.VendorResponse.Score,
                RiskCodes = assessment.VendorResponse.RiskCodes,
            },
            Customer = new ReviewCustomerSnapshotDto
            {
                FirstName = profile.FirstName, MiddleName = profile.MiddleName,
                LastName = profile.LastName, DateOfBirth = profile.DateOfBirth,
                Gender = profile.Gender, TaxIdType = profile.TaxIdType,
                ResidentialAddress = profile.ResidentialAddress,
                CountryOfResidence = profile.CountryOfResidence,
                CountryOfCitizenship = profile.CountryOfCitizenship,
                Occupation = profile.Occupation, Employer = profile.Employer,
            },
        };

        return await CallReview(profile.CustomerId, ticketRequest, token);
    }

    private async Task<IActionResult> CallReview(int customerId, ReviewCreateTicketDto ticketRequest, string token)
    {
        HttpResponseMessage reviewResponse;
        try
        {
            reviewResponse = await _review.CreateTicketAsync(ticketRequest, token);
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                _logger.LogWarning("Review timed out for CustomerId={Id}. Checking ticket status.", customerId);
            else
                _logger.LogError(ex, "Could not reach Review for CustomerId={Id}. Checking ticket status.", customerId);

            var lookup = await _review.FindTicketByCustomerAsync(customerId, token);
            if (lookup.IsFound && lookup.Ticket!.TicketId > 0)
            {
                _logger.LogInformation("Confirmed ticket {TicketId} for CustomerId={Id}.", lookup.Ticket.TicketId, customerId);
                return Ok(new { customerId, ticketId = lookup.Ticket.TicketId });
            }
            _logger.LogError("Cannot confirm Review ticket for CustomerId={Id}. Profile and credentials preserved.", customerId);
            return StatusCode(503, "The review service is temporarily unavailable. Please try again shortly.");
        }

        if (reviewResponse.IsSuccessStatusCode)
        {
            var result = await reviewResponse.Content.ReadFromJsonAsync<ReviewTicketResponseDto>();
            if (result is null || result.TicketId <= 0)
            {
                _logger.LogError("Review returned success but no ticket for CustomerId={Id}.", customerId);
                return StatusCode(502, "The review service returned an unusable response. Please try again.");
            }
            return Ok(new { customerId, ticketId = result.TicketId });
        }

        var reviewBody = await reviewResponse.Content.ReadAsStringAsync();
        _logger.LogWarning("Review returned {Status} for CustomerId={Id}.", (int)reviewResponse.StatusCode, customerId);

        if (reviewResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
            return Conflict(new { message = reviewBody });

        return StatusCode(503, "The review service could not open the ticket. Please try again shortly.");
    }

    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim() : null;
    }

    private async Task DeleteProfile(int customerId)
    {
        try
        {
            var row = await _db.Customers.FindAsync(customerId);
            if (row is not null) { _db.Customers.Remove(row); await _db.SaveChangesAsync(); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for CustomerId={Id}. Profile row still exists.", customerId);
        }
    }

}
