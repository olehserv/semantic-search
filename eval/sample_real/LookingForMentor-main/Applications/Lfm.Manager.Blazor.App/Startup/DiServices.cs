using System;
using Lfm.Common.Blazor.App.Extensions;
using Lfm.Common.Blazor.App.Identity;
using Lfm.Core.Common.Web.Extensions;
using LFM.DataAccess.DB.SQLite;
using Lfm.Domain.Common;
using Lfm.Domain.Manager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Manager.Blazor.App.Startup
{
    public static class DiServices
    {
        public static void Configure(IServiceCollection services, IConfiguration configuration)
        {
            var appConfiguration = services.ConfigureAppConfigurations(configuration);
            
            string sqliteDbPath = configuration.GetConnectionString("SqliteDbConnectionPath");
            
            services.AddLfmSqliteContext(sqliteDbPath);
            
            services.ConfigureApplicationCookie(
                options =>
                {
                    options.LoginPath = "/Identity/Account/Login";
                    options.Cookie.Name = ".Lfm.Manager";
                });
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
            
            services.AddHttpContextAccessor();

            services.AddDomainCommonServices();
            services.AddManagerServices();
                
            services.AddControllers();
            
            services.AddRazorPages();
            services.AddServerSideBlazor();
            
            services.AddAuthStateProvider();
        }
    }
}