using employee.management.identity.core.Business;
using employee.management.identity.core.Exceptions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;            // Tenant, DomainUser
using employee.management.identity.models.Constants;
using employee.management.identity.models.DatabaseModels;     // ApplicationUser
using employee.management.identity.models.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace Employee.Management.Identity.Core.Tests;

public class UserIdentityServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ITenantRepository> _tenantRepo = new();
    private readonly Mock<IPasswordHasher<ApplicationUser>> _hasher = new();

    private UserIdentityService Service() =>
        new(_userRepo.Object, _tenantRepo.Object, _hasher.Object, Mock.Of<ILogger<UserIdentityService>>());

    private static CreateIdentityDTO Dto(int tenantId = 1) => new()
    {
        UserName = "jdoe",
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Password = "P@ssw0rd!",
        TenantId = tenantId
    };

    [Fact]
    public async Task CreateUserAndIdentity_Throws_WhenUserAlreadyExists()
    {
        _userRepo.Setup(r => r.GetByUserNameAsync("jdoe")).ReturnsAsync(new ApplicationUser { UserName = "jdoe" });

        await Assert.ThrowsAsync<BadRequestException>(() => Service().CreateUserAndIdentityAsync(Dto()));
    }

    [Fact]
    public async Task CreateUserAndIdentity_Throws_WhenTenantIdMissing()
    {
        _userRepo.Setup(r => r.GetByUserNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => Service().CreateUserAndIdentityAsync(Dto(tenantId: 0)));
    }

    [Fact]
    public async Task CreateUserAndIdentity_Throws_WhenTenantNotFound()
    {
        _userRepo.Setup(r => r.GetByUserNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _tenantRepo.Setup(r => r.GetTenantInfoAsync(1)).ReturnsAsync((Tenant?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => Service().CreateUserAndIdentityAsync(Dto()));
    }

    [Fact]
    public async Task CreateUserAndIdentity_CreatesDomainUser_OnValidInput()
    {
        _userRepo.Setup(r => r.GetByUserNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _tenantRepo.Setup(r => r.GetTenantInfoAsync(1)).ReturnsAsync(new Tenant { TenantId = 1, Name = "Default" });
        _hasher.Setup(h => h.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>())).Returns("hashed");
        _userRepo.Setup(r => r.CreateIdentityUserAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(1);

        var result = await Service().CreateUserAndIdentityAsync(Dto());

        Assert.Equal("jdoe", result.UserName);
        _userRepo.Verify(r => r.CreateAsync(
            It.Is<DomainUser>(d => d.TenantId == 1 && d.Role == RoleConstants.Default)), Times.Once);
    }

    [Fact]
    public async Task DeleteUser_Throws_WhenUserMissing()
    {
        _userRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<UserNotFoundException>(() => Service().DeleteUserAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteUser_ReturnsTrue_WhenDeleted()
    {
        var id = Guid.NewGuid();
        _userRepo.Setup(r => r.ExistsAsync(id)).ReturnsAsync(true);
        _userRepo.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);

        Assert.True(await Service().DeleteUserAsync(id));
    }

    [Fact]
    public async Task EditUserRole_Throws_WhenUserMissing()
    {
        _userRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<UserNotFoundException>(() => Service().EditUserRoleAsync(Guid.NewGuid(), "manager"));
    }

    [Fact]
    public async Task ValidateUserCredentials_ReturnsFalse_WhenUserMissing()
    {
        _userRepo.Setup(r => r.GetByUserNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var (user, ok) = await Service().ValidateUserCredentialsAsync("nobody", "pw");

        Assert.Null(user);
        Assert.False(ok);
    }
}
