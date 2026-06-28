using Microsoft.AspNetCore.Identity;

namespace employee.management.identity.models.DatabaseModels;

/// <summary>
/// Represents an application user in the identity system, extending the IdentityUser class with a Guid as the primary key type.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    // You can add custom properties here if needed
}
