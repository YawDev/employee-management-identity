using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;

namespace employee.management.identity.core.Business
{
    public interface IAuthenticationService
    {
        Task<ApplicationUser> CreateUserAndIdentity(CreateIdentityDTO user);
        Task<(ApplicationUser? user, string? accessToken, string? refreshToken)> AuthenticateUser(AuthenticateIdentityDTO user);
        Task<(ApplicationUser user, string newAccessToken, string newRefreshToken)> RefreshUserSession(string oldRefreshToken);
        
        /// <summary>
        /// Gets Domain User by Id
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>UserDTO if found, otherwise null</returns>
        Task<UserDTO?> GetUserByIdAsync(Guid userId);
    }
}
