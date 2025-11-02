using employee.management.identity.core.Business;
using employee.management.identity.core.Interfaces;
using employee.management.identity.infrastructure;
using employee.management.identity.Mapping;
using employee.management.identity.Middleware;
using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Add services to the DI container.
#region
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(); // For controller-based APIs
builder.Services.AddEndpointsApiExplorer(); // Enables API explorer for tools like Swagger/OpenAPI
builder.Services.AddSwaggerGen(); // For generating OpenAPI documentation

// Configure DbContext with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("localhost"),
        b => b.MigrationsAssembly("employee.management.identity.infrastructure")
    )
);

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
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Register application services for dependency injection
builder.Services.AddScoped<IUserIdentityService, UserIdentityService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Register Repositories for dependency injection
builder.Services.AddScoped<IUserRepository, UserRepository>();


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

app.UseMiddleware<ExceptionHandlingMiddleware>(); // Centralized Exception handling

app.UseHttpsRedirection(); // Redirects HTTP requests to HTTPS

app.UseAuthentication(); // Enables authentication middleware (must come before Authorization)
app.UseAuthorization(); // Enables authorization middleware

app.MapControllers(); // Maps controller routes for controller-based APIs


app.Run();
#endregion