using employee.management.identity.models.DatabaseModels;
using Microsoft.AspNetCore.Identity;

namespace employee.management.identity.models.Dtos
{
    // DTO For Domain User 
    public class UserDTO
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public Guid IdentityUserId { get; set; }
        public TenantDTO Tenant { get; set; }
        public bool IsActive { get; set; }
        public string? JobTitle { get; set; }
        public Guid? ManagerId { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? SupervisorId { get; set; }
    }
}
