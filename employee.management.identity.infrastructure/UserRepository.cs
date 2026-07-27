
using AutoMapper;
using AutoMapper.QueryableExtensions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels;
using employee.management.identity.models.DatabaseModels.QueryResults;
using employee.management.identity.models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure
{
    public class UserRepository(EmployeeManagementDbContext context, IMapper mapper) : IUserRepository
    {
        private readonly EmployeeManagementDbContext _context = context;
        private readonly IMapper _mapper = mapper;

        public async Task<int> CreateAsync(DomainUser user)
        {
            _context.DomainUsers.Add(user);
            return await _context.SaveChangesAsync();
        }

        public async Task<int> CreateIdentityUserAsync(ApplicationUser user)
        {
            _context.ApplicationUsers.Add(user);
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Guid identityUserId)
        {
            var identityUser = await _context.ApplicationUsers.FindAsync(identityUserId);
            if (identityUser is null) return false;

            // DomainUser and RefreshTokens are keyed off IdentityUserId with no cascade
            // configured, so remove them explicitly before deleting the identity row.
            var domainUser = await _context.DomainUsers
                .FirstOrDefaultAsync(d => d.IdentityUserId == identityUserId);
            if (domainUser is not null) _context.DomainUsers.Remove(domainUser);

            var refreshTokens = await _context.RefreshTokens
                .Where(rt => rt.IdentityUserId == identityUserId)
                .ToListAsync();
            if (refreshTokens.Count > 0) _context.RefreshTokens.RemoveRange(refreshTokens);

            _context.ApplicationUsers.Remove(identityUser);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ExistsAsync(Guid userId)
        {
            var existingUser = await _context.ApplicationUsers.FindAsync(userId);
            return existingUser != null;
        }

        public async Task<IdentityUserDTO?> GetByEmailAsync(string email)
        {
            var existingUser = await _context.ApplicationUsers
                .Where(u => u.Email == email)
                .ProjectTo<IdentityUserDTO>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();
            return existingUser;
        }

        public async Task<UserDTO?> GetByIdAsync(Guid identityUserId)
        {
            var existingUser = await _context.DomainUsers
                .Include(x => x.Tenant)
                .ProjectTo<UserDTO>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(u => u.IdentityUserId == identityUserId);
            return existingUser;
        }

        public async Task<ApplicationUser?> GetByUserNameAsync(string userName)
        {
            var existingUser = await _context.ApplicationUsers
                .FirstOrDefaultAsync(u => u.UserName == userName);
            return existingUser;
        }

        public async Task<IdentityUserDTO?> GetIdentityUserInfoAsync(Guid id)
        {
            var existingUser = await _context.ApplicationUsers
                .ProjectTo<IdentityUserDTO>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(iu => iu.Id == id);
            return existingUser;
        }

        public async Task<string> GetUserRoleAsync(Guid identityUserId)
        {
            var existingUser = await _context.DomainUsers
                .Where(u => u.IdentityUserId == identityUserId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync();
            return existingUser ?? throw new Exception("Role not found");
        }

        public async Task<ApplicationUser> UpdateAsync(ApplicationUser user)
        {
            _context.ApplicationUsers.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> ValidateCredentialsAsync(string userName, string passwordHash)
        {
            var existingUser = await _context.ApplicationUsers.FirstOrDefaultAsync(u => u.UserName == userName && u.PasswordHash == passwordHash);
            return existingUser != null;
        }

        public async Task<int> EditUserRoleAsync(Guid identityUserId, string newRole)
        {
            var domainUser = await _context.DomainUsers
                .FirstOrDefaultAsync(u => u.IdentityUserId == identityUserId);

            if (domainUser is null) throw new Exception("User not found");

            domainUser.Role = newRole;
            return await _context.SaveChangesAsync();
        }

        public async Task<List<UserQueryResult>> GetAllUsersQueryJoinedAsync()
        {
            var users = await _context.DomainUsers.AsNoTracking()
                .Include(x => x.ApplicationUser)
                .Include(x => x.ReportingLine)
                .Include(x => x.Manager)
                .Include(x => x.Tenant).ThenInclude(t => t.DomainUsers)
                .Select(x => new UserQueryResult
                {
                    Id = x.ApplicationUser.Id,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    DomainUserId = x.DomainUserId,
                    UserName = x.ApplicationUser.UserName,
                    Email = x.ApplicationUser.Email,
                    EmailConfirmed = x.ApplicationUser.EmailConfirmed,
                    PhoneNumber = x.ApplicationUser.PhoneNumber,
                    PhoneNumberConfirmed = x.ApplicationUser.PhoneNumberConfirmed,
                    TwoFactorEnabled = x.ApplicationUser.TwoFactorEnabled,
                    LockoutEnd = x.ApplicationUser.LockoutEnd,
                    LockoutEnabled = x.ApplicationUser.LockoutEnabled,
                    AccessFailedCount = x.ApplicationUser.AccessFailedCount,
                    Role = x.Role,
                    IsActive = x.IsActive,
                    Manager = x.Manager,
                    ReportingLine = x.ReportingLine,
                    Tenant = x.Tenant
                })
                .ToListAsync();
            return users;
        }
        

        public async Task<UserQueryResult> GetUserQueryJoinedAsync(Guid identityUserId)
        {
            var user = await _context.DomainUsers.AsNoTracking()
                .Include(x => x.ApplicationUser)
                .Include(x => x.ReportingLine)
                .Include(x => x.Manager)
                .Include(x => x.Tenant)
                .Where(x => x.IdentityUserId == identityUserId)
                .Select(x => new UserQueryResult{
                    Id = x.ApplicationUser.Id,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    DomainUserId = x.DomainUserId,
                    UserName = x.ApplicationUser.UserName,
                    Email = x.ApplicationUser.Email,
                    EmailConfirmed = x.ApplicationUser.EmailConfirmed,
                    PhoneNumber = x.ApplicationUser.PhoneNumber,
                    PhoneNumberConfirmed = x.ApplicationUser.PhoneNumberConfirmed,
                    TwoFactorEnabled = x.ApplicationUser.TwoFactorEnabled,
                    LockoutEnd = x.ApplicationUser.LockoutEnd,
                    LockoutEnabled = x.ApplicationUser.LockoutEnabled,
                    AccessFailedCount = x.ApplicationUser.AccessFailedCount,
                    Role = x.Role,
                    IsActive = x.IsActive,
                    Manager = x.Manager,
                    ReportingLine = x.ReportingLine,
                    Tenant = x.Tenant
                })
                .FirstOrDefaultAsync();
            return user; 
        }
    }
}