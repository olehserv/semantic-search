using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using Lfm.Core.Common.Web.Data;
using Lfm.Core.Common.Web.Extensions;
using Lfm.Core.Common.Web.SessionAlerts;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.Mediator;
using LFM.Domain.Write.ResultModels;
using Lfm.Web.Mvc.App.Extensions;
using Lfm.Web.Mvc.Models.FormModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LFM.Web.Mvc.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly IMediator _commandBus;
        private readonly IMapper _mapper;

        public AuthController(
            IMediator commandBus, 
            IMapper mapper)
        {
            _commandBus = commandBus;
            _mapper = mapper;
        }

        #region Get Pages
        
        [HttpGet("login")]
        public async Task<IActionResult> Login(string returnUrl = Constants.DefaultUrl)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return View(new LoginFormModel{ReturnUrl = returnUrl});
        }

        [HttpGet("register/mentor")]
        public IActionResult RegisterMentor()
        {
            return View(new RegisterMentorFormModel());
        }
        
        [HttpGet("register/student")]
        public IActionResult RegisterStudent()
        {
            return View(new RegisterStudentFormModel());
        }
        #endregion

        #region Post Data
        
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginFormModel model)
        {
            return await this.HandleAction(async () =>
                {
                    var command = _mapper.Map<LoginUserCommand>(model);
                        
                    CommandResult loginResult = await _commandBus.ExecuteCommand<LoginUserCommand, CommandResult>(command);
                        
                    if (loginResult.IsSuccess)
                    {
                        this.AlertSuccess(Messages.LoginSuccessful);
                        return this.Redirect(model);
                    }
                    this.AlertError(Messages.LoginFailed);
                    return View(model);
                },
                View(model));
        }

        [HttpPost("register/mentor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterMentor(RegisterMentorFormModel model)
        {
            return await this.HandleAction(async () => 
                {
                    var command = _mapper.Map<RegisterMentorCommand>(model);
                
                    CommandResult loginResult = await _commandBus.ExecuteCommand<RegisterMentorCommand, CommandResult>(command);
                    
                    if (loginResult.IsSuccess)
                    {
                        this.AlertSuccess(Messages.RegistrationSuccessful, "Заповніть інформацію профілю та додайте ваші предмети щоб почати роботу.");
                        return RedirectToAction("EditGeneralInfo", "MentorUserCabinet");
                    }
                    this.AlertError(Messages.RegistrationFailed);
                    return View(model);
                },
                View(model));
        }
        
        [HttpPost("register/student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterStudent(RegisterStudentFormModel model)
        {
            return await this.HandleAction(async () =>
                {
                    var command = _mapper.Map<RegisterStudentCommand>(model);
                        
                    CommandResult registrationResult = await _commandBus.ExecuteCommand<RegisterStudentCommand, CommandResult>(command);
                        
                    if (registrationResult.IsSuccess)
                    {
                        this.AlertSuccess(Messages.RegistrationSuccessful, string.Empty);
                        return RedirectToAction("LfmRequests", "StudentUserCabinet");
                    }
                    this.AlertError(Messages.RegistrationFailed);
                    return View(model);
                },
                View(model));
        }

        [Authorize]
        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            LogoutUserCommand command = new LogoutUserCommand();
            CommandResult logoutResult = await _commandBus.ExecuteCommand<LogoutUserCommand, CommandResult>(command);
            
            if (logoutResult.IsSuccess)
                this.AlertSuccess(Messages.LogoutSuccessful);
            else
                this.AlertError(Messages.LogoutFailed);
            
            return LocalRedirect(Constants.DefaultUrl);
        }
        
        #endregion
    }
}