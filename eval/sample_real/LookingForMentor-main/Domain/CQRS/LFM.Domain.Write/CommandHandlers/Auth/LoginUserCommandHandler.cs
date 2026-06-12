using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.AspNetCore.Identity;

namespace LFM.Domain.Write.CommandHandlers.Auth
{
    internal class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, CommandResult>
    {
        private readonly SignInManager<LfmUser> _signInManager;

        public LoginUserCommandHandler(SignInManager<LfmUser> signInManager)
        {
            _signInManager = signInManager;
        }
        
        public async Task<CommandResult> ExecuteAsync(LoginUserCommand command)
        {
            if (_signInManager.IsSignedIn(_signInManager.Context.User))
            {
                return new CommandResult(false);
            }

            LfmUser user = await _signInManager.UserManager.FindByEmailAsync(command.Email);

            if (user == null)
            {
                throw new LfmException(Messages.DataNotFound, "User");
            }

            var loginResult = await _signInManager.PasswordSignInAsync(user.UserName, command.Password, command.RememberMe, lockoutOnFailure: false);
            
            return new CommandResult(loginResult.Succeeded);
        }
    }
}