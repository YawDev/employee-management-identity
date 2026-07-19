using employee.management.identity.infrastructure;
using employee.management.identity.models.Dtos;
using employee.management.identity.models.DatabaseModels;

namespace employee.management.identity.core.Interfaces
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetTenantInfoAsync(int tenantId);
    }
}
