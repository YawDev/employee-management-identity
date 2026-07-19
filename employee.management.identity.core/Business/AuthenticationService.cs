namespace employee.management.identity.core.Business
{
    using employee.management.identity.core.Exceptions;
    using employee.management.identity.core.Interfaces;
    using employee.management.identity.models.DatabaseModels;
    using employee.management.identity.models.Dtos;
    using Microsoft.Extensions.Logging;

    public class AuthenticationService(
        IUserIdentityService userIdentityService,
        ITokenService tokenService,
        ILogger<AuthenticationService> logger) : IAuthenticationService
    {
        private readonly IUserIdentityService _userIdentityService = userIdentityService;
        private readonly ITokenService _tokenService = tokenService;
        private readonly ILogger<AuthenticationService> _logger = logger;

        public async Task<ApplicationUser> CreateUserAndIdentity(CreateIdentityDTO user)
        {
            return await _userIdentityService.CreateUserAndIdentityAsync(user)
                ?? throw new Exception("Failed to create user and identity");
        }

        public async Task<(ApplicationUser? user, string? accessToken, string? refreshToken)> AuthenticateUser(AuthenticateIdentityDTO user)
        {
            var (authenticatedUser, isSuccess) = await _userIdentityService.ValidateUserCredentialsAsync(user.UserName, user.Password);
            if (!isSuccess)
            {
                _logger.LogWarning("Failed login attempt for {UserName}", user.UserName);
                throw new FailedAuthenticationException("Invalid user credentials.");
            }

            var role = await _userIdentityService.GetUserRole(authenticatedUser.Id);
            var accessToken = _tokenService.GenerateAccessToken(authenticatedUser, role);
            var refreshToken = _tokenService.GenerateRefreshToken();
            await _tokenService.SaveRefreshTokenAsync(authenticatedUser.Id, refreshToken);

            _logger.LogInformation("User {UserId} authenticated; access token issued", authenticatedUser.Id);
            return (authenticatedUser, accessToken, refreshToken);
        }

        public async Task<(ApplicationUser user, string newAccessToken, string newRefreshToken)> RefreshUserSession(string oldRefreshToken)
        {
            var refreshToken = await _tokenService.GetAndValidateRefreshToken(oldRefreshToken);
            if (refreshToken == null || !refreshToken.IsActive)
            {
                _logger.LogWarning("Refresh rejected: invalid or expired refresh token");
                throw new UnauthorizedException("Invalid or expired refresh token.");
            }

            var identityUser = refreshToken.IdentityUser
                ?? throw new UnauthorizedException("No user tied to this refresh token.");

            await _tokenService.RevokeRefreshToken(refreshToken);              // rotate: single-use

            var role = await _userIdentityService.GetUserRole(identityUser.Id);
            var newAccessToken = _tokenService.GenerateAccessToken(identityUser, role);
            var newRefreshToken = _tokenService.GenerateRefreshToken();
            await _tokenService.SaveRefreshTokenAsync(identityUser.Id, newRefreshToken);

            _logger.LogInformation("Refresh token rotated for user {UserId}", identityUser.Id);
            return (identityUser, newAccessToken, newRefreshToken);
        }

        public async Task<UserDTO?> GetUserByIdAsync(Guid userId)
        {
            return await _userIdentityService.GetUserByIdAsync(userId);
        }
    }
}
