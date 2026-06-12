using System.Security.Claims;
using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Lfm.Domain.Common.Extensions
{
    public static class UserManagerExtensions
    {
        public static async Task<string> GetName<TU>(this SignInManager<TU> signInManager, ClaimsPrincipal user) 
            where TU : LfmUser
        {
            return (await signInManager.UserManager.GetUserAsync(user))?.Name;
        }
        
        public static async Task<string> GetName<TU>(this UserManager<TU> userManager, ClaimsPrincipal user) 
            where TU : LfmUser
        {
            return (await userManager.GetUserAsync(user))?.Name;
        }
    }
}