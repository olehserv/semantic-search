using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.Domain.Write.Commands.StudentProfile;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.StudentProfile
{
    internal class DeleteLookingForMentorRequestCommandHandler : ICommandHandler<DeleteLookingForMentorRequestCommand, CommandResult>
    {
        private readonly LfmDbContext _context;

        public DeleteLookingForMentorRequestCommandHandler(LfmDbContext context)
        {
            _context = context;
        }

        public async Task<CommandResult> ExecuteAsync(DeleteLookingForMentorRequestCommand command)
        {
            var order = await _context.OrdersRequests
                .FirstOrDefaultAsync(o => o.StudentId == command.StudentId && o.Id == command.OrderId);

            if (order == null)
                throw new LfmException(Messages.DataNotFound, "Order");

            _context.OrdersRequests.Remove(order);
            await _context.SaveChangesAsync();
            
            return new CommandResult(true);
        }
    }
}