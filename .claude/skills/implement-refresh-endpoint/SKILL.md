---
name: implement-refresh-endpoint
description: >-
  Implement a rotating refresh-token endpoint (POST /api/auth/refresh) in the
  Employee Management identity service. Use when asked to add token refresh,
  refresh tokens, refresh-token rotation, "keep users logged in", silent re-auth,
  or to finish the stubbed GenerateRefreshToken/refresh flow in this .NET identity
  API. Reads the refresh_token HttpOnly cookie, validates + rotates it, and re-issues
  access_token + refresh_token cookies.
---

# Implement the refresh-token endpoint (EMT identity service)

This skill ports the refresh-token rotation flow from the Personal-Blog backend into the
`employee-management-identity` service, **tailored to this project's conventions**:
cookie + rotation transport, `employee.management.identity.*` namespaces, DB-first
scaffolding, and the already-present `RefreshToken` entity.

## What "refresh" does here

1. Client calls `POST /api/auth/refresh` (no body). Browser sends the `refresh_token` HttpOnly cookie automatically.
2. Endpoint looks up the token row, validates it is **active** (`!IsExpired && !IsRevoked && !IsUsed`).
3. The old token is **revoked** (rotation — a token is single-use).
4. A new access token + new refresh token are generated and persisted.
5. Both are re-set as HttpOnly cookies. Reusing a rotated/expired/revoked token → `401`.

## Current state in THIS repo (verify before editing — these were true at authoring time)

| Piece | Status | Action |
|-------|--------|--------|
| `RefreshToken` entity (`...models/DatabaseModels/RefreshToken.cs`) | **Exists** (scaffolded, has `IsExpired`/`IsActive` computed props + `IdentityUser` nav) | Reuse. Only fix namespace (see Gotcha 1). |
| `RefreshToken` DbContext mapping + `DbSet` | **Exists** (`ExcludeFromMigrations`, table `"RefreshToken"`, PascalCase cols, indexes on `Token` and `IdentityUserId`) | No migration needed — table is already in the DB. |
| `IRefreshTokenRepository` / `RefreshTokenRepository` | **Missing** | Create (Step 1). |
| `ITokenService` refresh methods + `TokenService.GenerateRefreshToken()` | **Stubbed** (`throw new NotImplementedException()`, no repo injected, hardcoded 30-min access) | Implement (Step 2). |
| `IAuthenticationService.RefreshUserSession` + `AuthenticationService` | **Missing**; `AuthenticateUser` returns `(ApplicationUser, string)` | Add refresh, extend login (Step 3). |
| Controller refresh endpoint + cookie-setting login | **Missing**; `Login` returns `Ok(new { User, Token })` in the body | Add endpoint, switch login to cookies (Step 4). |
| DI registration for the repo | **Missing** | Register (Step 5). |
| `Jwt:AccessTokenExpirationMinutes` / `Jwt:RefreshTokenExpirationDays` config | Likely **missing** | Add (Step 5). |

> Always re-read these files first — the repo may have moved on since this was written.

---

## Step 1 — Refresh-token repository (Infrastructure)

Create `employee.management.identity.core/Interfaces/IRefreshTokenRepository.cs`
(match `IUserRepository`'s namespace `...core.Interfaces`, **not** the `.core.Business`
quirk that `ITokenService` uses):

```csharp
using employee.management.identity.models.DatabaseModels; // after Gotcha 1 fix

namespace employee.management.identity.core.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<int> CreateAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<bool> RevokeAsync(RefreshToken token);
    }
}
```

