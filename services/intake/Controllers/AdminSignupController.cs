using Intake.Data;
using Intake.DTOs;
using Intake.Models;
using Intake.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Intake.Controllers;

// Handles admin self-registration, the same way CustomerIntakeController handles customers:
// save the profile first, then ask Identity to create the login under the new AdminId.
// Open to anyone, as in the original backend; the "auth" rate limit is the only guard.
//
// Error handling after the profile is saved:
//   Confirmed rejections (400/409/401) — Identity validates these before writing
//     anything, so the profile is safe to delete. We delete it and return the error.
//   Uncertain outcomes (timeout, network error, 5xx) — the profile is preserved
//     because Identity may have already saved credentials. The 503 response tells
//     the caller to retry later. Manual reconciliation may be needed.
[ApiController]
[Route("api/admins")]
public class AdminSignupController : ControllerBase
{
    private readonly IntakeDbContext _db;
    private readonly IdentityServiceClient _identity;
    private readonly string _taxIdPepper;
    private readonly ILogger<AdminSignupController> _logger;

    public AdminSignupController(
        IntakeDbContext db,
        IdentityServiceClient identity,
        IConfiguration configuration,
        ILogger<AdminSignupController> logger)
    {
        _db       = db;
        _identity = identity;
        _taxIdPepper = configuration["TaxIdHashPepper"]
            ?? throw new InvalidOperationException(
                "'TaxIdHashPepper' is not configured. " +
                "Set the TaxIdHashPepper environment variable.");
        _logger = logger;
    }

    [EnableRateLimiting("auth")]
    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] AdminSignupDto dto)
    {
        var profileError = ProfileValidator.Validate(dto);
        if (profileError is not null)
            return BadRequest(profileError);

        // Only check that credential fields were sent — Identity validates their content.
        var username = dto.username.Trim();
        if (username.Length == 0)
            return BadRequest("A username is required.");
        if (string.IsNullOrWhiteSpace(dto.password))
            return BadRequest("A password is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryQuestion))
            return BadRequest("A password recovery question is required.");
        if (string.IsNullOrWhiteSpace(dto.passwordRecoveryAnswer))
            return BadRequest("A password recovery answer is required.");

        // Check availability before saving. null means Identity was unreachable —
        // proceed anyway and let the create call catch any conflict.
        var available = await _identity.IsUsernameAvailable("admin", username);
        if (available == false)
            return Conflict("That username is already taken.");

        var ssnHash = TaxIdHasher.Hash(dto.ssn, _taxIdPepper);

        // A retry after Identity timed out or failed finds the profile left behind by the
        // first attempt. Resume it instead of hitting the SSN index. The username must
        // match so that knowing an SSN alone is not enough to claim someone's profile.
        var profile = await _db.Admins.FirstOrDefaultAsync(a => a.SSNHash == ssnHash && a.Username == username);
        var resumed = profile is not null;
        if (profile is null)
        {
            profile = new AdminProfile
            {
                // The shared Admins table has a unique index on Username. Identity holds the real login.
                Username             = username,
                FirstName            = dto.firstName.Trim(),
                MiddleName           = string.IsNullOrWhiteSpace(dto.middleName) ? null : dto.middleName.Trim(),
                LastName             = dto.lastName.Trim(),
                DateOfBirth          = dto.dateOfBirth.Date,
                Gender               = dto.gender.Trim(),
                SSNHash              = ssnHash,
                IsCitizen            = dto.isCitizen,
                CountryOfCitizenship = dto.countryOfCitizenship.Trim().ToUpperInvariant(),
            };

            try
            {
                _db.Admins.Add(profile);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                return Conflict("That username or SSN is already registered.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save admin profile.");
                return Problem("Could not create the admin account.");
            }
        }
        else
        {
            _logger.LogWarning("Resuming incomplete signup for AdminId={Id}.", profile.AdminId);
        }

        // Call Identity to create the login.
        HttpResponseMessage identityResponse;
        try
        {
            identityResponse = await _identity.CreateAccount(
                "admin", profile.AdminId, username, dto.password,
                dto.passwordRecoveryQuestion.Trim(), dto.passwordRecoveryAnswer.Trim());
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
                _logger.LogWarning("Identity timed out for AdminId={Id}. Profile preserved.", profile.AdminId);
            else
                _logger.LogError(ex, "Could not reach Identity for AdminId={Id}. Profile preserved.", profile.AdminId);

            return StatusCode(503, "The account service is temporarily unavailable. Please try again shortly.");
        }

        if (identityResponse.IsSuccessStatusCode)
            return Ok("created");

        var identityBody = await identityResponse.Content.ReadAsStringAsync();

        switch (identityResponse.StatusCode)
        {
            // Confirmed rejections: Identity checks these before writing anything,
            // so no credentials were saved. A profile created by this request is safe to
            // delete; a resumed one is kept, because it may belong to a completed signup.
            case System.Net.HttpStatusCode.BadRequest:
            case System.Net.HttpStatusCode.Conflict:
            case System.Net.HttpStatusCode.Unauthorized:
                _logger.LogWarning("Identity rejected AdminId={Id}: {Status}",
                    profile.AdminId, (int)identityResponse.StatusCode);
                if (!resumed) await DeleteProfile(profile.AdminId);
                if (identityResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return StatusCode(500, "Internal configuration error: internal API key mismatch.");
                return identityResponse.StatusCode == System.Net.HttpStatusCode.Conflict
                    ? Conflict(identityBody) : BadRequest(identityBody);

            default:
                // 5xx or unexpected status — Identity's DB write may have succeeded or failed.
                // Preserve the profile; do not attempt to delete it.
                _logger.LogError("Identity returned {Status} for AdminId={Id}. Profile preserved.",
                    (int)identityResponse.StatusCode, profile.AdminId);
                return StatusCode(503, "The account service is temporarily unavailable. Please try again shortly.");
        }
    }

    // Deletes the profile row on a confirmed Identity rejection (400/409/401).
    // Errors are logged, not rethrown.
    private async Task DeleteProfile(int adminId)
    {
        try
        {
            var row = await _db.Admins.FindAsync(adminId);
            if (row is not null) { _db.Admins.Remove(row); await _db.SaveChangesAsync(); }
        }
        catch (Exception ex)
        {
            // Log the failure clearly — the profile was NOT rolled back.
            _logger.LogError(ex,
                "Rollback failed for AdminId={Id}. Profile row still exists and needs manual cleanup.",
                adminId);
        }
    }
}
