using LFM.Domain.Read.Caching;
using LFM.Domain.Read.EntityProvideServices;
using LFM.Domain.Read.Mapper;
using LFM.Domain.Read.Providers;
using LFM.Domain.Read.Providers.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.Domain.Read
{
    public static class ModuleExporter
    {
        public static void AddDataProviders(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(ReadModelsMapperConfigs));
            services.AddSingleton<SubjectCachingService>();
            services.AddSingleton<TownsCachingService>();

            AddProviders(services);
            
            AddInternalServices(services);
        }

        private static void AddProviders(IServiceCollection services)
        {
            services.AddScoped<IMentorProfileProvider, MentorProfileProvider>();
            services.AddScoped<ISubjectsProvider, SubjectsProvider>();
            services.AddScoped<IMentorsProvider, MentorsProvider>();
            services.AddScoped<IMasterDataProvider, MasterDataProvider>();
            services.AddScoped<IStudentProfileProvider, StudentProfileProvider>();
        }
        
        private static void AddInternalServices(IServiceCollection services)
        {
            services.AddScoped<SubjectProvideService>();
            services.AddScoped<TownsProvideService>();
        }
    }
}