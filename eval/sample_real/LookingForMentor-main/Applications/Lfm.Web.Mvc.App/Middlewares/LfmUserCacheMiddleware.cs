using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Entities;
using Lfm.Domain.Common.Caching.User;
using Lfm.Domain.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Lfm.Web.Mvc.App.Middlewares
{
    internal class LfmUserCacheMiddleware
    {
        private readonly RequestDelegate _next;
        
        public LfmUserCacheMiddleware(RequestDelegate next)
        {
            _next = next;
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
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                await _next(context);
                return;
            }
            
            if (context.User.Identity.IsAuthenticated)
            {
                if (!context.User.IsStudent() && !context.User.IsMentor())
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