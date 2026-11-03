using System.Security.Cryptography;
using System.Text;
using AccountSecurity;
using Accounts;
using Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PasswordHasherNamespace;

namespace InternalAccountsNamespace
{
    // Only other services call /internal. They prove who they are with the shared key in the X-Internal-Key header.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class InternalKeyAttribute : Attribute, IAuthorizationFilter
    {
        public const string Header = "X-Internal-Key";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var expected = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Internal:ApiKey"] ?? string.Empty;
            var supplied = context.HttpContext.Request.Headers[Header].ToString();
            // Hashing first makes both sides the same length, so the comparison takes the same time whatever was sent.
            var matches = expected.Length > 0 && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
            if (!matches)
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }

    // The intake service has already saved the profile and got the id; it asks for the login to be created under that id.
    public class CreateAccountRequest
    {
        public string role { get; set; } = string.Empty;
        public int id { get; set; }
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string passwordRecoveryQuestion { get; set; } = string.Empty;
        public string passwordRecoveryAnswer { get; set; } = string.Empty;
    }

    [ApiController]
    [InternalKey]
    [Route("internal/accounts")]
    public class InternalAccountsController : ControllerBase
    {
        private readonly IdentityDbContext _db;
        private readonly ILogger<InternalAccountsController> _logger;

        public InternalAccountsController(IdentityDbContext db, ILogger<InternalAccountsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private static string NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() ?? string.Empty;
        private static bool KnownRole(string role) => role == Roles.Admin || role == Roles.Customer;

        // Lets the intake service reject a taken username before it saves a profile. It is only a courtesy: the create call below is the real check.
        [HttpGet("username-available")]
        public async Task<IActionResult> UsernameAvailable([FromQuery] string role, [FromQuery] string username)
        {
            role = NormalizeRole(role);
            username = username?.Trim() ?? string.Empty;
            if (!KnownRole(role) || username.Length == 0)
            {
                return BadRequest("A role (admin or customer) and a username are required.");
            }
            var taken = role == Roles.Admin
                ? await _db.AdminAccounts.AnyAsync(a => a.Username == username)
                : await _db.CustomerAccounts.AnyAsync(c => c.Username == username);
            return Ok(new { available = !taken });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountRequest info)
        {
            var role = NormalizeRole(info.role);
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;
            var question = info.passwordRecoveryQuestion?.Trim() ?? string.Empty;
            if (!KnownRole(role))
            {
                return BadRequest("Role must be admin or customer.");
            }
            if (info.id <= 0)
            {
                return BadRequest("The id from the intake service is required.");
            }
            if (username.Length < 3 || username.Length > 40)
            {
                return BadRequest("A username of 3 to 40 characters is required.");
            }
            if (!PasswordPolicy.IsValid(password))
            {
                return BadRequest(PasswordPolicy.Message);
            }
            if (!RecoveryQuestions.IsValid(question) || string.IsNullOrWhiteSpace(info.passwordRecoveryAnswer))
            {
                return BadRequest("Choose one of the password recovery questions and give an answer.");
            }
            var passwordHash = PasswordHasher.PasswordHash(password);
            var answerHash = RecoveryQuestions.Hash(info.passwordRecoveryAnswer);
            try
            {
                if (role == Roles.Admin)
                {
                    if (await _db.AdminAccounts.AnyAsync(a => a.Username == username))
                    {
                        return Conflict("That username is already taken.");
                    }
                    if (await _db.AdminAccounts.AnyAsync(a => a.AdminId == info.id))
                    {
                        return Conflict("An account already exists for that admin.");
                    }
                    _db.AdminAccounts.Add(new AdminAccount
                    {
                        AdminId = info.id,
                        Username = username,
                        PasswordHash = passwordHash,
                        PasswordRecoveryQuestion = question,
                        PasswordRecoveryAnswer = answerHash,
                    });
                }
                else
                {
                    if (await _db.CustomerAccounts.AnyAsync(c => c.Username == username))
                    {
                        return Conflict("That username is already taken.");
                    }
                    if (await _db.CustomerAccounts.AnyAsync(c => c.CustomerId == info.id))
                    {
                        return Conflict("An account already exists for that customer.");
                    }
                    _db.CustomerAccounts.Add(new CustomerAccount
                    {
                        CustomerId = info.id,
                        Username = username,
                        PasswordHash = passwordHash,
                        PasswordRecoveryQuestion = question,
                        PasswordRecoveryAnswer = answerHash,
                    });
                }
                await _db.SaveChangesAsync();
                return Ok(new { role, id = info.id, username });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                return Conflict("That username or id is already registered.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create {Role} account '{Username}'.", role, username);
                return Problem("Could not create the account.");
            }
        }
    }
}
