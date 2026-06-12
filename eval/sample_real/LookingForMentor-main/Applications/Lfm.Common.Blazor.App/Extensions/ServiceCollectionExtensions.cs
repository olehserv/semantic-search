using Lfm.Common.Blazor.App.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Lfm.Common.Blazor.App.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddAuthStateProvider(this IServiceCollection services)
            => services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();
    }
}