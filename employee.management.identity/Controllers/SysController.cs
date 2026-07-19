using employee.management.identity.core.Interfaces;
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
    }
}
