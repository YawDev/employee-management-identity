using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.core.Interfaces
{
    public interface IUserRepository
    {
        Task<int> CreateAsync(User user);
        Task<int> CreateIdentityUserAsync(ApplicationUser user);
        Task<bool> DeleteAsync(Guid userId);
        Task<bool> ExistsAsync(Guid userId);
        Task<ApplicationUser?> GetByEmailAsync(string email);
        Task<ApplicationUser?> GetByIdAsync(Guid userId);
        Task<ApplicationUser?> GetByUserNameAsync(string userName);
        Task<ApplicationUser?> GetIdentityUserInfoAsync(Guid id);
        Task<ApplicationUser> UpdateAsync(ApplicationUser user);
        Task<bool> ValidateCredentialsAsync(string userName, string passwordHash);
    }
}