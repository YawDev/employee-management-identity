using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure;

public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Manager> Managers { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CRITICAL: Must call base first to configure Identity tables
        base.OnModelCreating(modelBuilder);

            // Configure Department entity
        modelBuilder.Entity<Department>(entity =>
        {
            // Primary key configuration
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__B2079BEDB2C3B0B7");

            entity.ToTable("Department");

            // Configure DepartmentId to not auto-generate (explicitly assigned)
            entity.Property(e => e.DepartmentId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            // Department belongs to one Organization (required)
            entity.HasOne(d => d.Organization).WithMany(p => p.Departments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Departmen__Organ__34C8D9D1");

            // Department belongs to one Tenant (required)
            entity.HasOne(d => d.Tenant).WithMany(p => p.Departments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Departmen__Tenan__33D4B598");
        });

        // Configure Employee entity
        modelBuilder.Entity<Employee>(entity =>
        {
            // Primary key configuration
            entity.HasKey(e => e.EmployeeId).HasName("PK__Employee__7AD04F11DC87100E");

            entity.ToTable("Employee");

            // Configure EmployeeId to not auto-generate (explicitly assigned)
            entity.Property(e => e.EmployeeId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.JobTitle).HasMaxLength(255);
            entity.Property(e => e.Salary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            // Employee belongs to one Department (required)
            entity.HasOne(d => d.Department).WithMany(p => p.Employees)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Employee__Depart__38996AB5");

            // Employee has one Manager (optional) - many Employees can report to one Manager
            entity.HasOne(d => d.Manager).WithMany(p => p.EmployeeManagers)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__Employee__Manage__398D8EEE");

            // Employee belongs to one User (required) - links Employee record to User identity
            entity.HasOne(d => d.User).WithMany(p => p.EmployeeUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Employee__UserId__37A5467C");
        });

        // Configure Manager entity
        modelBuilder.Entity<Manager>(entity =>
        {
            // Primary key configuration
            entity.HasKey(e => e.ManagerId).HasName("PK__Manager__3BA2AAE1E907310E");

            entity.ToTable("Manager");

            // Ensure one User can only be one Manager (unique constraint)
            entity.HasIndex(e => e.UserId, "UQ__Manager__1788CC4DA45603B1").IsUnique();

            // Configure ManagerId to not auto-generate (explicitly assigned)
            entity.Property(e => e.ManagerId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            // Manager belongs to one Department (required)
            entity.HasOne(d => d.Department).WithMany(p => p.Managers)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Manager__Departm__3B75D760");

            // Manager has one Supervisor (optional, self-referencing) - hierarchical management structure
            entity.HasOne(d => d.Supervisor).WithMany(p => p.InverseSupervisor)
                .HasForeignKey(d => d.SupervisorId)
                .HasConstraintName("FK__Manager__Supervi__3C69FB99");

            // Manager has 1:1 relationship with User (required)
            entity.HasOne(d => d.User).WithOne(p => p.Manager)
                .HasForeignKey<Manager>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Manager__UserId__3A81B327");
        });

        // Configure Organization entity
        modelBuilder.Entity<Organization>(entity =>
        {
            // Primary key configuration
            entity.HasKey(e => e.OrganizationId).HasName("PK__Organiza__CADB0B1231302181");

            entity.ToTable("Organization");

            // Ensure Uid is unique across all organizations
            entity.HasIndex(e => e.Uid, "UQ__Organiza__C5B69A4B45D38D34").IsUnique();

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Industry).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            // Organization belongs to one Tenant (required) - multi-tenancy support
            entity.HasOne(d => d.Tenant).WithMany(p => p.Organizations)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Organizat__Tenan__32E0915F");
        });

        // Configure Tenant entity
        modelBuilder.Entity<Tenant>(entity =>
        {
            // Primary key configuration
            entity.HasKey(e => e.TenantId).HasName("PK__Tenant__2E9B47E1BDFCF135");

            entity.ToTable("Tenant");

            // Ensure Uid is unique across all tenants
            entity.HasIndex(e => e.Uid, "UQ__Tenant__C5B69A4B1996AD52").IsUnique();

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Logo).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.TimeZone).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            // Primary key configuration
            entity.HasKey(e => e.UserId).HasName("PK__User__1788CC4CD7557932");

            entity.ToTable("User");

            // Ensure Email is unique across all users
            entity.HasIndex(e => e.Email, "UQ__User__A9D10534521498A0").IsUnique();

            // Configure UserId to not auto-generate (explicitly assigned)
            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            // User has one Supervisor (optional, self-referencing) - hierarchical user structure
            entity.HasOne(d => d.Supervisor).WithMany(p => p.InverseSupervisor)
                .HasForeignKey(d => d.SupervisorId)
                .HasConstraintName("FK__User__Supervisor__36B12243");

            // User belongs to one Tenant (required) - multi-tenancy support (1:N relationship)
            entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User__TenantId__35BCFE0A");

            // User has 1:1 relationship with ApplicationUser (ASP.NET Identity) - required link to authentication system
            entity.HasOne(d => d.IdentityUser).WithOne()
            .HasForeignKey<User>(d => d.IdentityUserId)
            .HasPrincipalKey<ApplicationUser>(p => p.Id)
                .IsRequired()
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User__IdentityUs__3493CFA7");

            // Enforce uniqueness on IdentityUserId at database level - prevent multiple User records per identity
            entity.HasIndex(e => e.IdentityUserId, "UQ__User__C5B8FCD1D3B8A1E3").IsUnique().HasDatabaseName("UQ__User__IdentityUserId");
        });

        // Extension point for additional model configuration via partial method
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
