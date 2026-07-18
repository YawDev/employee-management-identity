namespace employee.management.identity.core.Business
{
    using employee.management.identity.core.Exceptions;
    using employee.management.identity.core.Interfaces;
    using employee.management.identity.models.DatabaseModels;
    using employee.management.identity.models.Dtos;
    public class AuthenticationService(IUserIdentityService userIdentityService, ITokenService tokenService) : IAuthenticationService
    {
        private readonly IUserIdentityService _userIdentityService = userIdentityService;
        private readonly ITokenService _tokenService = tokenService;
        public async Task<ApplicationUser> CreateUserAndIdentity(CreateIdentityDTO user)
        {
            ApplicationUser? newUser = null;
            try
            {
                newUser = await _userIdentityService.CreateUserAndIdentityAsync(user);
                if (newUser == null)
                {
                    throw new Exception("Failed to create user and identity");
                }
            }
            catch (Exception e)
            {
                throw;
            }
            return newUser;
        }

        public async Task<(ApplicationUser? user, string? accessToken, string? refreshToken)> AuthenticateUser(AuthenticateIdentityDTO user)
        {
            try
            {
                var (authenticatedUser, isSuccess) = await _userIdentityService.ValidateUserCredentialsAsync(user.UserName, user.Password);
                if (!isSuccess) throw new FailedAuthenticationException("Inxvalid user credentials.");

                var role = await _userIdentityService.GetUserRole(authenticatedUser.Id);
                var accessToken = _tokenService.GenerateAccessToken(authenticatedUser, role);
                var refreshToken = _tokenService.GenerateRefreshToken();
                await _tokenService.SaveRefreshTokenAsync(authenticatedUser.Id, refreshToken);
                return (authenticatedUser, accessToken, refreshToken);
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public async Task<(ApplicationUser user, string newAccessToken, string newRefreshToken)> RefreshUserSession(string oldRefreshToken)
        {
            var refreshToken = await _tokenService.GetAndValidateRefreshToken(oldRefreshToken);
            if (refreshToken == null || !refreshToken.IsActive)
                throw new UnauthorizedException("Invalid or expired refresh token.");

            var identityUser = refreshToken.IdentityUser
                ?? throw new UnauthorizedException("No user tied to this refresh token.");

            await _tokenService.RevokeRefreshToken(refreshToken);              // rotate: single-use

            var role = await _userIdentityService.GetUserRole(identityUser.Id);
            var newAccessToken = _tokenService.GenerateAccessToken(identityUser, role);
            var newRefreshToken = _tokenService.GenerateRefreshToken();
            await _tokenService.SaveRefreshTokenAsync(identityUser.Id, newRefreshToken);

            return (identityUser, newAccessToken, newRefreshToken);
        }

        public async Task<UserDTO?> GetUserByIdAsync(Guid userId)
        {
            var user = await _userIdentityService.GetUserByIdAsync(userId);
            return user;
        }
    }
}
