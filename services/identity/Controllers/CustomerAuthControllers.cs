using AccountSecurity;
using Data;
using JwtTokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CustomerAuthNamespace
{
    // Customers sign in here. Their tokens carry the customer role, which the admin endpoints in the other services reject.
    [ApiController]
    [Route("api/users")]
    public class CustomerAuthController : ControllerBase
    {
        private readonly IdentityDbContext _db;
        private readonly JwtTokenService _tokens;
        private readonly ILogger<CustomerAuthController> _logger;

        public CustomerAuthController(IdentityDbContext db, JwtTokenService tokens, ILogger<CustomerAuthController> logger)
        {
            _db = db;
            _tokens = tokens;
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
                var customer = await _db.CustomerAccounts.FirstOrDefaultAsync(c => c.Username == username);
                var failure = await AccountFlows.CheckPassword(_db, customer, password);
                if (failure is not null)
                {
                    return failure;
                }
                var (token, expiresAt) = _tokens.Issue(customer!.Username, Roles.Customer, customer.CustomerId);
                return Ok(new { message = "Customer logged in successfully", username = customer.Username, role = Roles.Customer, customerId = customer.CustomerId, token, expiresAt });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log in customer '{Username}'.", username);
                return Problem("An error occurred while logging in.");
            }
        }

        [EnableRateLimiting("auth")]
        [HttpGet("recovery-question")]
        public async Task<IActionResult> RecoveryQuestion([FromQuery] string username)
        {
            username = username?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                return BadRequest("A username is required.");
            }
            return AccountFlows.RecoveryQuestion(await _db.CustomerAccounts.FirstOrDefaultAsync(c => c.Username == username), username);
        }

        [EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] RecoveryAnswerRequest request)
        {
            var username = request.username?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                return BadRequest("A username is required.");
            }
            return await AccountFlows.ResetPassword(_db, await _db.CustomerAccounts.FirstOrDefaultAsync(c => c.Username == username), request);
        }

        // Lets the customer's landing page confirm the sign-in and show who is signed in.
        [Authorize(Roles = Roles.Customer)]
        [HttpGet("me")]
        public IActionResult Me() => Ok(new { username = User.Identity?.Name, role = Roles.Customer });
    }
}
