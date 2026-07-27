using AutoMapper;
using employee.management.identity.infrastructure;
using Moq;

namespace Employee.Management.Identity.Infrastructure.Tests;

public class TenantRepositoryTests
{
    [Fact]
    public async Task GetTenantInfoAsync_ReturnsTenant_WhenExists()
    {
        using var ctx = TestDb.New();
        ctx.Tenants.Add(new Tenant { TenantId = 1, Name = "Default" });
        await ctx.SaveChangesAsync();

        var tenant = await new TenantRepository(ctx, Mock.Of<IMapper>()).GetTenantInfoAsync(1);

        Assert.NotNull(tenant);
        Assert.Equal("Default", tenant!.Name);
    }

    [Fact]
    public async Task GetTenantInfoAsync_ReturnsNull_WhenMissing()
    {
        using var ctx = TestDb.New();
        var tenant = await new TenantRepository(ctx, Mock.Of<IMapper>()).GetTenantInfoAsync(999);
        Assert.Null(tenant);
    }
}
