using AutoMapper;
using employee.management.identity.ActionFilters;
using employee.management.identity.Contracts.Request;
using employee.management.identity.core.Business;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace employee.management.identity.Controllers
{
    [ApiController]
    [Route("api")]
    public class AuthenticationController(IAuthenticationService authenticationService, IMapper mapper, SignInManager<ApplicationUser> signInManager) : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
        private readonly IAuthenticationService _authenticationService = authenticationService;
        private readonly IMapper _mapper = mapper;


        [HttpPost("auth/login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var identityDTO = _mapper.Map<AuthenticateIdentityDTO>(request);
            var user = await _authenticationService.AuthenticateUser(identityDTO);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return Ok(_mapper.Map<IdentityUserDTO>(user));
        }

        [HttpPost("auth/register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var identityDTO = _mapper.Map<CreateIdentityDTO>(request);
            await _authenticationService.CreateUserAndIdentity(identityDTO);
            return Ok("User registered successfully");
        }

        [HttpPost("auth/logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok("User logged out successfully");
        }

        
        /// <summary>
        /// Retrieves user information for the authenticated user.
        /// The id is passed as a parameter and used to retrieve data for the signed-in user.
        /// </summary>
        /// <param name="id">The unique identifier of the identity user</param>
        /// <returns>User information for the authenticated user</returns>
        [IdentityFilter]
        [HttpGet("auth/user/{id}")]
        public async Task<IActionResult> GetUserInfo(Guid id)
        {
            var user = await _authenticationService.GetUserByIdAsync(id);
            return Ok(user);
        }

        /// <summary>
        /// Retrieves identity information for the authenticated user.
        /// The id is passed as a parameter and used to retrieve data for the signed-in user.
        /// </summary>
        /// <param name="id">The unique identifier of the identity user</param>
        /// <returns>Identity information for the authenticated user</returns>
        [IdentityFilter]
        [HttpGet("auth/identity/{id}")]
        public async Task<IActionResult> GetIdentityInfo(Guid id)
        {
            // Retrieve the pre-validated user from HttpContext
            var identityUser = HttpContext.Items["AuthenticatedIdentity"] as IdentityUserDTO;
            return Ok(identityUser);
        }
    }
}