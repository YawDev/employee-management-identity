namespace employee.management.identity.models.Dtos
{
    public class SystemUserDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Role { get; set; }
        public Guid IdentityUserId { get; set; }
        public bool IsActive { get; set; }
    }
}
