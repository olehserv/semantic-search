using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.CommandServices.Auth;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;

namespace LFM.Domain.Write.CommandHandlers.Auth
{
    internal class RegisterMentorCommandHandler : ICommandHandler<RegisterMentorCommand, CommandResult>
    {
        private readonly RegistrationService _registrationService;
        private readonly LfmDbContext _context;

        public RegisterMentorCommandHandler(
            RegistrationService registrationService, 
            LfmDbContext context)
        {
            _registrationService = registrationService;
            _context = context;
        }
        
        public async Task<CommandResult> ExecuteAsync(RegisterMentorCommand command)
        {
            LfmUser newUser = new LfmUser
            {
                Name = command.Name,
                UserName = command.Email,
                Email = command.Email,
                PhoneNumber = command.PhoneNumber
            };
            
            bool regResult = await _registrationService.RegisterUser(newUser, command.Password, LfmIdentityRolesEnum.Mentor);

            if (regResult)
            {
                MentorsProfile defaultProfile = new MentorsProfile
                {
                    IsVerified = false,
                    MentorId = newUser.Id,
                    Surname = string.Empty,
                    MiddleName = string.Empty,
                    ProfileImageId = null,
                    AboutMe = string.Empty,
                    TownId = null,
                    StudyingPlace = null,
                    Education = string.Empty
                };

                await _context.MentorsProfiles.AddAsync(defaultProfile);
                await _context.SaveChangesAsync();
            }
            
            return new CommandResult(regResult);
        }
    }
}