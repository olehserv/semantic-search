using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.DataProcessors;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Caching.User;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.PrettyCommandConverter;
using LFM.Domain.Write.ResultModels;
using LFM.Domain.Write.ToDo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.MentorProfile
{
    internal class EditMentorProfileCommandHandler :
        BaseNeedsApproveCommandHandler<EditMentorProfileCommand, CommandResult>
    {
        private readonly LfmDbContext _context;
        private readonly IUserCachingService _userCachingService;
        private readonly UserManager<LfmUser> _userManager;

        public EditMentorProfileCommandHandler(
            LfmDbContext context, 
            IUserCachingService userCachingService, 
            UserManager<LfmUser> userManager)
        {
            _context = context;
            _userCachingService = userCachingService;
            _userManager = userManager;
        }
        
        public override ToDoOperationsEnum Operation => ToDoOperationsEnum.EditMentorProfile;
        
        public override async Task<CommandResult> ExecuteAsync(EditMentorProfileCommand command)
        {
            await IsValid(command);
            
            var mentor = await _context.LfmUsers
                .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (user, userRole) => new { user, userRole.RoleId })
                .Where(u => u.RoleId == (int)LfmIdentityRolesEnum.Mentor)
                .Select(u => u.user)
                .FirstOrDefaultAsync(u => u.Id == command.MentorId);
            
            var profile = await _context.MentorsProfiles
                .Include(m => m.ProfileImage)
                .FirstOrDefaultAsync(m => m.MentorId == command.MentorId);
            
            if (!mentor.Name.Equals(command.Name, StringComparison.InvariantCulture))
            {
                mentor.Name = command.Name;
                var updateResult = await _userManager.UpdateAsync(mentor);
                if (!updateResult.Succeeded)
                {
                    throw new LfmException(updateResult.Errors.FirstOrDefault()?.Description ?? Messages.SystemError);
                }
                await _userCachingService.RemoveUserFromCache();
                await _userCachingService.TryCacheUser((mentor, LfmIdentityRolesEnum.Mentor));
            }
            
            if (HasChangesAndApply(profile, command))
            {
                profile.IsVerified = true;
                await _context.SaveChangesAsync();
            }
            
            return new CommandResult(true);
        }

        public override async Task IsValid(EditMentorProfileCommand command)
        {
            var mentorExist = await _context.LfmUsers
                .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (user, userRole) => new { user, userRole.RoleId })
                .Where(u => u.RoleId == (int)LfmIdentityRolesEnum.Mentor)
                .Select(u => u.user)
                .AnyAsync(u => u.Id == command.MentorId);

            if (!mentorExist)
                throw new LfmException(Messages.DataNotFound, "User");

            var profileExist = await _context.MentorsProfiles
                .Include(m => m.ProfileImage)
                .AnyAsync(m => m.MentorId == command.MentorId);

            if (!profileExist)
                throw new LfmException(Messages.DataNotFound);

            var isTownExist = await _context.UkrainianTowns.AnyAsync(t => t.Id == command.TownId);
            if (!isTownExist)
                throw new LfmException(Messages.DataNotFound, "Town");
        }

        private bool HasChangesAndApply(MentorsProfile profile, EditMentorProfileCommand command)
        {
            bool hasChanges = false;

            if (profile.WantReceivePersonalOrders != command.WantReceivePersonalOrders)
            {
                profile.WantReceivePersonalOrders = command.WantReceivePersonalOrders;
                hasChanges = true;
            }

            if (profile.Surname != command.Surname)
            {
                profile.Surname = command.Surname;
                hasChanges = true;
            }
            
            if(profile.MiddleName != command.MiddleName)
            {
                profile.MiddleName = command.MiddleName;
                hasChanges = true;
            }

            if(profile.AboutMe != command.AboutMe)
            {
                profile.AboutMe = command.AboutMe;
                hasChanges = true;
            }

            if(profile.TownId != command.TownId)
            {
                profile.TownId = command.TownId;
                hasChanges = true;
            }

            if(profile.StudyingPlace != command.StudyingPlace)
            {
                profile.StudyingPlace = command.StudyingPlace;
                hasChanges = true;
            }

            if(profile.Education != command.Education)
            {
                profile.Education = command.Education;
                hasChanges = true;
            }

            if (command.ProfileImageBytes?.Length > 0)
            {
                if (profile.ProfileImageId == null)
                    profile.ProfileImage = new MentorsProfileImage();
                
                byte[] imageData = command.ProfileImageBytes;
                imageData = ImageProcessor.MakeImageSquare(imageData);
                imageData = ImageProcessor.ReduceImage(imageData, 200);
                
                string imageBase64String = Convert.ToBase64String(imageData);
                if (profile.ProfileImage.Image != imageBase64String)
                {
                    profile.ProfileImage.Image = imageBase64String;
                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        public override async Task<ICollection<CommandField>> GetPrettyCommand(EditMentorProfileCommand command)
        {
            var town = await _context.UkrainianTowns
                .FirstOrDefaultAsync(t => t.Id == command.TownId);
            
            var profile = await _context.MentorsProfiles
                .Include(m => m.ProfileImage)
                .Include(m => m.Mentor)
                .FirstOrDefaultAsync(m => m.MentorId == command.MentorId);
            
            return new List<CommandField>
            {
                new CommandField("Ідентифікатор ментора", command.MentorId),
                new CommandField("Ім'я", command.Name, profile.Mentor.Name != command.Name),
                new CommandField("Прізвище", command.Surname, profile.Surname != command.Surname),
                new CommandField("По батькові", command.MiddleName, profile.MiddleName != command.MiddleName),
                new CommandField("Фото профілю", command.ProfileImageBytes, false, true),
                new CommandField("Про", command.AboutMe, profile.AboutMe != command.AboutMe),
                new CommandField("Місто", town.Name, profile.TownId != command.TownId),
                new CommandField("Місце проведення занять", command.StudyingPlace switch
                                                            {
                                                                StudyingPlaces.ONLINE_ONLY => "онлайн",
                                                                StudyingPlaces.OFFLINE_ONLY => "оффлайн",
                                                                _ => "онлайн або оффлайн"
                                                            }, profile.StudyingPlace != command.StudyingPlace),
                new CommandField("Освіта", command.Education, profile.Education != command.Education),
                new CommandField("Бажаю отримувати персональні заявки", 
                    command.WantReceivePersonalOrders ? "Так" : "Ні",
                    profile.WantReceivePersonalOrders != command.WantReceivePersonalOrders),
            };
        }
    }
}