using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using employee.management.identity.models.Constants;
using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.models.Dtos
{
    public class CreateIdentityDTO
    {
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = RoleConstants.Default;
        public int TenantId { get; set; }
    }
}
