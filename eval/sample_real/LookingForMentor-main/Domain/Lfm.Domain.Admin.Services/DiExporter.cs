using Lfm.Domain.Admin.Services.DataProviders;
using Lfm.Domain.Admin.Services.DataProviders.Implementations;
using Lfm.Domain.Admin.Services.DataWriters;
using Lfm.Domain.Admin.Services.DataWriters.Implementations;
using Lfm.Domain.Admin.Services.Mapper;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Domain.Admin.Services
{
    public static class DiExporter
    {
        public static void AddAdminServices(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(AdminModelsMaps));
            services.AddScoped<IManagersProvider, ManagersProvider>();
            
            services.AddScoped<IManagersWriteService, ManagersWriteService>();
        }
    }
}