using AutoMapper;
using employee.management.identity.infrastructure;
using employee.management.identity.models.DatabaseModels;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Employee.Management.Identity.Infrastructure.Tests;

public class UserRepositoryTests
{
    private static UserRepository Repo(EmployeeManagementDbContext ctx) => new(ctx, Mock.Of<IMapper>());

    [Fact]
    public async Task ExistsAsync_ReturnsTrue_WhenUserExists()
    {
        using var ctx = TestDb.New();
        var id = Guid.NewGuid();
        ctx.ApplicationUsers.Add(new ApplicationUser { Id = id, UserName = "jdoe" });
        await ctx.SaveChangesAsync();

        Assert.True(await Repo(ctx).ExistsAsync(id));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalse_WhenUserMissing()
    {
        using var ctx = TestDb.New();
        Assert.False(await Repo(ctx).ExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByUserNameAsync_ReturnsUser_WhenExists()
    {
        using var ctx = TestDb.New();
        ctx.ApplicationUsers.Add(new ApplicationUser { Id = Guid.NewGuid(), UserName = "jdoe" });
        await ctx.SaveChangesAsync();

        var user = await Repo(ctx).GetByUserNameAsync("jdoe");

        Assert.NotNull(user);
        Assert.Equal("jdoe", user!.UserName);
    }

    [Fact]
    public async Task EditUserRoleAsync_UpdatesDomainUserRole()
    {
        using var ctx = TestDb.New();
        var identityId = Guid.NewGuid();
        ctx.DomainUsers.Add(TestDb.DomainUser(identityId, role: "default-user"));
        await ctx.SaveChangesAsync();

        var affected = await Repo(ctx).EditUserRoleAsync(identityId, "manager");

        Assert.Equal(1, affected);
        var updated = await ctx.DomainUsers.FirstAsync(d => d.IdentityUserId == identityId);
        Assert.Equal("manager", updated.Role);
    }

    [Fact]
    public async Task DeleteAsync_RemovesIdentityDomainUserAndRefreshTokens()
    {
        using var ctx = TestDb.New();
        var identityId = Guid.NewGuid();
        ctx.ApplicationUsers.Add(new ApplicationUser { Id = identityId, UserName = "jdoe" });
        ctx.DomainUsers.Add(TestDb.DomainUser(identityId));
        ctx.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            IdentityUserId = identityId,
            Token = "tok",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var deleted = await Repo(ctx).DeleteAsync(identityId);

        Assert.True(deleted);
        Assert.False(await ctx.ApplicationUsers.AnyAsync(u => u.Id == identityId));
        Assert.False(await ctx.DomainUsers.AnyAsync(d => d.IdentityUserId == identityId));
        Assert.False(await ctx.RefreshTokens.AnyAsync(r => r.IdentityUserId == identityId));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenUserMissing()
    {
        using var ctx = TestDb.New();
        Assert.False(await Repo(ctx).DeleteAsync(Guid.NewGuid()));
    }
}
