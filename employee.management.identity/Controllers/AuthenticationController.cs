

using AutoMapper;
using employee.management.identity.Contracts.Request;
using employee.management.identity.core.Business;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace employee.management.identity.Controllers
{
    [ApiController]
    [Route("employee-management-identity/api")]
    public class AuthenticationController(IAuthenticationService authenticationService, IMapper mapper) : ControllerBase
    {

        private readonly IAuthenticationService _authenticationService = authenticationService;
        private readonly IMapper _mapper = mapper;


        [HttpPost("/login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Implement login logic
            return Ok();    
        }

        [HttpPost("/register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var identityDTO = _mapper.Map<CreateIdentityDTO>(request);
                await _authenticationService.CreateUserAndIdentity(identityDTO);
                return Ok();
            }
           catch(Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}