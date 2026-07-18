using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;
using employee.management.identity.models.DatabaseModels;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace employee.management.identity.core.Business
{
    public class TokenService(IConfiguration configuration, IRefreshTokenRepository refreshTokenRepository) : ITokenService
    {
        private readonly string _secretKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        private readonly string _issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        private readonly string _audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
        private readonly int _accessTokenExpirationMinutes = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 30;
        private readonly int _refreshTokenExpirationDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

        public string GenerateAccessToken(ApplicationUser user, string role)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserName ?? "" ),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
                signingCredentials: creds );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        public async Task<int> SaveRefreshTokenAsync(Guid userId, string refreshToken)
        {
            var entity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                IdentityUserId = userId,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
            };
            var result = await _refreshTokenRepository.CreateAsync(entity);
            if (result <= 0) throw new Exception("Failed to save refresh token");
            return result;
        }

        public Task<RefreshToken?> GetAndValidateRefreshToken(string refreshToken)
            => _refreshTokenRepository.GetByTokenAsync(refreshToken);

        public Task<bool> RevokeRefreshToken(RefreshToken refreshToken)
            => _refreshTokenRepository.RevokeAsync(refreshToken);
    }
}
