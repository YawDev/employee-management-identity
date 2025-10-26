using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;

namespace employee.management.identity.core.Interfaces
{
    public interface IUserIdentityService
    {
        Task<ApplicationUser> CreateUserAndIdentityAsync(CreateIdentityDTO user);
    }
}
