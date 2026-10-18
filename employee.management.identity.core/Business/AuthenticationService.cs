namespace employee.management.identity.core.Business
{
    using employee.management.identity.core.Exceptions;
    using employee.management.identity.core.Interfaces;
    using employee.management.identity.models.Constants;
    using employee.management.identity.models.DatabaseModels;
    using employee.management.identity.models.Dtos;
    using Microsoft.Extensions.Logging;
    using AutoMapper; 

    public class AuthenticationService(
        IUserIdentityService userIdentityService,
        ITokenService tokenService,
        ILogger<AuthenticationService> logger,
        IMapper mapper) : IAuthenticationService
    {
        private readonly IUserIdentityService _userIdentityService = userIdentityService;
        private readonly ITokenService _tokenService = tokenService;
        private readonly ILogger<AuthenticationService> _logger = logger;
        private readonly IMapper _mapper = mapper;
        public async Task<ApplicationUser> CreateUserAndIdentity(CreateIdentityDTO user)
        {
            return await _userIdentityService.CreateUserAndIdentityAsync(user)
                ?? throw new Exception("Failed to create user and identity");
        }

        public Task<(IdentityUserDTO user, string accessToken, string refreshToken)> AuthenticateUser(AuthenticateIdentityDTO user)
            => AuthenticateWithRoleAsync(user, requiredRole: null);

        public Task<(IdentityUserDTO user, string accessToken, string refreshToken)> AuthenticateSystemAdminUser(AuthenticateIdentityDTO user)
            => AuthenticateWithRoleAsync(user, RoleConstants.SystemAdmin);

        // Password is checked before role, so a username alone reveals nothing (401); a correct
        // password without the required role gets 403.
        private async Task<(IdentityUserDTO user, string accessToken, string refreshToken)> AuthenticateWithRoleAsync(
            AuthenticateIdentityDTO dto, string? requiredRole)
        {
            var (user, isSuccess) = await _userIdentityService.ValidateUserCredentialsAsync(dto.UserName, dto.Password);
            if (!isSuccess || user == null)
            {
                _logger.LogWarning("Failed login attempt for {UserName}", dto.UserName);
                throw new FailedAuthenticationException("Invalid user credentials.");
            }

            if (requiredRole != null && user.Role != requiredRole)
            {
                _logger.LogWarning("User {UserId} lacks role {Role} for this login", user.Id, requiredRole);
                throw new ForbiddenException("User is not a system admin.");
            }

            var accessToken = _tokenService.GenerateAccessToken(user, user.Role!);
            var refreshToken = _tokenService.GenerateRefreshToken();
            await _tokenService.SaveRefreshTokenAsync(user.Id, refreshToken);

            _logger.LogInformation("User {UserId} authenticated; access token issued", user.Id);
            return (user, accessToken, refreshToken);
        }

        public async Task<(IdentityUserDTO user, string newAccessToken, string newRefreshToken)> RefreshUserSession(string oldRefreshToken)
        {
            var refreshToken = await _tokenService.GetAndValidateRefreshToken(oldRefreshToken);
            if (refreshToken == null || !refreshToken.IsActive)
            {
                _logger.LogWarning("Refresh rejected: invalid or expired refresh token");
                throw new UnauthorizedException("Invalid or expired refresh token.");
            }

            var applicationUser = refreshToken.IdentityUser
                ?? throw new UnauthorizedException("No user tied to this refresh token.");

            await _tokenService.RevokeRefreshToken(refreshToken);              // rotate: single-use
            var identityUser = _mapper.Map<IdentityUserDTO>(applicationUser);
            
            identityUser.Role = await _userIdentityService.GetUserRole(identityUser.Id);
            var newAccessToken = _tokenService.GenerateAccessToken(identityUser, identityUser.Role);
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
