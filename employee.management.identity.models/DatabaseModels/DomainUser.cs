using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;
/// <summary>
/// Represents a domain user in the system, containing properties related to the user's identity, contact information, role, and associations with other entities such as employees, managers, reporting lines, and tenants.
/// </summary>
public partial class DomainUser
{
    public Guid DomainUserId { get; set; }

    public int TenantId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual Manager? Manager { get; set; }

    public virtual ReportingLine? ReportingLine { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
