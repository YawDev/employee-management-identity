using employee.management.identity.infrastructure;
using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // Redirects HTTP requests to HTTPS

app.UseAuthentication(); // Enables authentication middleware (must come before Authorization)
app.UseAuthorization(); // Enables authorization middleware

app.MapControllers(); // Maps controller routes for controller-based APIs

// Example of a minimal API endpoint
app.MapGet("/hello", () => "Hello!");

app.Run();