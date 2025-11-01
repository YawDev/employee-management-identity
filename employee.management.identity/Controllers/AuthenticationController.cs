

using AutoMapper;
using employee.management.identity.Contracts.Request;
using employee.management.identity.core.Business;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace employee.management.identity.Controllers
{
    [ApiController]
    [Route("api")]
    public class AuthenticationController(IAuthenticationService authenticationService, IMapper mapper, SignInManager<IdentityUser> signInManager) : ControllerBase
    {
        private readonly SignInManager<IdentityUser> _signInManager = signInManager;
        private readonly IAuthenticationService _authenticationService = authenticationService;
        private readonly IMapper _mapper = mapper;


        [HttpPost("/login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var identityDTO = _mapper.Map<AuthenticateIdentityDTO>(request);
            await _authenticationService.AuthenticateUser(identityDTO);
            await _signInManager.SignInAsync(new IdentityUser { UserName = request.UserName }, isPersistent: false);
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

        [HttpPost("/logout")]
        public async Task<IActionResult> Logout([FromBody] LoginRequest request)
        {
            await _signInManager.SignOutAsync();
            return Ok();
        }
    }
}