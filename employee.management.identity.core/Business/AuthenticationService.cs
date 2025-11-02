namespace employee.management.identity.core.Business
{
    using employee.management.identity.core.Exceptions;
    using employee.management.identity.core.Interfaces;
    using employee.management.identity.models.DatabaseModels;
    using employee.management.identity.models.Dtos;

    public class AuthenticationService(IUserIdentityService userIdentityService) : IAuthenticationService
    {
        private readonly IUserIdentityService _userIdentityService = userIdentityService;

        public async Task<ApplicationUser> CreateUserAndIdentity(CreateIdentityDTO user)
        {
            ApplicationUser? newUser = null;
            try
            {
                newUser  = await _userIdentityService.CreateUserAndIdentityAsync(user);
                if(newUser == null)
                {
                    throw new Exception("Failed to create user and identity");
                }
            }
            catch(Exception e)
            {
                throw;
            }
            return newUser;
        }

        public async Task<ApplicationUser> AuthenticateUser(AuthenticateIdentityDTO user)
        {
            try
            {
               var( authenticatedUser, isSuccess) = await _userIdentityService.ValidateUserCredentialsAsync(user.UserName, user.Password);
                if(!isSuccess) throw new FailedAuthenticationException("Invalid user credentials.");

                return authenticatedUser;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public async Task<UserDTO?> GetUserByIdAsync(Guid userId)
        {
            var user = await _userIdentityService.GetUserByIdAsync(userId);
            return user;
        }
    }
}