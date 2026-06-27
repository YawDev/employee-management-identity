using employee.management.identity.core.Exceptions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.Constants;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Identity;

namespace employee.management.identity.core.Business
{
    public class UserIdentityService(IUserRepository userRepository, IPasswordHasher<ApplicationUser> passwordHasher) : IUserIdentityService
    {
        // private readonly IUserRepository _userRepository = userRepository;
        // private readonly IPasswordHasher<ApplicationUser> _passwordHasher = passwordHasher;

        // public async Task<ApplicationUser> CreateUserAndIdentityAsync(CreateIdentityDTO user)
        // {
        //     var existingUser = await _userRepository.GetByUserNameAsync(user.UserName);

        //     if (existingUser != null) throw new BadRequestException("User already exists");

        //     var newIdentity = new ApplicationUser
        //     {
        //         UserName = user.UserName,
        //         Email = user.Email,
        //         NormalizedEmail = user.Email.ToUpper(),
        //         NormalizedUserName = user.UserName.ToUpper(),
        //         SecurityStamp = Guid.NewGuid().ToString() // Generate security stamp
        //     };

        //     newIdentity.PasswordHash = _passwordHasher.HashPassword(newIdentity, user.Password);

        //     var result = await _userRepository.CreateIdentityUserAsync(newIdentity);
        //     if(result > 0)
        //     {
        //         var newUser = new User
        //         {
        //             UserId = Guid.NewGuid(),
        //             IdentityUserId = newIdentity.Id,
        //             FirstName = user.FirstName,
        //             LastName = user.LastName,
        //             Email = user.Email,
        //             Role = RoleConstants.Default,
        //             IsActive = true,
        //             TenantId = user.TenantId, //TODO: determine how to set tenant, for now using default
        //             CreatedAt = DateTime.UtcNow,
        //             UpdatedAt = DateTime.UtcNow
        //         };
        //         await _userRepository.CreateAsync(newUser);
        //     }

        //     return newIdentity;
        // }

        // public async Task<IdentityUserDTO?> GetIdentityUserInfo(Guid userId)
        // {
        //     return await _userRepository.GetIdentityUserInfoAsync(userId);
        // }

        // public async Task<IdentityUserDTO?> GetUserByEmailAsync(string email)
        // {
        //     return await _userRepository.GetByEmailAsync(email);
        // }

        // public async Task<UserDTO?> GetUserByIdAsync(Guid identityUserId)
        // {
        //     var user = await _userRepository.GetByIdAsync(identityUserId) ?? throw new BadRequestException("User not found");
        //     return user;
        // }

        // public async Task<ApplicationUser?> GetUserByUserNameAsync(string userName)
        // {
        //     return await _userRepository.GetByUserNameAsync(userName);
        // }

        // public async Task<(ApplicationUser?,bool)> ValidateUserCredentialsAsync(string userName, string password)
        // {
        //     var user = await _userRepository.GetByUserNameAsync(userName);

        //     if (user == null) return (null, false);

        //     // Verify the password using PasswordHasher
        //     var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        //     return (user, result == PasswordVerificationResult.Success);
        // }
        public Task<ApplicationUser> CreateUserAndIdentityAsync(CreateIdentityDTO user)
        {
            throw new NotImplementedException();
        }

        public Task<IdentityUserDTO?> GetIdentityUserInfo(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<UserDTO?> GetUserByIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<(ApplicationUser, bool)> ValidateUserCredentialsAsync(string userName, string password)
        {
            throw new NotImplementedException();
        }
    }
}
