using employee.management.identity.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure
{
    public class RefreshTokenRepository(EmployeeManagementDbContext context) : IRefreshTokenRepository
    {
        private readonly EmployeeManagementDbContext _context = context;

        public async Task<int> CreateAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Add(refreshToken);
            return await _context.SaveChangesAsync();
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            // Include the user so RefreshUserSession can read it directly off the token.
            return await _context.RefreshTokens
                .Include(rt => rt.IdentityUser)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task<bool> RevokeAsync(RefreshToken refreshToken)
        {
            if (refreshToken == null || refreshToken.IsRevoked || refreshToken.IsUsed || refreshToken.IsExpired)
                return false;

            refreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(refreshToken);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
