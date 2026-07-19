using employee.management.identity.core.Exceptions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;
using employee.management.identity.models.Constants;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace employee.management.identity.core.Business
{
    public class UserIdentityService(IUserRepository userRepository, ITenantRepository tenantRepository, IPasswordHasher<ApplicationUser> passwordHasher, ILogger<UserIdentityService> logger) : IUserIdentityService
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ITenantRepository _tenantRepository = tenantRepository;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher = passwordHasher;
        private readonly ILogger<UserIdentityService> _logger = logger;

        public async Task<ApplicationUser> CreateUserAndIdentityAsync(CreateIdentityDTO user)
        {
            var existingUser = await _userRepository.GetByUserNameAsync(user.UserName);

            if (existingUser != null) throw new BadRequestException("User already exists");

            // Enforce a valid tenant up front so we never persist an ApplicationUser
            // that can't be paired with a DomainUser (TenantId is a required FK).
            if (user.TenantId <= 0)
                throw new BadRequestException("TenantId is required");

            var tenant = await _tenantRepository.GetTenantInfoAsync(user.TenantId)
                ?? throw new BadRequestException("Tenant does not exist");

            var newIdentity = new ApplicationUser
            {
                UserName = user.UserName,
                Email = user.Email,
                NormalizedEmail = user.Email.ToUpper(),
                NormalizedUserName = user.UserName.ToUpper(),
                SecurityStamp = Guid.NewGuid().ToString() // Generate security stamp
            };

            newIdentity.PasswordHash = _passwordHasher.HashPassword(newIdentity, user.Password);

            var result = await _userRepository.CreateIdentityUserAsync(newIdentity);
            if(result > 0)
            {
                var newUser = new DomainUser
                {
                    DomainUserId = Guid.NewGuid(),
                    IdentityUserId = newIdentity.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = RoleConstants.Default,
                    IsActive = true,
                    TenantId = tenant.TenantId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userRepository.CreateAsync(newUser);
                _logger.LogInformation("Created user {IdentityUserId} on tenant {TenantId}", newIdentity.Id, tenant.TenantId);
            }

            return newIdentity;
        }

        public async Task<IdentityUserDTO?> GetIdentityUserInfo(Guid userId)
        {
            return await _userRepository.GetIdentityUserInfoAsync(userId);
        }

        public async Task<IdentityUserDTO?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task<UserDTO?> GetUserByIdAsync(Guid identityUserId)
        {
            var user = await _userRepository.GetByIdAsync(identityUserId) ?? throw new BadRequestException("User not found");
            return user;
        }

        public async Task<string> GetUserRole(Guid identityUserId)
        {
            var role = await _userRepository.GetUserRoleAsync(identityUserId) ?? throw new BadRequestException("Role not found");
            return role;
        }

        public async Task<bool> DeleteUserAsync(Guid identityUserId)
        {
            var exists = await _userRepository.ExistsAsync(identityUserId);
            if (!exists) throw new UserNotFoundException("User not found");

            var deleted = await _userRepository.DeleteAsync(identityUserId);
            _logger.LogInformation("Deleted user {IdentityUserId} (success: {Deleted})", identityUserId, deleted);
            return deleted;
        }

        public async Task<ApplicationUser?> GetUserByUserNameAsync(string userName)
        {
            return await _userRepository.GetByUserNameAsync(userName);
        }

        public async Task<(ApplicationUser?, bool)> ValidateUserCredentialsAsync(string userName, string password)
        {
            var user = await _userRepository.GetByUserNameAsync(userName);

            if (user == null) return (null, false);

            // Verify the password using PasswordHasher
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

            return (user, result == PasswordVerificationResult.Success);
        }

        public async Task<bool> EditUserRoleAsync(Guid identityUserId, string newRole)
        {
            var exists = await _userRepository.ExistsAsync(identityUserId);
            if (!exists) throw new UserNotFoundException("User not found");

            var updated = await _userRepository.EditUserRoleAsync(identityUserId, newRole) > 0;
            _logger.LogInformation("Changed role for user {IdentityUserId} to {NewRole}", identityUserId, newRole);
            return updated;
        }
        

    
    }
}
