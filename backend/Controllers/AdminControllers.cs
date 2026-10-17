using Microsoft.AspNetCore.Mvc;
using AdminRepo;
using Microsoft.Data.SqlClient;

namespace AdminControlNamespace
{
    public class information
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
    [ApiController]
    [Route("api/admins")]
    public class AdminSignupController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminSignupController> _logger;

        public AdminSignupController(IConfiguration configuration, ILogger<AdminSignupController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            _logger = logger;
        }

        [HttpPost("signup")]
        public IActionResult CreateAdmin([FromBody] information info)
        {
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

        [HttpPost("login")]
        public IActionResult Login([FromBody] information info)
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
                return Ok(new { message = "Admin logged in successfully", username });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log in admin '{Username}'.", username);
                return Problem("An error occurred while logging in.");
            }
        }
    }
}
