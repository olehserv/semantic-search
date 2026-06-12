using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.Domain.Write.Commands.StudentProfile;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.StudentProfile
{
    public class ApproveMentorProposeCommandHandler : ICommandHandler<ApproveMentorProposeCommand, ApproveMentorProposeResult>
    {
        private readonly LfmDbContext _context;
        private readonly IMapper _mapper;

        
        public ApproveMentorProposeCommandHandler(LfmDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<ApproveMentorProposeResult> ExecuteAsync(ApproveMentorProposeCommand command)
        {
            var orderRequest = await _context.OrdersRequests
                .Include(o => o.InterestedMentors)
                .FirstOrDefaultAsync(o =>
                    o.StudentId == command.StudentId && o.Id == command.OrderId &&
                    o.InterestedMentors.Any(i => i.MentorId == command.MentorId));

            if (orderRequest == null)
                throw new LfmException(Messages.DataNotFound, "Заявку");
            
            ApprovedOrder approvedOrder = _mapper.Map<OrderRequest, ApprovedOrder>(orderRequest);

            int? costPerHour = (await _context.MentorsSubjectsInfo
                .FirstOrDefaultAsync(s => s.MentorId == command.MentorId && 
                                          s.SubjectId == approvedOrder.SubjectId))?.CostPerHour;

            if (!costPerHour.HasValue)
                throw new LfmException(Messages.AccessDenied);

            approvedOrder.MentorId = command.MentorId;

            approvedOrder.CostPerHour = costPerHour.Value;

            _context.ApprovedOrders.Add(approvedOrder);
            _context.OrdersRequests.Remove(orderRequest);

            await _context.SaveChangesAsync();

            string mentorName = (await _context.LfmUsers.FirstOrDefaultAsync(u => u.Id == command.MentorId)).Name;
            
            return new ApproveMentorProposeResult(true)
            {
                Id = approvedOrder.Id,
                MentorName = mentorName
            };
        }
    }
}