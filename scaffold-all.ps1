# Re-sync ALL model classes from the employee_management DB. Regenerates every entity below.
# Run from the solution root: employee-management-identity/
# The scaffolded context lands in infrastructure/_scaffold as a throwaway reference —
# harvest its OnModelCreating config into the real ApplicationDbContext (IdentityDbContext).
$appsettings = Get-Content "employee.management.identity/appsettings.Development.json" -Raw | ConvertFrom-Json
$connString = $appsettings.ConnectionStrings.DefaultConnection

dotnet ef dbcontext scaffold $connString Npgsql.EntityFrameworkCore.PostgreSQL `
  -t Tenant `
  -t Organization `
  -t Department `
  -t DomainUser `
  -t Employee `
  -t Manager `
  -t ReportingLine `
  -t RefreshToken `
  --output-dir ../employee.management.identity.models/DatabaseModels `
  --context-dir _scaffold `
  --context ScaffoldDbContext `
  --no-onconfiguring `
  --force `
  --project employee.management.identity.infrastructure `
  --startup-project employee.management.identity
