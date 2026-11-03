using AccountSecurity;
using Data;
using JwtTokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RevokedTokens;

namespace AdminAuthNamespace
{
    // Admins sign in here. Signing up is done through the intake service, which creates the account through /internal/accounts.
    [ApiController]
    [Route("api/admins")]
    public class AdminAuthController : ControllerBase
    {
        private readonly ILogger<AdminAuthController> _logger;
        private readonly JwtTokenService _tokens;
        private readonly IdentityDbContext _db;

        public AdminAuthController(ILogger<AdminAuthController> logger, JwtTokenService tokens, IdentityDbContext db)
        {
            _tokens = tokens;
            _db = db;
            _logger = logger;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginInformation info)
        {
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;
            if (username.Length == 0 || password.Length == 0)
            {
                return BadRequest("Username and password are required.");
            }
            try
            {
                var admin = await _db.AdminAccounts.FirstOrDefaultAsync(a => a.Username == username);
                var failure = await AccountFlows.CheckPassword(_db, admin, password);
                if (failure is not null)
                {
                    return failure;
                }
                var (token, expiresAt) = _tokens.Issue(admin!.Username, Roles.Admin, admin.AdminId);
                return Ok(new { message = "Admin logged in successfully", username = admin.Username, role = Roles.Admin, adminId = admin.AdminId, token, expiresAt });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log in admin '{Username}'.", username);
                return Problem("An error occurred while logging in.");
            }
        }

        // Step 1 of password recovery: the question to answer.
        [EnableRateLimiting("auth")]
        [HttpGet("recovery-question")]
        public async Task<IActionResult> RecoveryQuestion([FromQuery] string username)
        {
            username = username?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                return BadRequest("A username is required.");
            }
            return AccountFlows.RecoveryQuestion(await _db.AdminAccounts.FirstOrDefaultAsync(a => a.Username == username), username);
        }

        // Step 2: the answer plus the new password.
        [EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] RecoveryAnswerRequest request)
        {
            var username = request.username?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                return BadRequest("A username is required.");
            }
            return await AccountFlows.ResetPassword(_db, await _db.AdminAccounts.FirstOrDefaultAsync(a => a.Username == username), request);
        }

        // Revokes the JWT that made this request so it cannot be used again. Admins and customers both use it.
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var jti = User.FindFirst("jti")?.Value;
                var expiry = User.FindFirst("exp")?.Value;
                if (!string.IsNullOrEmpty(jti) && long.TryParse(expiry, out var exp)
                    && !await _db.RevokedTokens.AnyAsync(t => t.Jti == jti))
                {
                    _db.RevokedTokens.Add(new RevokedToken(jti, DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime));
                    await _db.SaveChangesAsync();
                }
                // Revoked tokens that have expired anyway no longer need to be remembered.
                await _db.RevokedTokens.Where(t => t.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync();
                return Ok(new { message = "Logged out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log out.");
                return Problem("An error occurred while logging out.");
            }
        }
    }
}
