using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.core.Business
{
    public interface ITokenService
    {
        //TODO: Define JWT token generation methods here
        public string GenerateAccessToken(ApplicationUser user);
        public string GenerateRefreshToken();
    }
}