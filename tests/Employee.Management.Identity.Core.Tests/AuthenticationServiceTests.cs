using employee.management.identity.core.Business;
using employee.management.identity.core.Exceptions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;            // RefreshToken
using employee.management.identity.models.DatabaseModels;     // ApplicationUser
using employee.management.identity.models.Dtos;
using Microsoft.Extensions.Logging;
using Moq;

namespace Employee.Management.Identity.Core.Tests;

public class AuthenticationServiceTests
{
    private readonly Mock<IUserIdentityService> _userService = new();
    private readonly Mock<ITokenService> _tokenService = new();

    private AuthenticationService Service() =>
        new(_userService.Object, _tokenService.Object, Mock.Of<ILogger<AuthenticationService>>());

    [Fact]
    public async Task AuthenticateUser_Throws_WhenCredentialsInvalid()
    {
        _userService.Setup(s => s.ValidateUserCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((null!, false));

        await Assert.ThrowsAsync<FailedAuthenticationException>(
            () => Service().AuthenticateUser(new AuthenticateIdentityDTO { UserName = "x", Password = "y" }));
    }

    [Fact]
    public async Task AuthenticateUser_ReturnsTokens_OnSuccess()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "jdoe" };
        _userService.Setup(s => s.ValidateUserCredentialsAsync("jdoe", "pw")).ReturnsAsync((user, true));
        _userService.Setup(s => s.GetUserRole(user.Id)).ReturnsAsync("manager");
        _tokenService.Setup(t => t.GenerateAccessToken(user, "manager")).Returns("access");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");
        _tokenService.Setup(t => t.SaveRefreshTokenAsync(user.Id, "refresh")).ReturnsAsync(1);

        var (resultUser, access, refresh) = await Service().AuthenticateUser(
            new AuthenticateIdentityDTO { UserName = "jdoe", Password = "pw" });

        Assert.Equal(user, resultUser);
        Assert.Equal("access", access);
        Assert.Equal("refresh", refresh);
    }

    [Fact]
    public async Task RefreshUserSession_Throws_WhenTokenInvalid()
    {
        _tokenService.Setup(t => t.GetAndValidateRefreshToken(It.IsAny<string>()))
            .ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() => Service().RefreshUserSession("bad"));
    }
}
