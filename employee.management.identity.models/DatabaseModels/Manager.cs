using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;

public partial class Manager
{
    public Guid ManagerId { get; set; }

    public Guid DomainUserId { get; set; }

    public Guid DepartmentId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual DomainUser DomainUser { get; set; } = null!;

    public virtual ICollection<ReportingLine> ReportingLines { get; set; } = new List<ReportingLine>();
}
