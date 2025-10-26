
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;
        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CreateAsync(User user)
        {
            _context.Users.Add(user);
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

        public async Task<ApplicationUser?> GetByEmailAsync(string email)
        {
            var existingUser = await _context.ApplicationUsers.FindAsync(email);
            return existingUser;
        }

        public async Task<ApplicationUser?> GetByIdAsync(Guid userId)
        {
            var existingUser = await _context.ApplicationUsers.FindAsync(userId);
            return existingUser;
        }

        public async Task<ApplicationUser?> GetByUserNameAsync(string userName)
        {
            var existingUser = await _context.ApplicationUsers.FirstOrDefaultAsync(u => u.UserName == userName);
            return existingUser;
        }

        public async Task<ApplicationUser?> GetIdentityUserInfoAsync(Guid id)
        {
            var existingUser = await _context.ApplicationUsers.FindAsync(id);
            return existingUser;;
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