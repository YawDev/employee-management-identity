

using employee.management.identity.Contracts.Request;
using Microsoft.AspNetCore.Mvc;

namespace employee.management.identity.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthenticationController : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Implement login logic
            return Ok();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Implement registration logic
            return Ok();
        }
    }
}