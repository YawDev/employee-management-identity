using AutoMapper;
using AutoMapper.QueryableExtensions;
using employee.management.identity.core.Interfaces;
using employee.management.identity.models.Dtos;
using employee.management.identity.models.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace employee.management.identity.infrastructure
{
    public class TenantRepository(EmployeeManagementDbContext context, IMapper mapper) : ITenantRepository
    {
        private readonly EmployeeManagementDbContext _context = context;
        private readonly IMapper _mapper = mapper;

        public async Task<Tenant?> GetTenantInfoAsync(int tenantId)
        {
            var tenant = await _context.Tenants.FindAsync(tenantId);
            return tenant;
        }
    }
}
