namespace employee.management.identity.core.Business
{
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

    }
}