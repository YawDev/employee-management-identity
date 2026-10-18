using AutoMapper;
using employee.management.identity.core.Business;
using employee.management.identity.core.Exceptions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;            // RefreshToken
using employee.management.identity.models.Constants;
using employee.management.identity.models.DatabaseModels;     // ApplicationUser
using employee.management.identity.models.Dtos;
using Microsoft.Extensions.Logging;
using Moq;

namespace Employee.Management.Identity.Core.Tests;

public class AuthenticationServiceTests
{
    private readonly Mock<IUserIdentityService> _userService = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IMapper> _mapper = new();

    private AuthenticationService Service() =>
        new(_userService.Object, _tokenService.Object, Mock.Of<ILogger<AuthenticationService>>(), _mapper.Object);

    private static AuthenticateIdentityDTO Login(string userName = "jdoe") => new() { UserName = userName, Password = "pw" };

    private void SetupValidation(IdentityUserDTO? user, bool ok) =>
        _userService.Setup(s => s.ValidateUserCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((user, ok));

    private void SetupTokens(IdentityUserDTO user, string role)
    {
        _tokenService.Setup(t => t.GenerateAccessToken(user, role)).Returns("access");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");
        _tokenService.Setup(t => t.SaveRefreshTokenAsync(user.Id, "refresh")).ReturnsAsync(1);
    }

    [Fact]
    public async Task AuthenticateUser_Throws_WhenCredentialsInvalid()
    {
        SetupValidation(null, false);

        await Assert.ThrowsAsync<FailedAuthenticationException>(() => Service().AuthenticateUser(Login()));
    }

    [Fact]
    public async Task AuthenticateUser_ReturnsTokens_OnSuccess()
    {
        var user = new IdentityUserDTO { Id = Guid.NewGuid(), UserName = "jdoe", Role = "manager" };
        SetupValidation(user, true);
        SetupTokens(user, "manager");

        var (resultUser, access, refresh) = await Service().AuthenticateUser(Login());

        Assert.Equal(user, resultUser);
        Assert.Equal("access", access);
        Assert.Equal("refresh", refresh);
        // Role comes from the credential lookup; no second query.
        _userService.Verify(s => s.GetUserRole(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateSystemAdminUser_Throws_WhenCredentialsInvalid()
    {
        SetupValidation(null, false);

        await Assert.ThrowsAsync<FailedAuthenticationException>(() => Service().AuthenticateSystemAdminUser(Login()));
    }

    [Fact]
    public async Task AuthenticateSystemAdminUser_ThrowsForbidden_WhenNotSystemAdmin()
    {
        var user = new IdentityUserDTO { Id = Guid.NewGuid(), UserName = "jdoe", Role = "manager" };
        SetupValidation(user, true);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service().AuthenticateSystemAdminUser(Login()));

        _tokenService.Verify(t => t.SaveRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateSystemAdminUser_ReturnsTokens_ForSystemAdmin()
    {
        var user = new IdentityUserDTO { Id = Guid.NewGuid(), UserName = "root", Role = RoleConstants.SystemAdmin };
        SetupValidation(user, true);
        SetupTokens(user, RoleConstants.SystemAdmin);

        var (resultUser, access, refresh) = await Service().AuthenticateSystemAdminUser(Login("root"));

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

    [Fact]
    public async Task RefreshUserSession_RotatesToken_AndReturnsMappedUser()
    {
        var appUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = "jdoe" };
        var stored = new RefreshToken { IdentityUser = appUser, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var dto = new IdentityUserDTO { Id = appUser.Id, UserName = "jdoe" };
        _tokenService.Setup(t => t.GetAndValidateRefreshToken("old")).ReturnsAsync(stored);
        _mapper.Setup(m => m.Map<IdentityUserDTO>(appUser)).Returns(dto);
        _userService.Setup(s => s.GetUserRole(appUser.Id)).ReturnsAsync("manager");
        SetupTokens(dto, "manager");

        var (user, access, refresh) = await Service().RefreshUserSession("old");

        Assert.Equal("manager", user.Role);
        Assert.Equal("access", access);
        Assert.Equal("refresh", refresh);
        _tokenService.Verify(t => t.RevokeRefreshToken(stored), Times.Once);
    }
}
