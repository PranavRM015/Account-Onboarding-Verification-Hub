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
using Flags;
using MockVendor;


namespace AdminControlNamespace
{
    public class information
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
    public class SignupInformation : information
    {
        public string signupCode { get; set; } = string.Empty;
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
    }
    [ApiController]
    [Authorize]
    [Route("api/admins")]
    public class AdminSignupController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _signupCode;
        private readonly ILogger<AdminSignupController> _logger;

        public AdminSignupController(IConfiguration configuration, ILogger<AdminSignupController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            _signupCode = configuration["Signup:Code"] ?? string.Empty;
            _logger = logger;
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("signup")]
        public IActionResult CreateAdmin([FromBody] SignupInformation info)
        {
            if (_signupCode.Length == 0)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Signup is disabled because no signup code is configured.");
            }
            var suppliedCode = Encoding.UTF8.GetBytes(info.signupCode ?? string.Empty);
            if (!CryptographicOperations.FixedTimeEquals(suppliedCode, Encoding.UTF8.GetBytes(_signupCode)))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "That signup code is not valid.");
            }
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;

            if (username.Length == 0 || username.Length > 100 || password.Length == 0)
            {
                return BadRequest("A username (max 100 characters) and a password are required.");
            }
            try
            {
                var adminRepository = new AdminRepository(_connectionString);
                adminRepository.AddAdmin(username, password);
                return Ok("created");
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // Unique constraint violation
            {
                return Conflict("That username is already taken.");
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
        private readonly string _connectionString;
        private readonly ILogger<AdminLoginController> _logger;

        public AdminLoginController(IConfiguration configuration, ILogger<AdminLoginController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
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
                var adminRepository = new AdminRepository(_connectionString);
                if (!adminRepository.ValidateAdmin(username, password))
                {
                    return Unauthorized("Invalid username or password.");
                }
                var storedUsername = adminRepository.GetStoredUsername(username) ?? username;
                // Session cookie; the input form controller reads the admin's name from it for Ticket.AssignedTo.
                var identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, storedUsername) },
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return Ok(new { message = "Admin logged in successfully", username = storedUsername });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log in admin '{Username}'.", username);
                return Problem("An error occurred while logging in.");
            }
        }
    }
    [ApiController]
    [Authorize]
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
                };
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
                // Tell the admin which customer already has this tax ID so they can use it as a joint holder.
                _db.ChangeTracker.Clear();
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
