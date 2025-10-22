namespace employee.management.identity.models.DatabaseModels;

public partial class Tenant
{
    public int TenantId { get; set; }

    public Guid Uid { get; set; }

    public string Name { get; set; } = null!;

    public string? Logo { get; set; }

    public string? TimeZone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
