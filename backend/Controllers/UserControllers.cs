using System.Security.Claims;
using AccountSecurity;
using Customers;
using Data;
using JwtTokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PasswordHasherNamespace;

namespace UserControlNamespace
{
    public class UserSignupInformation
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string firstName { get; set; } = string.Empty;
        public string? middleName { get; set; }
        public string lastName { get; set; } = string.Empty;
        public DateTime dateOfBirth { get; set; }
        public string gender { get; set; } = string.Empty;
        public string taxIdType { get; set; } = string.Empty;
        public string taxIdNumber { get; set; } = string.Empty;
        public string residentialAddress { get; set; } = string.Empty;
        public string countryOfResidence { get; set; } = string.Empty;
        public string countryOfCitizenship { get; set; } = string.Empty;
        public string occupation { get; set; } = string.Empty;
        public string employer { get; set; } = string.Empty;
        public string passwordRecoveryQuestion { get; set; } = string.Empty;
        public string passwordRecoveryAnswer { get; set; } = string.Empty;
    }

    public class UserLoginInformation
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    // Customers sign themselves up and log in here. Their tokens carry the User role, which the admin endpoints reject.
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly JwtTokenService _tokens;
        private readonly ILogger<UserController> _logger;
        private readonly string _taxIdPepper;

        public UserController(AppDbContext db, JwtTokenService tokens, IConfiguration configuration, ILogger<UserController> logger)
        {
            _db = db;
            _tokens = tokens;
            _taxIdPepper = configuration["TaxIdHashPepper"]
                ?? throw new InvalidOperationException("'TaxIdHashPepper' is not configured.");
            _logger = logger;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] UserSignupInformation info)
        {
            var username = info.username?.Trim() ?? string.Empty;
            var taxIdNumber = info.taxIdNumber?.Trim() ?? string.Empty;
            if (taxIdNumber.Length == 0)
            {
                return BadRequest("A tax ID number is required.");
            }
            if (!PasswordPolicy.IsValid(info.password))
            {
                return BadRequest(PasswordPolicy.Message);
            }
            var taxIdHash = PasswordHasher.TaxIdHash(taxIdNumber, _taxIdPepper);
            var digits = new string(taxIdNumber.Where(char.IsLetterOrDigit).ToArray());
            var customer = new Customer
            {
                FirstName = info.firstName?.Trim() ?? string.Empty,
                MiddleName = string.IsNullOrWhiteSpace(info.middleName) ? null : info.middleName.Trim(),
                LastName = info.lastName?.Trim() ?? string.Empty,
                DateOfBirth = info.dateOfBirth.Date,
                Gender = info.gender?.Trim() ?? string.Empty,
                TaxIdType = info.taxIdType?.Trim().ToUpperInvariant() ?? string.Empty,
                TaxIdHash = taxIdHash,
                TaxIdLastFourDigits = digits.Length >= 4 ? digits[^4..] : digits,
                ResidentialAddress = info.residentialAddress?.Trim() ?? string.Empty,
                CountryOfResidence = info.countryOfResidence?.Trim().ToUpperInvariant() ?? string.Empty,
                CountryOfCitizenship = info.countryOfCitizenship?.Trim().ToUpperInvariant() ?? string.Empty,
                Occupation = info.occupation?.Trim() ?? string.Empty,
                Employer = info.employer?.Trim() ?? string.Empty,
                Username = username,
                PasswordHash = PasswordHasher.PasswordHash(info.password!),
                PasswordRecoveryQuestion = info.passwordRecoveryQuestion?.Trim() ?? string.Empty,
                PasswordRecoveryAnswer = string.IsNullOrWhiteSpace(info.passwordRecoveryAnswer)
                    ? string.Empty
                    : RecoveryQuestions.Hash(info.passwordRecoveryAnswer),
            };
            var validation = new CustomerValidator().Validate(customer);
            if (!validation.IsValid)
            {
                return BadRequest(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
            }
            try
            {
                if (await _db.Customers.AnyAsync(c => c.Username == username))
                {
                    return Conflict("That username is already taken.");
                }
                if (await _db.Customers.AnyAsync(c => c.TaxIdHash == taxIdHash))
                {
                    return Conflict("A customer with that tax ID already exists.");
                }
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();
                return Ok("created");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                return Conflict("That username or tax ID is already registered.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create user '{Username}'.", username);
                return Problem("Could not create the account.");
            }
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginInformation info)
        {
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;
            if (username.Length == 0 || password.Length == 0)
            {
                return BadRequest("Username and password are required.");
            }
            try
            {
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Username == username);
                var failure = await AccountFlows.CheckPassword(_db, customer, password);
                if (failure is not null)
                {
                    return failure;
                }
                var storedUsername = customer!.Username;
                var identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, storedUsername), new Claim(ClaimTypes.Role, Roles.User) },
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                var (token, expiresAt) = _tokens.Issue(storedUsername, Roles.User);
                return Ok(new { message = "User logged in successfully", username = storedUsername, role = Roles.User, token, expiresAt });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log in user '{Username}'.", username);
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
            return AccountFlows.RecoveryQuestion(await _db.Customers.FirstOrDefaultAsync(c => c.Username == username), username);
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
            return await AccountFlows.ResetPassword(_db, await _db.Customers.FirstOrDefaultAsync(c => c.Username == username), request);
        }

        // Lets the user's landing page confirm the sign-in and show who is signed in.
        [Authorize(Roles = Roles.User)]
        [HttpGet("me")]
        public IActionResult Me() => Ok(new { username = User.Identity?.Name, role = Roles.User });
    }
}
