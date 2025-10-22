
namespace employee.management.identity.models.DatabaseModels;

public partial class Manager
{
    public Guid ManagerId { get; set; }

    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid? SupervisorId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<Manager> InverseSupervisor { get; set; } = new List<Manager>();

    public virtual Manager? Supervisor { get; set; }

    public virtual User User { get; set; } = null!;
}
