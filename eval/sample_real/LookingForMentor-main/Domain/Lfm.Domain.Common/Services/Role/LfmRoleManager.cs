using System;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Microsoft.AspNetCore.Identity;

namespace Lfm.Domain.Common.Services.Role
{
    internal class LfmRoleManager : ILfmRoleManager
    {
        private readonly RoleManager<LfmRole> _roleManager;
        private readonly UserManager<LfmUser> _userManager;

        public LfmRoleManager(
            RoleManager<LfmRole> roleManager, 
            UserManager<LfmUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<LfmRole> GetRole(LfmIdentityRolesEnum roleType)
        {
            return await _roleManager.FindByNameAsync(roleType.ToString());
        }

        public async Task SetRoleToUser(LfmUser user, LfmIdentityRolesEnum roleType)
        {
            await _userManager.AddToRoleAsync(user, roleType.ToString());
        }

        public async Task<LfmIdentityRolesEnum> RetrieveUserRole(int userId)
        {
            LfmUser user = await _userManager.FindByIdAsync(userId.ToString());
            
            if (user == null)
            {
                throw new LfmException(Messages.DataNotFound, "Користувача");
            }

            var roleName = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            if (roleName == null || !Enum.TryParse(roleName, ignoreCase: true, out LfmIdentityRolesEnum role))
            {
                throw new LfmException(Messages.UserDoesNotHaveRole);
            }

            return role;
        }
    }
}