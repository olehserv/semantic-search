using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using Lfm.Core.Common.Web.Data;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.Administration;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Services.Role;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Web.Manager.Blazor.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ActivateAccount : PageModel
    {
        private readonly SignInManager<LfmUser> _signInManager;
        private readonly IRepository<PendingManagerCreation> _pendingManagersCreations;
        private readonly LfmDbContext _context;
        private readonly ILfmRoleManager _roleManager;
        
        public ActivateAccount(
            IRepository<PendingManagerCreation> pendingManagersCreations, 
            SignInManager<LfmUser> signInManager,
            LfmDbContext context, 
            ILfmRoleManager roleManager)
        {
            _pendingManagersCreations = pendingManagersCreations;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
        }

        [BindProperty]
        public ActivateAccountModel Input { get; set; }
        
        public class ActivateAccountModel
        {
            [Required(ErrorMessage = Messages.RequiredField)]
            [DataType(DataType.Password)]
            [MinLength(10, ErrorMessage = Messages.MinLenght)]
            [Display(Name = "Пароль")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Підтвердження паролю")]
            [Compare("Password", ErrorMessage = "Паролі не співпадають.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGet([Required]string creationStamp)
        {
            if (!(await _pendingManagersCreations
                .GetQueryable()
                .Where(c => !c.IsActivated)
                .AnyAsync(p => p.CreationStamp == creationStamp)))
            {
                HttpContext.Response.Redirect(Constants.DefaultUrl);
            }
        }

        public async Task<IActionResult> OnPost([Required]string creationStamp)
        {
            if (ModelState.IsValid)
            {
                var createModel = await _pendingManagersCreations
                    .GetQueryable()
                    .Where(c => !c.IsActivated)
                    .FirstOrDefaultAsync(p => p.CreationStamp == creationStamp);

                if (createModel == default)
                    return LocalRedirect(Constants.DefaultUrl);

                LfmUser user = new LfmUser
                {
                    Name = createModel.Name,
                    Email = createModel.Email,
                    UserName = createModel.Email,
                    PhoneNumber = createModel.PhoneNumber,
                    LastLoginTime = DateTime.Now
                };
                
                var result = await _signInManager.UserManager.CreateAsync(user, Input.Password);
                await _roleManager.SetRoleToUser(user, LfmIdentityRolesEnum.Manager);

                if (result.Succeeded)
                {
                    createModel.IsActivated = true;
                    _context.Update(createModel);
                    await _context.SaveChangesAsync();
                    await _signInManager.SignInAsync(user, true);
                }
            }
            
            return LocalRedirect(Constants.DefaultUrl);
        }
    }
}