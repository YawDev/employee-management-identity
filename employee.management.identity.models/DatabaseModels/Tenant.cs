using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;

public partial class Tenant
{
    public int TenantId { get; set; }

    public Guid Uid { get; set; }

    public string Name { get; set; } = null!;

    public string? Logo { get; set; }

    public string? TimeZone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<DomainUser> DomainUsers { get; set; } = new List<DomainUser>();

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}
