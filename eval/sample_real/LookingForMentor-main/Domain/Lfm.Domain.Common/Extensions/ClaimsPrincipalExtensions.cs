using System;
using System.Security.Claims;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.Common.Extensions
{
    public static class LfmUserClaimsPrincipalExtensions
    {
        public static string GetName(this ClaimsPrincipal claimsPrincipal) 
            => GetClaimValue(claimsPrincipal, ClaimTypes.GivenName);
        
        public static string GetPhoneNumber(this ClaimsPrincipal claimsPrincipal) 
            => GetClaimValue(claimsPrincipal, ClaimTypes.MobilePhone);
        
        public static string GetEmail(this ClaimsPrincipal claimsPrincipal) 
            => GetRequiredClaimValue(claimsPrincipal, ClaimTypes.Email);

        public static int GetId(this ClaimsPrincipal claimsPrincipal)
        {
            string claimValue = GetRequiredClaimValue(claimsPrincipal, ClaimTypes.NameIdentifier);
            if (!int.TryParse(claimValue, out int id))
            {
                throw new LfmException(Messages.InvalidUserClaim);
            }
            return id;
        }
        
        public static LfmIdentityRolesEnum GetRole(this ClaimsPrincipal claimsPrincipal)
        {
            string claimValue = GetRequiredClaimValue(claimsPrincipal, ClaimTypes.Role);
            if (!Enum.TryParse(claimValue, ignoreCase: true, out LfmIdentityRolesEnum role))
            {
                throw new LfmException(Messages.InvalidUserClaim);
            }
            return role;
        }

        public static bool IsStudent(this ClaimsPrincipal claimsPrincipal) 
            => IsInRole(claimsPrincipal, LfmIdentityRolesEnum.Student);
        
        public static bool IsMentor(this ClaimsPrincipal claimsPrincipal) 
            => IsInRole(claimsPrincipal, LfmIdentityRolesEnum.Mentor);
        
        public static bool IsAdmin(this ClaimsPrincipal claimsPrincipal) 
            => IsInRole(claimsPrincipal, LfmIdentityRolesEnum.Admin);
        
        public static bool IsManager(this ClaimsPrincipal claimsPrincipal) 
            => IsInRole(claimsPrincipal, LfmIdentityRolesEnum.Manager);
        
        private static bool IsInRole(ClaimsPrincipal claimsPrincipal, LfmIdentityRolesEnum role)
            => claimsPrincipal.GetRole() == role;

        private static string GetClaimValue(ClaimsPrincipal claimsPrincipal, string claimType) 
            => claimsPrincipal.FindFirstValue(claimType);

        private static string GetRequiredClaimValue(ClaimsPrincipal claimsPrincipal, string claimType)
            => GetClaimValue(claimsPrincipal, claimType) 
               ?? throw new LfmException(Messages.UserClaimNotFound, claimType);
    }
}