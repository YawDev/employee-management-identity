using employee.management.identity.infrastructure;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.DatabaseModels.QueryResults;
using employee.management.identity.models.Dtos;

namespace employee.management.identity.core.Interfaces
{
    public interface IUserRepository
    {
        Task<int> CreateAsync(DomainUser user);
        Task<int> CreateIdentityUserAsync(ApplicationUser user);
        Task<bool> DeleteAsync(Guid userId);
        Task<bool> ExistsAsync(Guid userId);
        Task<IdentityUserDTO?> GetByEmailAsync(string email);
        Task<UserDTO?> GetByIdAsync(Guid userId);
        Task<string> GetUserRoleAsync(Guid identityUserId);
        Task<ApplicationUser?> GetByUserNameAsync(string userName);
        Task<IdentityUserDTO?> GetIdentityUserInfoAsync(Guid id);
        Task<ApplicationUser> UpdateAsync(ApplicationUser user);
        Task<bool> ValidateCredentialsAsync(string userName, string passwordHash);
        Task<int> EditUserRoleAsync(Guid identityUserId, string newRole);
        Task<List<UserQueryResult>> GetAllUsersQueryJoinedAsync();

    }
}