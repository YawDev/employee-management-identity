using employee.management.identity.infrastructure;

namespace employee.management.identity.core.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<int> CreateAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<bool> RevokeAsync(RefreshToken token);
    }
}
