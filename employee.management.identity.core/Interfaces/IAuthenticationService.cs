using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;

namespace employee.management.identity.core.Business
{
    public interface IAuthenticationService
    {
        Task<ApplicationUser> CreateUserAndIdentity(CreateIdentityDTO user);
        Task<ApplicationUser> AuthenticateUser(AuthenticateIdentityDTO user);
    }
}