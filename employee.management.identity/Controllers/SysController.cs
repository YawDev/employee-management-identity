using System.Security.Claims;
using AutoMapper;
using employee.management.identity.Contracts.Request;
using employee.management.identity.core.Business;
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace employee.management.identity.Controllers
{
    [Authorize(Policy = "SysAdmin")]
    [ApiController]
    [Route("sys-api")]
    public class SysController(IUserIdentityService userIdentityService, IAuthenticationService authenticationService, IMapper mapper, IConfiguration configuration) : ControllerBase
    {
        private readonly IUserIdentityService _userIdentityService = userIdentityService;
        private readonly IAuthenticationService _authenticationService = authenticationService;
        private readonly IMapper _mapper = mapper;
        private readonly int _accessTokenExpirationMinutes = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 30;
        private readonly int _refreshTokenExpirationDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;

        private static CookieOptions Cookie(DateTimeOffset expires) => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None, // cross-site FE -> API; requires HTTPS
            Expires = expires
        };
        
        [HttpGet("get-all-users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userIdentityService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpDelete("delete-user/{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var isDeleted = await _userIdentityService.DeleteUserAsync(id);
            return Ok(new { IsDeleted = isDeleted });
        }

        [HttpPut("permissions/edit-role")]
        public async Task<IActionResult> EditUserRole([FromBody] EditUserRoleDto editUserRoleDto)
        {
            var isModified = await _userIdentityService.EditUserRoleAsync(editUserRoleDto.IdentityUserId, editUserRoleDto.NewRole);
            return Ok(new { IsModified = isModified });
        }

        /// <summary>
        /// Retrieves current session information for the authenticated system admin user.
        /// </summary>
        /// <returns>current session information for the authenticated system admin user</returns>
        [Authorize]
        [HttpGet("auth/admin/me")]
        public async Task<IActionResult> GetCurrentSystemAdminUser()
        {
            // Retrieve the pre-validated user from HttpContext
            var userId = HttpContext.User.Claims
            .Where(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")
            .Select(c => c.Value)
            .FirstOrDefault(v => Guid.TryParse(v, out _));

            if (userId == null) return Unauthorized();

            var user = await _userIdentityService.GetSystemAdminUserAsync(Guid.Parse(userId));
            return Ok(user);
        }

                /// <summary>
        /// Authenticates a system admin user and establishes a session with access and refresh tokens.
        /// </summary>
        /// <param name="request">The login request containing the system admin user's credentials.</param>
        /// <returns>The authenticated system admin user along with access and refresh tokens.</returns>
        [AllowAnonymous]
        [HttpPost("auth/login")]
        public async Task<IActionResult> SysLogin([FromBody] LoginRequest request)
        {
            var identityDTO = _mapper.Map<AuthenticateIdentityDTO>(request);
            // Bad credentials throw FailedAuthenticationException (401); a non-sys user throws
            // ForbiddenException (403). Both are mapped by ExceptionHandlingMiddleware, so no cookies are set.
            var (user, accessToken, refreshToken) = await _authenticationService.AuthenticateSystemAdminUser(identityDTO);

            Response.Cookies.Append("access_token", accessToken, Cookie(DateTimeOffset.UtcNow.AddMinutes(_accessTokenExpirationMinutes)));
            Response.Cookies.Append("refresh_token", refreshToken, Cookie(DateTimeOffset.UtcNow.AddDays(_refreshTokenExpirationDays)));

            return Ok(new { User = user });
        }
    }
}
