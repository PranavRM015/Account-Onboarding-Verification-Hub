using Intake.Data;
using Intake.DTOs;
using Intake.Models;
using Intake.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Intake.Controllers;

// POST /api/users/signup — customer profile portion of self-registration.
//
// INTEGRATION BLOCKER: The monolith does two things atomically:
//   1. Save the customer profile  (Intake — done here)
//   2. Hash and store credentials (Identity — NOT done here, see TODO in Signup())
//
// A customer created through this endpoint cannot log in until
// Identity integration is completed in Step 6.
[ApiController]
[Route("api/users")]
public class CustomerIntakeController : ControllerBase
{
    private readonly IntakeDbContext _db;
    private readonly string _taxIdPepper;
    private readonly ILogger<CustomerIntakeController> _logger;

    public CustomerIntakeController(
        IntakeDbContext db,
        IConfiguration configuration,
        ILogger<CustomerIntakeController> logger)
    {
        _db = db;
        _taxIdPepper = configuration["TaxIdHashPepper"]
            ?? throw new InvalidOperationException(
                "'TaxIdHashPepper' is not configured. " +
                "Set the TaxIdHashPepper environment variable.");
        _logger = logger;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] CustomerSignupDto dto)
    {
        // 1. Validate profile fields (Intake responsibility).
        var profileError = ProfileValidator.Validate(dto);
        if (profileError is not null)
            return BadRequest(profileError);

        // 2. Verify credential fields are present — Intake does not process
        //    them, but we must confirm they were sent so Identity can use them.
        //    Silently accepting a request without credentials would create
        //    an account the customer can never log into.
        if (string.IsNullOrWhiteSpace(dto.username))
            return BadRequest("A username is required.");
        if (string.IsNullOrWhiteSpace(dto.password))
            return BadRequest("A password is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryQuestion))
            return BadRequest("A password recovery question is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryAnswer))
            return BadRequest("A password recovery answer is required.");

        // 3. Compute TaxId hash (profile integrity — Intake owns this).
        //    Algorithm matches the monolith exactly so hashes are identical.
        var taxIdNumber = dto.taxIdNumber.Trim();
        var taxIdHash   = TaxIdHasher.Hash(taxIdNumber, _taxIdPepper);
        var taxDigits   = new string(taxIdNumber.Where(char.IsLetterOrDigit).ToArray());
        var lastFour    = taxDigits.Length >= 4 ? taxDigits[^4..] : taxDigits.PadLeft(4, '0');

        // 4. Build the profile row.
        //    Credential columns (Username, PasswordHash, etc.) are left as
        //    C# defaults. HasDefaultValue() in IntakeDbContext tells EF Core
        //    to exclude them from the INSERT — the DB fills them with ''.
        var profile = new CustomerProfile
        {
            FirstName            = dto.firstName.Trim(),
            MiddleName           = string.IsNullOrWhiteSpace(dto.middleName)
                                       ? null : dto.middleName.Trim(),
            LastName             = dto.lastName.Trim(),
            DateOfBirth          = dto.dateOfBirth.Date,
            Gender               = dto.gender.Trim(),
            TaxIdType            = dto.taxIdType.Trim().ToUpperInvariant(),
            TaxIdHash            = taxIdHash,
            TaxIdLastFourDigits  = lastFour,
            ResidentialAddress   = dto.residentialAddress.Trim(),
            CountryOfResidence   = dto.countryOfResidence.Trim().ToUpperInvariant(),
            CountryOfCitizenship = dto.countryOfCitizenship.Trim().ToUpperInvariant(),
            Occupation           = dto.occupation.Trim(),
            Employer             = dto.employer.Trim(),
        };

        try
        {
            _db.Customers.Add(profile);
            await _db.SaveChangesAsync();

            // TODO (Step 6): Call Identity service here to set Username,
            // PasswordHash, PasswordRecoveryQuestion, PasswordRecoveryAnswer
            // for profile.CustomerId. If Identity rejects the call, delete the
            // profile row and return Identity's error to the caller.
            _logger.LogWarning(
                "Profile created (CustomerId={CustomerId}) — credentials not " +
                "yet set, awaiting Identity service integration (Step 6).",
                profile.CustomerId);

            // Monolith returns plain-text "created" with 200 OK.
            // Angular user-signup.ts uses responseType:'text' and navigates
            // on any 2xx — this is fully compatible.
            return Ok("created");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            // Duplicate TaxIdHash or Username unique constraint.
            // Monolith returns this exact message — Angular expects it.
            return Conflict("That username or tax ID is already registered.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create customer profile.");
            return Problem("Could not create the account.");
        }
    }
}
