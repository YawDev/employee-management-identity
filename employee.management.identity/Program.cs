using System.Text;
using employee.management.identity.core.Business;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;

// using employee.management.identity.infrastructure;
using employee.management.identity.Mapping;
using employee.management.identity.Middleware;
using employee.management.identity.models.Constants;
using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Add services to the DI container.
#region
var builder = WebApplication.CreateBuilder(args);

// Console logging: human-readable in Development, structured JSON once hosted. JSON is what log
// aggregators (CloudWatch, journald/Loki, Azure Log Analytics, etc.) can filter by CorrelationId.
// IncludeScopes = true so every line carries the request's correlation id.
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddSimpleConsole(o => o.IncludeScopes = true);
else
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddControllers(); // For controller-based APIs
builder.Services.AddEndpointsApiExplorer(); // Enables API explorer for tools like Swagger/OpenAPI
builder.Services.AddSwaggerGen(); // For generating OpenAPI documentation

// Lightweight HTTP request logging. Only method/path/status/duration — never headers or
// bodies, so bearer tokens, cookies, and credentials are never written to the log.
builder.Services.AddHttpLogging(o =>
    o.LoggingFields = HttpLoggingFields.RequestMethod
                    | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode
                    | HttpLoggingFields.Duration);

// Configure DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EmployeeManagementDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure ASP.NET Core Identity with Guid keys
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<EmployeeManagementDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]?? throw new InvalidOperationException("JWT Key not configured")))
        };
    });

// Configure Authorization policies to enforce role-based access control for different user roles in the application.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SysAdmin", p => p.RequireRole(RoleConstants.SystemAdmin))
    .AddPolicy("CompanyAdmin", p => p.RequireRole(RoleConstants.Company))
    .AddPolicy("ReportUser", p => p.RequireRole(RoleConstants.Manager, RoleConstants.Employee));


// Register application services for dependency injection
builder.Services.AddScoped<IUserIdentityService, UserIdentityService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ITokenService, TokenService>();
// Register Repositories for dependency injection
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();


// Register AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MapperProfile>();
});
#endregion


// Configure the HTTP request pipeline.
#region 
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>(); // Tags every request + its logs with a correlation id
app.UseHttpLogging(); // Concise per-request log (method, path, status, duration)

app.UseMiddleware<ExceptionHandlingMiddleware>(); // Centralized Exception handling

app.UseHttpsRedirection(); // Redirects HTTP requests to HTTPS

app.UseAuthentication(); // Enables authentication middleware (must come before Authorization)
app.UseAuthorization(); // Enables authorization middleware

app.MapControllers(); // Maps controller routes for controller-based APIs


app.Run();
#endregion