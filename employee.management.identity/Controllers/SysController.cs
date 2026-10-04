using System.Security.Claims;
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace employee.management.identity.Controllers
{
    [Authorize(Policy = "SysAdmin")]
    [ApiController]
    [Route("sys-api")]
    public class SysController(IUserIdentityService userIdentityService) : ControllerBase
    {
        private readonly IUserIdentityService _userIdentityService = userIdentityService;

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
    }
}
