using employee.management.identity.infrastructure;
using employee.management.identity.models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace Employee.Management.Identity.Infrastructure.Tests;

public class RefreshTokenRepositoryTests
{
    private static RefreshToken Token(string token) => new()
    {
        Id = Guid.NewGuid(),
        IdentityUserId = Guid.NewGuid(),
        Token = token,
        ExpiresAt = DateTime.UtcNow.AddDays(1),
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task CreateAsync_PersistsToken()
    {
        using var ctx = TestDb.New();

        var affected = await new RefreshTokenRepository(ctx).CreateAsync(Token("abc"));

        Assert.Equal(1, affected);
        Assert.True(await ctx.RefreshTokens.AnyAsync(t => t.Token == "abc"));
    }

    [Fact]
    public async Task GetByTokenAsync_ReturnsToken_WhenExists()
    {
        using var ctx = TestDb.New();
        var userId = Guid.NewGuid();
        ctx.ApplicationUsers.Add(new ApplicationUser { Id = userId, UserName = "jdoe" });
        var token = Token("abc");
        token.IdentityUserId = userId; // GetByTokenAsync Includes the owning user (required nav)
        ctx.RefreshTokens.Add(token);
        await ctx.SaveChangesAsync();

        var found = await new RefreshTokenRepository(ctx).GetByTokenAsync("abc");

        Assert.NotNull(found);
        Assert.Equal("abc", found!.Token);
    }

    [Fact]
    public async Task RevokeAsync_MarksTokenRevoked()
    {
        using var ctx = TestDb.New();
        var entity = Token("abc");
        ctx.RefreshTokens.Add(entity);
        await ctx.SaveChangesAsync();

        var revoked = await new RefreshTokenRepository(ctx).RevokeAsync(entity);

        Assert.True(revoked);
        Assert.True(entity.IsRevoked);
    }

    [Fact]
    public async Task RevokeAsync_ReturnsFalse_WhenAlreadyRevoked()
    {
        using var ctx = TestDb.New();
        var entity = Token("abc");
        entity.IsRevoked = true;
        ctx.RefreshTokens.Add(entity);
        await ctx.SaveChangesAsync();

        Assert.False(await new RefreshTokenRepository(ctx).RevokeAsync(entity));
    }
}
