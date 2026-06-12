using System.Linq;
using System.Reflection;
using LFM.DataAccess.DB.Core.Entities.ToDoEntities;
using LFM.Domain.Write.ToDo;
using Lfm.Web.Mvc.App.DataInitializers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace LFM.Web.Mvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            
            InitialDbSeed.Init(host.Services);
            
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
