namespace employee.management.identity.models.Dtos
{
    public class EditUserRoleDto
    {
        public Guid IdentityUserId { get; set; }
        public string NewRole { get; set; } = string.Empty;
    }
}
