using System;
using System.Collections.Generic;
using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure;

public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__B2079BEDB2C3B0B7");

            entity.ToTable("Department");

            entity.Property(e => e.DepartmentId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Organization).WithMany(p => p.Departments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Departmen__Organ__34C8D9D1");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Departments)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Departmen__Tenan__33D4B598");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("PK__Employee__7AD04F11DC87100E");

            entity.ToTable("Employee");

            entity.Property(e => e.EmployeeId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.JobTitle).HasMaxLength(255);
            entity.Property(e => e.Salary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Department).WithMany(p => p.Employees)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Employee__Depart__38996AB5");

            entity.HasOne(d => d.Manager).WithMany(p => p.EmployeeManagers)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__Employee__Manage__398D8EEE");

            entity.HasOne(d => d.User).WithMany(p => p.EmployeeUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Employee__UserId__37A5467C");
        });

        modelBuilder.Entity<Manager>(entity =>
        {
            entity.HasKey(e => e.ManagerId).HasName("PK__Manager__3BA2AAE1E907310E");

            entity.ToTable("Manager");

            entity.HasIndex(e => e.UserId, "UQ__Manager__1788CC4DA45603B1").IsUnique();

            entity.Property(e => e.ManagerId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Department).WithMany(p => p.Managers)
                .HasForeignKey(d => d.DepartmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Manager__Departm__3B75D760");

            entity.HasOne(d => d.Supervisor).WithMany(p => p.InverseSupervisor)
                .HasForeignKey(d => d.SupervisorId)
                .HasConstraintName("FK__Manager__Supervi__3C69FB99");

            entity.HasOne(d => d.User).WithOne(p => p.Manager)
                .HasForeignKey<Manager>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Manager__UserId__3A81B327");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.OrganizationId).HasName("PK__Organiza__CADB0B1231302181");

            entity.ToTable("Organization");

            entity.HasIndex(e => e.Uid, "UQ__Organiza__C5B69A4B45D38D34").IsUnique();

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Industry).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Organizations)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Organizat__Tenan__32E0915F");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.TenantId).HasName("PK__Tenant__2E9B47E1BDFCF135");

            entity.ToTable("Tenant");

            entity.HasIndex(e => e.Uid, "UQ__Tenant__C5B69A4B1996AD52").IsUnique();

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Logo).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.TimeZone).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__1788CC4CD7557932");

            entity.ToTable("User");

            entity.HasIndex(e => e.Email, "UQ__User__A9D10534521498A0").IsUnique();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Supervisor).WithMany(p => p.InverseSupervisor)
                .HasForeignKey(d => d.SupervisorId)
                .HasConstraintName("FK__User__Supervisor__36B12243");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User__TenantId__35BCFE0A");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
