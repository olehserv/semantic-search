using System;
using Lfm.Core.Common.Web.Configurations;
using Lfm.Core.Common.Web.Extensions;
using LFM.DataAccess.DB.SQLite;
using LFM.DataAccess.DB.SQLite.Claims;
using Lfm.Domain.Common;
using LFM.Domain.Read;
using LFM.Domain.Write;
using Lfm.Web.Mvc.App.Attributes.Action;
using Lfm.Web.Mvc.App.Mapper.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Web.Mvc.App.Startup
{
    public static class DiServices
    {
        public static void Configure(IServiceCollection services, IConfiguration configuration)
        {
            var appConfiguration = services.ConfigureAppConfigurations(configuration);
            
            string sqliteDbPath = configuration.GetConnectionString("SqliteDbConnectionPath");
            
            services.AddLfmSqliteContext(sqliteDbPath);
            
            services.ConfigureApplicationCookie(options => options.LoginPath = $"/auth/login");

            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.None;
                options.ConsentCookie.Expiration = TimeSpan.FromHours(appConfiguration.UserSessionExpirationHours);
            });
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(appConfiguration.UserSessionExpirationHours);
            });

            services.AddScoped<AlertModelStateErrorsAttribute>();

            services.AddCommands();
            services.AddAutoMapper(typeof(ApplicationMapperConfigs));
            services.AddHttpContextAccessor();

            services.AddDataProviders();
            services.AddDomainCommonServices();
            
            services.AddControllersWithViews();
            services.AddRazorPages();
        }
    }
}