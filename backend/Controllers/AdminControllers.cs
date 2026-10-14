using Microsoft.AspNetCore.Mvc;

namespace AdminControllers
{
    public class AdminInformation
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
    [ApiController]
    [Route("api/users")]
    public class AdminController : ControllerBase
    {
        [HttpPost]
        public IActionResult CreateAdmin([FromBody] AdminInformation adminInfo)
        {
            adminInfo.username = adminInfo.username.Trim();
            adminInfo.password = adminInfo.password.Trim();
            return Ok(new { message = "Admin created successfully", admin = adminInfo });
        }
    }
}