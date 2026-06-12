using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Commands.Order;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.Order
{
    internal class CreatePersonalOrderToMentorCommandHandler : ICommandHandler<CreatePersonalOrderToMentorCommand, CreatePersonalOrderResult>
    {
        private readonly LfmDbContext _context;
        private readonly IMapper _mapper;

        public CreatePersonalOrderToMentorCommandHandler(
            LfmDbContext context, 
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<CreatePersonalOrderResult> ExecuteAsync(CreatePersonalOrderToMentorCommand command)
        {
            var mentor = await _context.MentorsProfiles
                .Include(m => m.SubjectsInfo)
                .ThenInclude(s => s.Tags)
                .Where(m => 
                    m.IsVerified && 
                    m.WantReceivePersonalOrders &&
                    m.SubjectsInfo.Any() && 
                    m.MentorId == command.MentorId)
                .Select(m => new { m.StudyingPlace, Subject = m.SubjectsInfo.FirstOrDefault(s => s.SubjectId == command.SubjectId) })
                .FirstOrDefaultAsync();

            if (mentor == null)
                return new CreatePersonalOrderResult(false);

            if (command.StudentId.HasValue && !await CanCreate(command, mentor.Subject))
            {
                return new CreatePersonalOrderResult(false);
            }

            OrderRequest order = _mapper.Map<CreatePersonalOrderToMentorCommand, OrderRequest>(command);

            StudyingPlaces? mentorStudyingPlace = mentor.StudyingPlace;

            if (mentorStudyingPlace.HasValue && mentorStudyingPlace != StudyingPlaces.ONLINE_AND_OFFLINE)
                order.StudyingPlace = mentorStudyingPlace.Value;

            order.CostPerHour = mentor.Subject.CostPerHour;
            
            _context.OrdersRequests.Add(order);

            await _context.SaveChangesAsync();

            string mentorName = (await _context.LfmUsers.FirstOrDefaultAsync(m => m.Id == command.MentorId)).Name;

            return new CreatePersonalOrderResult(true) { MentorName = mentorName};
        }

        private async Task<bool> CanCreate(CreatePersonalOrderToMentorCommand command, MentorsSubjectInfo subject)
        {
            bool isOrderExists = await _context.OrdersRequests
                .AnyAsync(p => p.MentorId.HasValue && 
                               p.MentorId.Value == command.MentorId && 
                               p.StudentId == command.StudentId);

            bool isTagAvailable = subject?.Tags.FirstOrDefault(t => t.TagId == command.TagId) != null;

            return !isOrderExists && isTagAvailable;
        }
    }
}