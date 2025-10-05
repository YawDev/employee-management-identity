

using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.core.Interfaces
{
    public interface IUserRepository
    {
        // Basic CRUD operations
        Task<ApplicationUser?> GetByIdAsync(string userId);
        Task<ApplicationUser?> GetByEmailAsync(string email);
        Task<ApplicationUser?> GetByUserNameAsync(string userName);
        Task<ApplicationUser> CreateAsync(ApplicationUser user);
        Task<ApplicationUser> UpdateAsync(ApplicationUser user);
        Task<bool> ValidateCredentialsAsync(string userName, string password);  
        Task<bool> DeleteAsync(string userId);
        Task<bool> ExistsAsync(string userId);
    }

}