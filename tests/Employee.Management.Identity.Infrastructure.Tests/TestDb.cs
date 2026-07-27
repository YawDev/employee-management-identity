using employee.management.identity.infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Employee.Management.Identity.Infrastructure.Tests;

// Fresh isolated in-memory context per test (unique database name).
internal static class TestDb
{
    public static EmployeeManagementDbContext New() =>
        new(new DbContextOptionsBuilder<EmployeeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static DomainUser DomainUser(Guid identityUserId, string role = "default-user") => new()
    {
        DomainUserId = Guid.NewGuid(),
        IdentityUserId = identityUserId,
        TenantId = 1,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Role = role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
