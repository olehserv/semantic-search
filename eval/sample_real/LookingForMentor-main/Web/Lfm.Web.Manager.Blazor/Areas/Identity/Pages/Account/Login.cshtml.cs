using System;
using System.Threading.Tasks;
using Lfm.Common.Blazor.App.PageModels;
using LFM.Core.Common.Data;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Services.Role;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lfm.Web.Manager.Blazor.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<LfmUser> _signInManager;
        private readonly ILfmRoleManager _roleManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly LfmDbContext _context;
        
        public LoginModel(
            SignInManager<LfmUser> signInManager, 
            ILfmRoleManager roleManager, 
            ILogger<LoginModel> logger, 
            LfmDbContext context)
        {
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
            _roleManager = roleManager;
        }

        [BindProperty]
        public LoginInputModel Input { get; set; }
        
        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;
            
            return Task.CompletedTask;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            
            if (ModelState.IsValid)
            {
                LfmUser user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);

                if (user != null)
                {
                    var role = await _roleManager.RetrieveUserRole(user.Id);
                    if (role == LfmIdentityRolesEnum.Manager)
                    {
                        bool isBlocked = await _context.BlockedManagers.AnyAsync(m => m.ManagerId == user.Id);

                        if (!isBlocked)
                        {
                            var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                        
                            if (result.Succeeded)
                            {
                                user.LastLoginTime = DateTime.Now;
                                await _signInManager.UserManager.UpdateAsync(user);
                            
                                _logger.LogInformation("User logged in.");
                                return LocalRedirect(returnUrl);
                            }
                        }

                        ModelState.AddModelError(string.Empty, Messages.LoginFailed);
                        return Page();
                    }
                }
                
                ModelState.AddModelError(string.Empty, string.Format(Messages.DataNotFound, "Користувача"));
                return Page();
            }

            return Page();
        }
    }
}
