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
    }
}
