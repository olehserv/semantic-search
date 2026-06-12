using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Entities;
using Lfm.Domain.Common.Caching.User;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.AspNetCore.Identity;

namespace LFM.Domain.Write.CommandHandlers.Auth
{
    internal class LogoutUserCommandHandler : ICommandHandler<LogoutUserCommand, CommandResult>
    {
        private readonly SignInManager<LfmUser> _signInManager;
        private readonly IUserCachingService _userCachingService;
        
        public LogoutUserCommandHandler(
            SignInManager<LfmUser> signInManager, 
            IUserCachingService userCachingService)
        {
            _signInManager = signInManager;
            _userCachingService = userCachingService;
        }
        public async Task<CommandResult> ExecuteAsync(LogoutUserCommand command)
        {
            await _userCachingService.RemoveUserFromCache();
            
            await _signInManager.SignOutAsync();

            return new CommandResult(true);
        }
    }
}