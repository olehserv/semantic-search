using Lfm.Core.Common.Web.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Core.Common.Web.Extensions
{
    public static class ConfigurationsExtensions
    {
        public static AppConfigurations ConfigureAppConfigurations(this IServiceCollection services, IConfiguration configuration)
        {
            IConfigurationSection section = configuration.GetSection("AppConfigurations");
            services.Configure<AppConfigurations>(section);

            var appConfiguration = section.Get<AppConfigurations>();
            return appConfiguration;
        }
    }
}