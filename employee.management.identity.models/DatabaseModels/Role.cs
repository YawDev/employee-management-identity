

namespace employee.management.identity.models.DatabaseModels
{
    public class Role
    {
        public Guid RoleId { get; set; }
        public string Name { get; set; }
        public string Permissions { get; set; }
    }
}