Create `employee.management.identity.infrastructure/RefreshTokenRepository.cs`
(mirror `UserRepository`'s primary-constructor style, but no `IMapper` is needed):

```csharp
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.DatabaseModels; // after Gotcha 1 fix
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure
{
    public class RefreshTokenRepository(EmployeeManagementDbContext context) : IRefreshTokenRepository
    {
        private readonly EmployeeManagementDbContext _context = context;

        public async Task<int> CreateAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Add(refreshToken);
            return await _context.SaveChangesAsync();
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            // Include the user so RefreshUserSession can read it directly — see Gotcha 2.
            return await _context.RefreshTokens
                .Include(rt => rt.IdentityUser)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task<bool> RevokeAsync(RefreshToken refreshToken)
        {
            if (refreshToken == null || refreshToken.IsRevoked || refreshToken.IsUsed || refreshToken.IsExpired)
                return false;

            refreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(refreshToken);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
```

## Step 2 — Token service

Extend `employee.management.identity.core/Interfaces/ITokenService.cs` (keep its existing
`...core.Business` namespace):

```csharp
public Task<int> SaveRefreshTokenAsync(Guid userId, string refreshTokenString);
Task<RefreshToken?> GetAndValidateRefreshToken(string refreshTokenString);
Task<bool> RevokeRefreshToken(RefreshToken refreshToken);
```

Update `TokenService` to inject the repo and drop the hardcoded 30-minute expiry:

```csharp
public class TokenService(IConfiguration configuration, IRefreshTokenRepository refreshTokenRepository) : ITokenService
{
    private readonly string _secretKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
    private readonly string _issuer   = configuration["Jwt:Issuer"]   ?? throw new InvalidOperationException("JWT Issuer not configured");
    private readonly string _audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
    private readonly int _accessTokenExpirationMinutes = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 30;
    private readonly int _refreshTokenExpirationDays    = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

    // GenerateAccessToken: unchanged, but swap the hardcoded `AddMinutes(30)` for `_accessTokenExpirationMinutes`.
    // EMT is multi-tenant — consider adding a tenant claim here (see Gotcha 4).

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create(); // System.Security.Cryptography
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<int> SaveRefreshTokenAsync(Guid userId, string refreshToken)
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            IdentityUserId = userId,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
        };
        var result = await _refreshTokenRepository.CreateAsync(entity);
        if (result <= 0) throw new Exception("Failed to save refresh token");
        return result;
    }

    public Task<RefreshToken?> GetAndValidateRefreshToken(string refreshToken)
        => _refreshTokenRepository.GetByTokenAsync(refreshToken);

    public Task<bool> RevokeRefreshToken(RefreshToken refreshToken)
        => _refreshTokenRepository.RevokeAsync(refreshToken);
}
```

## Step 3 — Authentication service

In `IAuthenticationService` add:

```csharp
Task<(ApplicationUser user, string newAccessToken, string newRefreshToken)> RefreshUserSession(string oldRefreshToken);
```

In `AuthenticationService`, **tailored to EMT**: the `RefreshToken.IdentityUser` navigation is
eager-loaded by the repo, so use it directly — EMT's `IUserIdentityService` has **no**
`GetApplicationUserAsync`, so don't re-fetch the user the way Personal-Blog does.

```csharp
public async Task<(ApplicationUser user, string newAccessToken, string newRefreshToken)> RefreshUserSession(string oldRefreshToken)
{
    var refreshToken = await _tokenService.GetAndValidateRefreshToken(oldRefreshToken);
    if (refreshToken == null || !refreshToken.IsActive)
        throw new UnauthorizedException("Invalid or expired refresh token.");

    var identityUser = refreshToken.IdentityUser
        ?? throw new UnauthorizedException("No user tied to this refresh token.");

    await _tokenService.RevokeRefreshToken(refreshToken);              // rotate: single-use

    var newAccessToken  = _tokenService.GenerateAccessToken(identityUser);
    var newRefreshToken = _tokenService.GenerateRefreshToken();
    await _tokenService.SaveRefreshTokenAsync(identityUser.Id, newRefreshToken);

    return (identityUser, newAccessToken, newRefreshToken);
}
```

Also extend `AuthenticateUser` to mint + persist a refresh token at login (the refresh flow
needs a token to have been issued). Change its return tuple to
`(ApplicationUser?, string? accessToken, string? refreshToken)`, update the interface, and
update the controller call site (Step 4):

```csharp
var accessToken  = _tokenService.GenerateAccessToken(authenticatedUser);
var refreshToken = _tokenService.GenerateRefreshToken();
await _tokenService.SaveRefreshTokenAsync(authenticatedUser.Id, refreshToken);
return (authenticatedUser, accessToken, refreshToken);
```

## Step 4 — Controller (`AuthenticationController`)

Add `using Microsoft.AspNetCore.Authorization;`. Add `IConfiguration` to the primary
constructor for cookie lifetimes, and define one cookie-options helper to avoid repetition:

```csharp
public class AuthenticationController(
    IConfiguration configuration,
    IAuthenticationService authenticationService,
    IMapper mapper,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    private readonly int _accessTokenExpirationMinutes = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 30;
    private readonly int _refreshTokenExpirationDays    = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 7;
    // ...existing fields...

    private static CookieOptions Cookie(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None, // cross-site FE→API; requires HTTPS (Gotcha 3)
        Expires = expires
    };
```

Update **Login** to set cookies instead of returning the token in the body, and mark it `[AllowAnonymous]`:

```csharp
[AllowAnonymous]
[HttpPost("auth/login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var identityDTO = _mapper.Map<AuthenticateIdentityDTO>(request);
    var (user, accessToken, refreshToken) = await _authenticationService.AuthenticateUser(identityDTO);
    if (user == null) return Unauthorized("Failed to authenticate credentials.");

    Response.Cookies.Append("access_token",  accessToken,  Cookie(DateTimeOffset.UtcNow.AddMinutes(_accessTokenExpirationMinutes)));
    Response.Cookies.Append("refresh_token", refreshToken, Cookie(DateTimeOffset.UtcNow.AddDays(_refreshTokenExpirationDays)));

    await _signInManager.SignInAsync(user, isPersistent: false);
    return Ok(new { User = _mapper.Map<IdentityUserDTO>(user) });
}
```

Add the **refresh** endpoint:

```csharp
[AllowAnonymous]
[HttpPost("auth/refresh")]
public async Task<IActionResult> RefreshToken()
{
    if (!Request.Cookies.TryGetValue("refresh_token", out var existing) || string.IsNullOrWhiteSpace(existing))
        return Unauthorized("Missing refresh token.");

    var (user, newAccessToken, newRefreshToken) = await _authenticationService.RefreshUserSession(existing);

    Response.Cookies.Append("access_token",  newAccessToken,  Cookie(DateTimeOffset.UtcNow.AddMinutes(_accessTokenExpirationMinutes)));
    Response.Cookies.Append("refresh_token", newRefreshToken, Cookie(DateTimeOffset.UtcNow.AddDays(_refreshTokenExpirationDays)));

    return Ok(new { User = _mapper.Map<IdentityUserDTO>(user) });
}
```

`UnauthorizedException` should already map to `401` via `ExceptionHandlingMiddleware` —
confirm that mapping exists; if not, catch it in the endpoint.

## Step 5 — Wiring & config

`Program.cs` DI (near the other `AddScoped` lines):

```csharp
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
```

`appsettings.json` → `Jwt` section:

```json
"AccessTokenExpirationMinutes": 30,
"RefreshTokenExpirationDays": 7
```

No EF migration: the `RefreshToken` table is already in the DB and mapped
`ExcludeFromMigrations` (DB-first). If the table is somehow missing, hand-write the DDL and
scaffold per this repo's DB-first workflow — do **not** add a code-first migration.

---

## EMT-specific gotchas (these differ from Personal-Blog)

1. **`RefreshToken` namespace is backwards.** The entity lives in the *models* project folder
   but is declared `namespace employee.management.identity.infrastructure;`. Rename it to
   `employee.management.identity.models.DatabaseModels` (matching `ApplicationUser` and its
   folder) so `core` references the model cleanly. If you'd rather not touch it now, the
   interface/repo can `using employee.management.identity.infrastructure;` instead — it still
   compiles (same models assembly), just reads oddly.
2. **Use the `IdentityUser` navigation, don't re-fetch.** `RefreshToken.IdentityUser` is a
   non-nullable nav already eager-loaded by `GetByTokenAsync`. EMT lacks Personal-Blog's
   `GetApplicationUserAsync`, so read the user off the token — don't add a fetch method.
3. **`SameSite=None` + `Secure` requires HTTPS.** Locally the app already calls
   `UseHttpsRedirection()`. The browser will silently drop these cookies over plain HTTP.
4. **Multi-tenancy (optional).** EMT has `Tenant`/`Organization`; Personal-Blog doesn't. If
   downstream needs tenant context, add a tenant claim in `GenerateAccessToken` (e.g. from
   `DomainUser.Tenant`). Out of scope for refresh itself, but the rotation re-mints the access
   token, so any claim you add must be reproducible at refresh time from the user alone.
5. **CORS must allow credentials.** For the browser to send/receive these cookies cross-origin,
   the CORS policy needs `.AllowCredentials()` with explicit origins (not `AllowAnyOrigin`),
   and the frontend fetch/axios must use `credentials: 'include'` / `withCredentials: true`.
6. **Interface namespace inconsistency.** `ITokenService`/`IAuthenticationService` sit in
   `Interfaces/` but declare `...core.Business`; `IUserRepository` uses `...core.Interfaces`.
   Put `IRefreshTokenRepository` with `IUserRepository` (`...core.Interfaces`) and add the
   matching `using` where consumed.

## Verify when done

- Build: `dotnet build` from `employee-management-identity/`.
- `POST /api/auth/login` → response sets `access_token` + `refresh_token` cookies; a row lands in `RefreshToken`.
- `POST /api/auth/refresh` (with cookies) → `200`, new cookies, the previous token row has `IsRevoked = true`, a new row exists.
- Replay the **old** refresh token → `401`.
- Refresh with no cookie → `401 "Missing refresh token."`
