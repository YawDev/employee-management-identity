using employee.management.identity.infrastructure;
using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.core.Business
{
    public interface ITokenService
    {
        //TODO: Define JWT token generation methods here
        public string GenerateAccessToken(ApplicationUser user, string role);
        public string GenerateRefreshToken();
        Task<int> SaveRefreshTokenAsync(Guid userId, string refreshTokenString);
        Task<RefreshToken?> GetAndValidateRefreshToken(string refreshTokenString);
        Task<bool> RevokeRefreshToken(RefreshToken refreshToken);
    }
}
