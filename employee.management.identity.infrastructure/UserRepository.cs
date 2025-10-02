
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.infrastructure
{
    public class UserRepository : IUserRepository
    {
        
        public UserRepository()
        {

        }
        // Implementation of IUserRepository methods
        public Task<ApplicationUser> CreateAsync(ApplicationUser user)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationUser?> GetByEmailAsync(string email)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationUser?> GetByIdAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationUser?> GetByUserNameAsync(string userName)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationUser> UpdateAsync(ApplicationUser user)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ValidateCredentialsAsync(string userName, string password)
        {
            throw new NotImplementedException();
        }
    }
}