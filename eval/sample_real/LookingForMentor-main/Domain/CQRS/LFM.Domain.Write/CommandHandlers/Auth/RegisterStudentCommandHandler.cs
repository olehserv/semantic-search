using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.CommandServices.Auth;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;

namespace LFM.Domain.Write.CommandHandlers.Auth
{
    internal class RegisterStudentCommandHandler : ICommandHandler<RegisterStudentCommand, CommandResult>
    {
        private readonly RegistrationService _registrationService;

        public RegisterStudentCommandHandler(RegistrationService registrationService)
        {
            _registrationService = registrationService;
        }

        public async Task<CommandResult> ExecuteAsync(RegisterStudentCommand command)
        {
            LfmUser newUser = new LfmUser
            {
                Name = command.Name,
                UserName = command.Email,
                Email = command.Email,
                PhoneNumber = command.PhoneNumber
            };
            
            bool regResult = await _registrationService.RegisterUser(newUser, command.Password, LfmIdentityRolesEnum.Student);
            return new CommandResult(regResult);
        }
    }
}