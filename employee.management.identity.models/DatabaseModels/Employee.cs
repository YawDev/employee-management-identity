
namespace employee.management.identity.models.DatabaseModels;

public partial class Employee
{
    public Guid EmployeeId { get; set; }

    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid? ManagerId { get; set; }

    public string? JobTitle { get; set; }

    public DateOnly? HireDate { get; set; }

    public decimal? Salary { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual User? Manager { get; set; }

    public virtual User User { get; set; } = null!;
}
