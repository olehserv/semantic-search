using Lfm.Domain.Common.Caching.User;
using Lfm.Domain.Common.Services.Role;
using Lfm.Domain.Common.Services.User;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Domain.Common
{
    public static class ModuleExporter
    {
        public static void AddDomainCommonServices(this IServiceCollection services)
        {
            services.AddTransient<ILfmRoleManager, LfmRoleManager>();
            services.AddTransient<ICommonUserProvider, CommonUserProvider>();
            services.AddSingleton<IUserCachingService, UserCachingService>();
        }
    }
}