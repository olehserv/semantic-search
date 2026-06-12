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
    internal class RejectPersonalOrderCommandHandler : ICommandHandler<RejectPersonalOrderCommand, CommandResult>
    {
        private readonly LfmDbContext _context;
        private readonly IMapper _mapper;

        public RejectPersonalOrderCommandHandler(
            LfmDbContext context, 
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<CommandResult> ExecuteAsync(RejectPersonalOrderCommand command)
        {
            OrderRequest orderRequest = await _context.OrdersRequests
                .Where(o => o.MentorId == command.MentorId && o.Id == command.OrderRequestId)
                .FirstOrDefaultAsync();

            if (orderRequest == null)
                throw new LfmException(Messages.DataNotFound);

            if (orderRequest.StudentId.HasValue)
            {
                RejectedOrder rejectedOrder = _mapper.Map<OrderRequest, RejectedOrder>(orderRequest);

                rejectedOrder.RejectReason = command.Reason;
                
                _context.RejectedOrders.Add(rejectedOrder);
            }
            
            _context.OrdersRequests.Remove(orderRequest);

            await _context.SaveChangesAsync();

            return new CommandResult(true);
        }
    }
}