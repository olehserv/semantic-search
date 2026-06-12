using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Caching.User;
using Lfm.Domain.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Lfm.Common.Blazor.App.Middlewares
{
    internal class CommonUserCacheMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly LfmIdentityRolesEnum _role;
        
        public CommonUserCacheMiddleware(RequestDelegate next, LfmIdentityRolesEnum role)
        {
            _next = next;
            _role = role;
        }

        /// <summary>
        /// Handles request
        /// </summary>
        public async Task InvokeAsync(
            HttpContext context, 
            IUserCachingService userCachingService, 
            SignInManager<LfmUser> signInManager)
        {
            Endpoint endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            {
                await _next(context);
                return;
            }
            
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (context.User.GetRole() != _role)
                {
                    await userCachingService.RemoveUserFromCache();
                    await signInManager.SignOutAsync();
                }
                else
                {
                    await userCachingService.EnsureUserCache();
                }
            }

            await _next(context);
        }
    }
}