using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.PrettyCommandConverter;
using LFM.Domain.Write.ResultModels;
using LFM.Domain.Write.ToDo;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace LFM.Domain.Write.CommandHandlers.MentorProfile
{
    internal class AddMentorSubjectCommandHandler :
        BaseNeedsApproveCommandHandler<AddMentorSubjectCommand, CommandResult>
    {
        private readonly LfmDbContext _context;

        public AddMentorSubjectCommandHandler(LfmDbContext context)
        {
            _context = context;
        }

        public override ToDoOperationsEnum Operation => ToDoOperationsEnum.AddMentorSubject;

        public override async Task<CommandResult> ExecuteAsync(AddMentorSubjectCommand command)
        {
            await IsValid(command);
            
            var selectedTags = command.TagIds
                .Select(t => new MentorsSubjectTag {TagId = t})
                .ToList();
            
            await _context.MentorsSubjectsInfo.AddAsync(new MentorsSubjectInfo
            {
                MentorId = command.MentorId,
                CostPerHour = command.CostPerHour,
                Description = command.Description,
                SubjectId = command.SubjectId,
                Tags = selectedTags
            });

            await _context.SaveChangesAsync();

            return new CommandResult(true);
        }

        public override async Task IsValid(AddMentorSubjectCommand command)
        {
            if (command.TagIds?.Any() != true)
                throw new LfmException("No tags selected");

            var mentorSubjects = await _context.MentorsProfiles
                .Where(m => m.MentorId == command.MentorId)
                .Select(m => m.SubjectsInfo)
                .FirstOrDefaultAsync();

            if (mentorSubjects == null)
                throw new LfmException(Messages.SystemError);

            Subject subject = await _context.Subjects
                .Include(s => s.Tags)
                .FirstOrDefaultAsync(s => s.Id == command.SubjectId);

            if (subject == null)
                throw new LfmException(Messages.DataNotFound);
            
            if (mentorSubjects.Exists(p => p.SubjectId == command.SubjectId))
                throw new LfmException($"Subject '{subject.Name}' already added.");
            
            if (command.TagIds.Exists(t => subject.Tags.All(tg => tg.Id != t)))
                throw new LfmException("Invalid subject tags.");
        }

        public override async Task<ICollection<CommandField>> GetPrettyCommand(AddMentorSubjectCommand command)
        {
            var subject = await _context.Subjects
                .Include(s => s.Tags)
                .Where(s => s.Id == command.SubjectId)
                .FirstOrDefaultAsync();
            
            return new List<CommandField>
            {
                new CommandField("Ідентифікатор ментора", command.MentorId),
                new CommandField("Ціна за годину", command.CostPerHour),
                new CommandField("Опис", command.Description),
                new CommandField("Назва предмету", subject.Name),
                new CommandField("Напрямки підгтовки", JsonConvert.SerializeObject(subject.Tags
                                                       .Where(t => command.TagIds.Contains(t.Id))
                                                       .Select(t => t.Name)
                                                       .ToList()))
            };
        }
    }
}