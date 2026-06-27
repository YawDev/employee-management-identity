using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure
{
    public partial class EmployeeManagementDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {

        public EmployeeManagementDbContext()
        {
        }

        public EmployeeManagementDbContext(DbContextOptions<EmployeeManagementDbContext> options)
        : base(options)
        {
        }

            public virtual DbSet<Department> Departments { get; set; }

            public virtual DbSet<Employee> Employees { get; set; }

            public virtual DbSet<Manager> Managers { get; set; }

            public virtual DbSet<Organization> Organizations { get; set; }

            public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

            public virtual DbSet<ReportingLine> ReportingLines { get; set; }

            public virtual DbSet<Tenant> Tenants { get; set; }

            public virtual DbSet<DomainUser> DomainUsers { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(e => e.DepartmentId).HasName("Department_pkey");

                entity.ToTable("Department", t => t.ExcludeFromMigrations());

                entity.Property(e => e.DepartmentId).ValueGeneratedNever();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Name).HasMaxLength(255);

                entity.HasOne(d => d.Organization).WithMany(p => p.Departments)
                    .HasForeignKey(d => d.OrganizationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Department_Organization");
            });

            modelBuilder.Entity<DomainUser>(entity =>
            {
                entity.HasKey(e => e.DomainUserId).HasName("DomainUser_pkey");

                entity.ToTable("DomainUser", t => t.ExcludeFromMigrations());

                entity.HasIndex(e => e.Email, "UQ_DomainUser_Email").IsUnique();

                entity.HasIndex(e => e.IdentityUserId, "UQ_DomainUser_IdentityUserId").IsUnique();

                entity.Property(e => e.DomainUserId).ValueGeneratedNever();
                entity.Property(e => e.Email).HasMaxLength(255);
                entity.Property(e => e.FirstName).HasMaxLength(100);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.LastName).HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.Role).HasMaxLength(50);

                entity.HasOne(d => d.Tenant).WithMany(p => p.DomainUsers)
                    .HasForeignKey(d => d.TenantId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_DomainUser_Tenant");
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeId).HasName("Employee_pkey");

                entity.ToTable("Employee", t => t.ExcludeFromMigrations());

                entity.Property(e => e.EmployeeId).ValueGeneratedNever();
                entity.Property(e => e.EmploymentStatus)
                    .HasMaxLength(10)
                    .HasDefaultValueSql("'Active'::character varying");
                entity.Property(e => e.JobTitle).HasMaxLength(255);
                entity.Property(e => e.Salary).HasPrecision(18, 2);

                entity.HasOne(d => d.Department).WithMany(p => p.Employees)
                    .HasForeignKey(d => d.DepartmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Employee_Department");

                entity.HasOne(d => d.DomainUser).WithMany(p => p.Employees)
                    .HasForeignKey(d => d.DomainUserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Employee_DomainUser");
            });

            modelBuilder.Entity<Manager>(entity =>
            {
                entity.HasKey(e => e.ManagerId).HasName("Manager_pkey");

                entity.ToTable("Manager", t => t.ExcludeFromMigrations());

                entity.HasIndex(e => e.DomainUserId, "Manager_DomainUserId_key").IsUnique();

                entity.Property(e => e.ManagerId).ValueGeneratedNever();

                entity.HasOne(d => d.Department).WithMany(p => p.Managers)
                    .HasForeignKey(d => d.DepartmentId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Manager_Department");

                entity.HasOne(d => d.DomainUser).WithOne(p => p.Manager)
                    .HasForeignKey<Manager>(d => d.DomainUserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Manager_DomainUser");
            });

            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasKey(e => e.OrganizationId).HasName("Organization_pkey");

                entity.ToTable("Organization", t => t.ExcludeFromMigrations());

                entity.HasIndex(e => e.Uid, "Organization_Uid_key").IsUnique();

                entity.Property(e => e.Industry).HasMaxLength(255);
                entity.Property(e => e.Name).HasMaxLength(255);

                entity.HasOne(d => d.Tenant).WithMany(p => p.Organizations)
                    .HasForeignKey(d => d.TenantId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Organization_Tenant");
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("RefreshToken_pkey");

                entity.ToTable("RefreshToken", t => t.ExcludeFromMigrations());

                entity.HasIndex(e => e.IdentityUserId, "IX_RefreshToken_IdentityUserId");

                entity.HasIndex(e => e.Token, "IX_RefreshToken_Token");

                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.Property(e => e.IsRevoked).HasDefaultValue(false);
                entity.Property(e => e.IsUsed).HasDefaultValue(false);
                entity.Property(e => e.Token).HasMaxLength(500);
            });

            modelBuilder.Entity<ReportingLine>(entity =>
            {
                entity.HasKey(e => e.ReportId).HasName("ReportingLine_pkey");

                entity.ToTable("ReportingLine", t => t.ExcludeFromMigrations());

                entity.Property(e => e.ReportId).ValueGeneratedNever();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

                entity.HasOne(d => d.Manager).WithMany(p => p.ReportingLines)
                    .HasForeignKey(d => d.ManagerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_ReportingLine_Manager");

                entity.HasOne(d => d.Report).WithOne(p => p.ReportingLine)
                    .HasForeignKey<ReportingLine>(d => d.ReportId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_ReportingLine_Report");
            });

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(e => e.TenantId).HasName("Tenant_pkey");

                entity.ToTable("Tenant", t => t.ExcludeFromMigrations());

                entity.HasIndex(e => e.Uid, "Tenant_Uid_key").IsUnique();

                entity.Property(e => e.Logo).HasMaxLength(255);
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.TimeZone).HasMaxLength(100);
            });

            OnModelCreatingPartial(modelBuilder);
        }
            partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    }

}