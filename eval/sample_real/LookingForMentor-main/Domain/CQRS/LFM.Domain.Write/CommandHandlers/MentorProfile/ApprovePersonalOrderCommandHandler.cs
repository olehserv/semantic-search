using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.MentorProfile
{
    internal class ApprovePersonalOrderCommandHandler : ICommandHandler<ApprovePersonalOrderCommand, IdCommandResult>
    {
        private readonly LfmDbContext _context;
        private readonly IMapper _mapper;

        public ApprovePersonalOrderCommandHandler(
            LfmDbContext context, 
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IdCommandResult> ExecuteAsync(ApprovePersonalOrderCommand command)
        {
            OrderRequest orderRequest = await _context.OrdersRequests
                .Where(o => o.MentorId == command.MentorId && o.Id == command.OrderRequestId)
                .FirstOrDefaultAsync();

            if (orderRequest == null)
                throw new LfmException(Messages.DataNotFound, "Заявки");
            
            ApprovedOrder mentorsOrder = _mapper.Map<OrderRequest, ApprovedOrder>(orderRequest);

            int? costPerHour = (await _context.MentorsSubjectsInfo
                .FirstOrDefaultAsync(s => s.MentorId == command.MentorId && 
                                          s.SubjectId == mentorsOrder.SubjectId))?.CostPerHour;

            if (!costPerHour.HasValue)
                throw new LfmException(Messages.AccessDenied);

            mentorsOrder.CostPerHour = costPerHour.Value;

            _context.ApprovedOrders.Add(mentorsOrder);
            _context.OrdersRequests.Remove(orderRequest);

            await _context.SaveChangesAsync();

            return new IdCommandResult(true) { Id = mentorsOrder.Id };
        }
    }
}