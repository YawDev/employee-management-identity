using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Identity;

namespace employee.management.identity.core.Business
{
    public class UserIdentityService(IUserRepository userRepository, IPasswordHasher<ApplicationUser> passwordHasher) : IUserIdentityService
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher = passwordHasher;

        public async Task<ApplicationUser> CreateUserAndIdentityAsync(CreateIdentityDTO user)
        {

            var existingUser = await _userRepository.GetByUserNameAsync(user.UserName);

            if (existingUser != null) throw new Exception("User already exists");

            var newIdentity = new ApplicationUser
            {
                UserName = user.UserName,
                Email = user.Email,
                NormalizedEmail = user.Email.ToUpper(),
                NormalizedUserName = user.UserName.ToUpper()        
            };

            newIdentity.PasswordHash = _passwordHasher.HashPassword(newIdentity, user.Password);

            var result = await _userRepository.CreateIdentityUserAsync(newIdentity);
            if(result > 0)
            {
                var newUser = new User
                {
                    IdentityUserId = newIdentity.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    IsActive = true,
                    Role = "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userRepository.CreateAsync(newUser);
            }

            return newIdentity;
        }

        public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }
        public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }
        public async Task<ApplicationUser?> GetUserByUserNameAsync(string userName)
        {
            return await _userRepository.GetByUserNameAsync(userName);
        }
        public async Task<bool> ValidateUserCredentialsAsync(string userName, string passwordHash)
        {
            return await _userRepository.ValidateCredentialsAsync(userName, passwordHash);
        }
    }
}
