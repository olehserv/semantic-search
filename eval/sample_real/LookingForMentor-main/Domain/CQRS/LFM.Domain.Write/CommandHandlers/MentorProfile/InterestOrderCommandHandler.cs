using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.MentorProfile
{
    public class InterestOrderCommandHandler : ICommandHandler<InterestOrderCommand, CommandResult>
    {
        private readonly LfmDbContext _context;

        public InterestOrderCommandHandler(LfmDbContext context)
        {
            _context = context;
        }

        public async Task<CommandResult> ExecuteAsync(InterestOrderCommand command)
        {
            var order = await _context.OrdersRequests
                .Include(o => o.InterestedMentors)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId);

            if (!await CanNominate(command, order))
                throw new LfmException(Messages.ActionNotAllowed);

            InterestedMentorsOrdersRelation interest = new InterestedMentorsOrdersRelation
            {
                MentorId = command.MentorId,
                OrderRequestId = command.OrderId
            };

            _context.InterestedMentorsOrdersRelations.Add(interest);
            await _context.SaveChangesAsync();
            
            return new CommandResult(true);
        }

        private async Task<bool> CanNominate(InterestOrderCommand command, OrderRequest order)
        {
            var mentorInfo = await _context.MentorsProfiles
                .Where(m => m.MentorId == command.MentorId)
                .Include(t => t.SubjectsInfo)
                .ThenInclude(t => t.Tags)
                .Select(m => new { m.StudyingPlace, Subject = m.SubjectsInfo.FirstOrDefault(s => s.SubjectId == order.SubjectId) })
                .FirstOrDefaultAsync();

            if (mentorInfo?.Subject == null)
                throw new LfmException(Messages.AccessDenied);

            if ((order.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE ||
                 mentorInfo.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE ||
                 mentorInfo.StudyingPlace == order.StudyingPlace) && 
                mentorInfo.Subject.Tags.Select(t => t.TagId).Contains(order.TagId) &&
                mentorInfo.Subject.CostPerHour >= order.CostFrom &&
                mentorInfo.Subject.CostPerHour <= order.CostTo && 
                order.InterestedMentors.All(i => i.MentorId != command.MentorId))
            {
                return true;
            }

            return false;
        }
    }
}