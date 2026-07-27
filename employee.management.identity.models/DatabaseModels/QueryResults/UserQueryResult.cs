using employee.management.identity.infrastructure;

namespace employee.management.identity.models.DatabaseModels.QueryResults
{
    // Joined result of ApplicationUser and DomainUser for queries that need both sets of data.
    public class UserQueryResult
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;
        public Guid DomainUserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public bool PhoneNumberConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; }
        public int AccessFailedCount { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public virtual Manager? Manager { get; set; }
        public virtual ReportingLine? ReportingLine { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;    
    
    }



}
