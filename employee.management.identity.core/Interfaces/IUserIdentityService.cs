using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.DatabaseModels.QueryResults;
using employee.management.identity.models.Dtos;

namespace employee.management.identity.core.Interfaces
{
    public interface IUserIdentityService
    {
        Task<ApplicationUser> CreateUserAndIdentityAsync(CreateIdentityDTO user);
        Task<(ApplicationUser,bool)> ValidateUserCredentialsAsync(string userName, string password);
        Task<UserDTO?> GetUserByIdAsync(Guid userId);
        Task<IdentityUserDTO?> GetIdentityUserInfo(Guid userId);
        Task<string> GetUserRole(Guid identityUserId);
        Task<bool> DeleteUserAsync(Guid identityUserId);
        Task<bool> EditUserRoleAsync(Guid identityUserId, string newRole);
        Task<List<UserQueryResult>> GetAllUsersAsync();
    }
}
