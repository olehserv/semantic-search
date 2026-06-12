using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.MentorProfile
{
    internal class DeleteMentorSubjectCommandHandler : ICommandHandler<DeleteMentorSubjectCommand, CommandResult>
    {
        private readonly LfmDbContext _context;

        public DeleteMentorSubjectCommandHandler(LfmDbContext context)
        {
            _context = context;
        }

        public async Task<CommandResult> ExecuteAsync(DeleteMentorSubjectCommand command)
        {
            var subject = await _context.MentorsSubjectsInfo
                .Include(m => m.Tags)
                .FirstOrDefaultAsync(m => m.MentorId == command.MentorId && m.SubjectId == command.SubjectId);

            if (subject == null)
                throw new LfmException(Messages.DataNotFound);

            _context.MentorsSubjectsInfo.Remove(subject);
           await _context.SaveChangesAsync();

           return new CommandResult(true);
        }
    }
}