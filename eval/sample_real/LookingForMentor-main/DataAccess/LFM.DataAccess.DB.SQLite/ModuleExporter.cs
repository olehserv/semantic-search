using LFM.DataAccess.DB.Core;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.SQLite.Claims;
using LFM.DataAccess.DB.SQLite.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.DataAccess.DB.SQLite
{
    public static class ModuleExporter
    {
        public static void AddLfmSqliteContext(this IServiceCollection services, string sqliteDbPath)
        {
            services.AddDbContext<LfmDbContext, LfmSqliteDbContext>(options =>
            {
                SqliteConnectionStringBuilder connectionStringBuilder = new SqliteConnectionStringBuilder();
                connectionStringBuilder.DataSource = sqliteDbPath;
                
                options.UseSqlite(connectionStringBuilder.ToString());
                options.LogTo(System.Console.WriteLine);
            });

            services.AddIdentity<LfmUser, LfmRole>(options =>
                {
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = false;
                    options.Password.RequiredLength = 1;
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<LfmDbContext>()
                .AddClaimsPrincipalFactory<LfmUserClaimsPrincipalFactory>();

            services.AddRepository();
        }
    }
}