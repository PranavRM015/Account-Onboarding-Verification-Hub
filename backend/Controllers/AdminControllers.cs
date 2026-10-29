using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AdminRepo;
using AuditTrails;
using Tickets;
using PasswordHasherNamespace;
using Data;
using Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Admins;
using AccountSecurity;
using Flags;
using MockVendor;
using JwtTokens;
using RevokedTokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;


namespace AdminControlNamespace
{
    public class information
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
    public class InputFormInformation
    {
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string taxIdType { get; set; } = string.Empty;
        public string taxIdNumber { get; set; } = string.Empty;
        public string residentialAddress { get; set; } = string.Empty;
        public string countryOfResidence { get; set; } = string.Empty;
        public string countryOfCitizenship { get; set; } = string.Empty;
        public string occupation { get; set; } = string.Empty;
        public string Employer { get; set; } = string.Empty;
        public string typeOfAccount { get; set; } = string.Empty;
        public int? jointCustomerId { get; set; }
        // The admin signs the customer up, so the customer's login details are entered here too.
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string passwordRecoveryQuestion { get; set; } = string.Empty;
        public string passwordRecoveryAnswer { get; set; } = string.Empty;
    }
    public class AdminSignupInformation
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string firstName { get; set; } = string.Empty;
        public string? middleName { get; set; }
        public string lastName { get; set; } = string.Empty;
        public DateTime dateOfBirth { get; set; }
        public string gender { get; set; } = string.Empty;
        public string ssn { get; set; } = string.Empty;
        public bool isCitizen { get; set; }
        public string countryOfCitizenship { get; set; } = string.Empty;
        public string passwordRecoveryQuestion { get; set; } = string.Empty;
        public string passwordRecoveryAnswer { get; set; } = string.Empty;
    }
    [ApiController]
    [Authorize(Roles = Roles.Admin)]
    [Route("api/admins")]
    public class AdminSignupController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AdminSignupController> _logger;
        private readonly string _taxIdPepper;

        public AdminSignupController(AppDbContext db, IConfiguration configuration, ILogger<AdminSignupController> logger)
        {
            _db = db;
            _taxIdPepper = configuration["TaxIdHashPepper"]
                ?? throw new InvalidOperationException("'TaxIdHashPepper' is not configured.");
            _logger = logger;
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("signup")]
        public async Task<IActionResult> CreateAdmin([FromBody] AdminSignupInformation info)
        {
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;
            var ssn = new string((info.ssn ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

            if (username.Length < 3 || username.Length > 40)
            {
                return BadRequest("A username of 3 to 40 characters is required.");
            }
            if (!PasswordPolicy.IsValid(password))
            {
                return BadRequest(PasswordPolicy.Message);
            }
            if (ssn.Length != 9 || !ssn.All(char.IsDigit))
            {
                return BadRequest("An SSN of exactly 9 digits is required.");
            }
            var admin = new Admin(username, PasswordHasher.PasswordHash(password))
            {
                FirstName = info.firstName?.Trim() ?? string.Empty,
                MiddleName = string.IsNullOrWhiteSpace(info.middleName) ? null : info.middleName.Trim(),
                LastName = info.lastName?.Trim() ?? string.Empty,
                DateOfBirth = info.dateOfBirth.Date,
                Gender = info.gender?.Trim() ?? string.Empty,
                SSNHash = PasswordHasher.TaxIdHash(ssn, _taxIdPepper),
                IsCitizen = info.isCitizen,
                CountryOfCitizenship = info.countryOfCitizenship?.Trim().ToUpperInvariant() ?? string.Empty,
                PasswordRecoveryQuestion = info.passwordRecoveryQuestion?.Trim() ?? string.Empty,
                PasswordRecoveryAnswer = string.IsNullOrWhiteSpace(info.passwordRecoveryAnswer)
                    ? string.Empty
                    : RecoveryQuestions.Hash(info.passwordRecoveryAnswer),
            };
            var validation = new AdminValidator().Validate(admin);
            if (!validation.IsValid)
            {
                return BadRequest(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
            }
            try
            {
                if (await _db.Admins.AnyAsync(a => a.Username == username))
                {
                    return Conflict("That username is already taken.");
                }
                if (await _db.Admins.AnyAsync(a => a.SSNHash == admin.SSNHash))
                {
                    return Conflict("An admin with that SSN already exists.");
                }
                _db.Admins.Add(admin);
                await _db.SaveChangesAsync();
                return Ok("created");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                return Conflict("That username or SSN is already registered.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create admin '{Username}'.", username);
                return Problem("Could not create the admin account.");
            }
        }
    }
    [ApiController]
    [Route("api/admins")]
    public class AdminLoginController : ControllerBase
    {
        private readonly ILogger<AdminLoginController> _logger;
        private readonly JwtTokenService _tokens;
        private readonly AppDbContext _db;

        public AdminLoginController(ILogger<AdminLoginController> logger, JwtTokenService tokens, AppDbContext db)
        {
            _tokens = tokens;
            _db = db;
            _logger = logger;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] information info)
        {
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;
            if (username.Length == 0 || password.Length == 0)
            {
                return BadRequest("Username and password are required.");
            }
            try
            {
                var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == username);
                var failure = await AccountFlows.CheckPassword(_db, admin, password);
                if (failure is not null)
                {
                    return failure;
                }
                var storedUsername = admin!.Username;
                // The JWT is the main sign-in; the session cookie is kept as a fallback. Both carry the admin's name,
                // which the controllers read for Ticket.AssignedTo, and the Admin role.
                var identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, storedUsername), new Claim(ClaimTypes.Role, Roles.Admin) },
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                var (token, expiresAt) = _tokens.Issue(storedUsername, Roles.Admin);
                return Ok(new { message = "Admin logged in successfully", username = storedUsername, role = Roles.Admin, token, expiresAt });
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
            return AccountFlows.RecoveryQuestion(await _db.Admins.FirstOrDefaultAsync(a => a.Username == username), username);
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
            return await AccountFlows.ResetPassword(_db, await _db.Admins.FirstOrDefaultAsync(a => a.Username == username), request);
        }

        // Revokes the JWT that made this request and clears the cookie, so neither can be used again. Admins and users both use it.
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
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Ok(new { message = "Logged out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log out.");
                return Problem("An error occurred while logging out.");
            }
        }
    }
    [ApiController]
    [Authorize(Roles = Roles.Admin)]
    [Route("api/admins/inputForm")]
    public class InputFormController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly string _connectionString;
        private readonly ILogger<InputFormController> _logger;
        private readonly string _taxIdPepper;

        public InputFormController(AppDbContext db, IConfiguration configuration, ILogger<InputFormController> logger)
        {
            _db = db;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            _taxIdPepper = configuration["TaxIdHashPepper"]
                ?? throw new InvalidOperationException("'TaxIdHashPepper' is not configured.");
            _logger = logger;
        }

        // The form calls this before submitting a joint account.
        [HttpGet("customers/{customerId:int}/exists")]
        public IActionResult CustomerExists(int customerId)
        {
            try
            {
                var adminRepository = new AdminRepository(_connectionString);
                return Ok(new { exists = adminRepository.CustomerExists(customerId) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check whether customer {CustomerId} exists.", customerId);
                return Problem("Could not check the customer.");
            }
        }

        [HttpPost]
        public IActionResult inputForm([FromBody] InputFormInformation info)
        {
            var taxIdNumber = info.taxIdNumber?.Trim() ?? string.Empty;
            if (taxIdNumber.Length == 0)
            {
                return BadRequest("A tax ID number is required.");
            }
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
            if (accountType == "Joint" && (jointCustomerId is null || !_db.Customers.Any(c => c.CustomerId == jointCustomerId)))
            {
                return BadRequest("The joint customer does not exist.");
            }
            var taxIdHash = PasswordHasher.TaxIdHash(taxIdNumber, _taxIdPepper);
            try
            {
                var digits = new string(taxIdNumber.Where(char.IsLetterOrDigit).ToArray());
                var customer = new Customer
                {
                    FirstName = info.FirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(info.MiddleName) ? null : info.MiddleName.Trim(),
                    LastName = info.LastName.Trim(),
                    DateOfBirth = info.DateOfBirth.Date,
                    Gender = info.Gender.Trim(),
                    TaxIdType = info.taxIdType.Trim().ToUpperInvariant(),
                    TaxIdHash = taxIdHash,
                    TaxIdLastFourDigits = digits.Length >= 4 ? digits[^4..] : digits,
                    ResidentialAddress = info.residentialAddress.Trim(),
                    CountryOfResidence = info.countryOfResidence.Trim().ToUpperInvariant(),
                    CountryOfCitizenship = info.countryOfCitizenship.Trim().ToUpperInvariant(),
                    Occupation = info.occupation.Trim(),
                    Employer = info.Employer.Trim(),
                    Username = info.username?.Trim() ?? string.Empty,
                    PasswordHash = PasswordHasher.PasswordHash(info.password ?? string.Empty),
                    PasswordRecoveryQuestion = info.passwordRecoveryQuestion?.Trim() ?? string.Empty,
                    PasswordRecoveryAnswer = string.IsNullOrWhiteSpace(info.passwordRecoveryAnswer)
                        ? string.Empty
                        : RecoveryQuestions.Hash(info.passwordRecoveryAnswer),
                };
                if (!PasswordPolicy.IsValid(info.password))
                {
                    return BadRequest(PasswordPolicy.Message);
                }
                var validation = new CustomerValidator().Validate(customer);
                if (!validation.IsValid)
                {
                    return BadRequest(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
                }

                // Customer, ticket and audit row are saved in one transaction: all succeed or none do.
                using var transaction = _db.Database.BeginTransaction();
                _db.Customers.Add(customer);
                _db.SaveChanges(); // generates customer.CustomerId

                var ticket = new Ticket(customer.CustomerId, accountType, assignedTo, jointCustomerId)
                {
                    TicketStatus = "Review", // moves straight from Submitted to Review
                };
                var ticketValidation = new TicketValidator().Validate(ticket);
                if (!ticketValidation.IsValid)
                {
                    return BadRequest(string.Join(" ", ticketValidation.Errors.Select(e => e.ErrorMessage)));
                }
                _db.Tickets.Add(ticket);
                _db.SaveChanges(); // generates ticket.TicketId

                var admin = _db.Admins.FirstOrDefault(a => a.Username == assignedTo);
                if(admin is null){
                    return Unauthorized("Your admin account is no longer active.");
                }

                var vendor = new Vendor(customer);
                var vendorResponse = vendor.Verify(customer, ticket.TicketId);
                var raisedFlags = new[]
                {
                    vendor.CheckVerification(admin, vendorResponse, ticket.TicketId),
                    vendor.CheckOFACVerification(admin, customer, ticket.TicketId),
                    vendor.CheckCountryTier(admin, customer, ticket.TicketId),
                    vendor.CheckIllicitSubstances(admin, customer, ticket.TicketId),
                }.OfType<Flag>().ToList();
                ticket.RiskRating = vendor.FinalRiskRating();
                _db.VendorResponses.Add(vendorResponse);
                _db.Flags.AddRange(raisedFlags);

                var audit = new AuditTrail(ticket.TicketId, customer.CustomerId, "Submitted", "Review",
                    "Ticket created", "Submission received");
                var auditValidation = new AuditTrailValidator().Validate(audit);
                if (!auditValidation.IsValid)
                {
                    return BadRequest(string.Join(" ", auditValidation.Errors.Select(e => e.ErrorMessage)));
                }
                _db.AuditTrails.Add(audit);
                _db.AuditTrails.Add(new AuditTrail(ticket.TicketId, customer.CustomerId, "Review", "Review", "Validation pipeline completed", $"{raisedFlags.Count} flag(s) raised; risk rating {ticket.RiskRating}"));
                _db.SaveChanges();

                transaction.Commit();
                return Ok(new { message = "Customer saved", customer.CustomerId, ticket.TicketId, verified = raisedFlags.Count == 0, riskRating = ticket.RiskRating, flags = raisedFlags.Select(f => f.ViolationType), vendorResponse = new { vendorResponse.Score, vendorResponse.RiskCodes } });

            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
            {
                _db.ChangeTracker.Clear();
                if (_db.Customers.Any(c => c.Username == info.username.Trim()))
                {
                    return Conflict(new { message = "That username is already taken." });
                }
                // Tell the admin which customer already has this tax ID so they can use it as a joint holder.
                var existingId = _db.Customers.Where(c => c.TaxIdHash == taxIdHash).Select(c => c.CustomerId).FirstOrDefault();
                return Conflict(new { message = "A customer with that tax ID already exists.", customerId = existingId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save the input form.");
                return Problem("Could not save the customer.");
            }
        }
    }
}
