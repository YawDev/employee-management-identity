using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;

public partial class ReportingLine
{
    public Guid ReportId { get; set; }

    public Guid ManagerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Manager Manager { get; set; } = null!;

    public virtual DomainUser Report { get; set; } = null!;
}
