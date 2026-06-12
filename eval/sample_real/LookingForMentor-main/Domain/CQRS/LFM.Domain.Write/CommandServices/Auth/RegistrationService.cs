using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Services.Role;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandServices.Auth
{
    internal class RegistrationService
    {
        private readonly SignInManager<LfmUser> _signInManager;
        private readonly UserManager<LfmUser> _userManager;
        private readonly ILfmRoleManager _roleManager;
        private readonly IUserClaimsPrincipalFactory<LfmUser> _userClaimFactory;

        public RegistrationService(
            SignInManager<LfmUser> signInManager, 
            UserManager<LfmUser> userManager, 
            ILfmRoleManager roleManager, 
            IUserClaimsPrincipalFactory<LfmUser> userClaimFactory)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _userClaimFactory = userClaimFactory;
        }

        public async Task<bool> RegisterUser(LfmUser user, string password, LfmIdentityRolesEnum role)
        {
            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == user.PhoneNumber))
            {
                throw new LfmException(Messages.UserWithPhoneAlreadyExists, user.PhoneNumber);
            }
            
            if (await _userManager.Users.AnyAsync(u => u.Email == user.Email))
            {
                throw new LfmException(Messages.UserWithEmailAlreadyExists, user.Email);
            }

            IdentityResult result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _roleManager.SetRoleToUser(user, role);
                await _signInManager.SignInAsync(user, false);
                await _userClaimFactory.CreateAsync(user);
            }
            else
            {
                //throw new LfmException(Messages.RegistrationFailed);
            }

            return result.Succeeded;
        }
    }
}