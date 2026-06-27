using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;

public partial class Department
{
    public Guid DepartmentId { get; set; }

    public int OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<Manager> Managers { get; set; } = new List<Manager>();

    public virtual Organization Organization { get; set; } = null!;
}
