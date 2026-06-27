using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace employee.management.identity.infrastructure
{
    /// <summary>
    /// Design-time factory used by `dotnet ef`. The app's DI container can't be
    /// built at design time yet (IUserRepository isn't registered), so EF can't
    /// obtain the context from the host. This factory wires Npgsql directly,
    /// reading the connection string from the startup project's appsettings
    /// (or the ConnectionStrings__DefaultConnection env var). Design-time only.
    /// </summary>
    public class EmployeeManagementDbContextFactory : IDesignTimeDbContextFactory<EmployeeManagementDbContext>
    {
        public EmployeeManagementDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? ReadDefaultConnectionFromAppSettings();

            var optionsBuilder = new DbContextOptionsBuilder<EmployeeManagementDbContext>();
            // A null connection string is fine for `migrations add` (no DB access);
            // `database update` will surface a clear error if it can't be resolved.
            optionsBuilder.UseNpgsql(connectionString);

            return new EmployeeManagementDbContext(optionsBuilder.Options);
        }

        private static string? ReadDefaultConnectionFromAppSettings()
        {
            foreach (var dir in CandidateDirectories())
            {
                foreach (var file in new[] { "appsettings.Development.json", "appsettings.json" })
                {
                    var path = Path.Combine(dir, file);
                    if (!File.Exists(path)) continue;

                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var section) &&
                        section.TryGetProperty("DefaultConnection", out var value) &&
                        value.GetString() is { Length: > 0 } connectionString)
                    {
                        return connectionString;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> CandidateDirectories()
        {
            yield return Directory.GetCurrentDirectory();
            yield return AppContext.BaseDirectory;
            yield return Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "..", "employee.management.identity"));
        }
    }
}
