namespace employee.management.identity.models.Dtos
{
    public class TenantDTO
    {
        public int TenantId { get; set; }
        public Guid Uid { get; set; }
        public string Name { get; set; } = null!;
        public string? Logo { get; set; }

    }
}
