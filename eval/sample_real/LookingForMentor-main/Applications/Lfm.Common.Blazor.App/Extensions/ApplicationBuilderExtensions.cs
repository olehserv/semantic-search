using Lfm.Common.Blazor.App.Middlewares;
using LFM.DataAccess.DB.Core.Types;
using Microsoft.AspNetCore.Builder;

namespace Lfm.Common.Blazor.App.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseCachingUser(this IApplicationBuilder builder, LfmIdentityRolesEnum role) 
            => builder.UseMiddleware<CommonUserCacheMiddleware>(role);
        
        public static IApplicationBuilder UseErrorsHandler(this IApplicationBuilder builder) 
            => builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}