namespace employee.management.identity.models.DatabaseModels;

public partial class Organization
{
    public int OrganizationId { get; set; }

    public Guid Uid { get; set; }

    public int TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Industry { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual Tenant Tenant { get; set; } = null!;
}
