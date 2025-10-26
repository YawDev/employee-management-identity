namespace employee.management.identity.models.DatabaseModels;
public partial class User
{
    public Guid UserId { get; set; }

    public int TenantId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    public Guid? SupervisorId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Employee> EmployeeManagers { get; set; } = new List<Employee>();

    public virtual ICollection<Employee> EmployeeUsers { get; set; } = new List<Employee>();

    public virtual ICollection<User> InverseSupervisor { get; set; } = new List<User>();

    public virtual Manager? Manager { get; set; }

    public virtual User? Supervisor { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ApplicationUser IdentityUser { get; set; }
}
