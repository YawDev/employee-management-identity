using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;

public partial class Employee
{
    public Guid EmployeeId { get; set; }

    public Guid DomainUserId { get; set; }

    public Guid DepartmentId { get; set; }

    public string? JobTitle { get; set; }

    public DateOnly? HireDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal? Salary { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual DomainUser DomainUser { get; set; } = null!;
}
