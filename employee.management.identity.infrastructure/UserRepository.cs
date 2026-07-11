
using AutoMapper;
using AutoMapper.QueryableExtensions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels;
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

        public async Task<bool> DeleteAsync(Guid userId)
        {
            var user = _context.ApplicationUsers.Where(u => u.Id == userId);
            _context.Remove(user);
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
    }
}