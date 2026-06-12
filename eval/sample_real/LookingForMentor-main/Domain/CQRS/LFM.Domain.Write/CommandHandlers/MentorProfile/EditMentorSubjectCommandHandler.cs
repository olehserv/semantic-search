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
    internal class EditMentorSubjectCommandHandler :
        BaseNeedsApproveCommandHandler<EditMentorSubjectCommand, CommandResult>
    {
        private readonly LfmDbContext _context;

        public EditMentorSubjectCommandHandler(LfmDbContext context)
        {
            _context = context;
        }
        
        public override ToDoOperationsEnum Operation => ToDoOperationsEnum.EditMentorSubject;

        public override async Task<CommandResult> ExecuteAsync(EditMentorSubjectCommand command)
        {
            await IsValid(command);
            
            var mentorSubject = await _context.MentorsSubjectsInfo
                .Include(s => s.Tags)
                .Where(m => m.MentorId == command.MentorId && m.SubjectId == command.SubjectId)
                .FirstOrDefaultAsync();

            if (!HasChangesAndApply(mentorSubject, command))
                return new CommandResult(true);

            await _context.SaveChangesAsync();

            return new CommandResult(true);
        }

        public override async Task IsValid(EditMentorSubjectCommand command)
        {
            if (command.TagIds?.Any() != true)
                throw new LfmException("No tags selected");

            var mentorSubjectExists = await _context.MentorsSubjectsInfo
                .Include(s => s.Tags)
                .Where(m => m.MentorId == command.MentorId && m.SubjectId == command.SubjectId)
                .AnyAsync();
            
            if (!mentorSubjectExists)
                throw new LfmException(Messages.DataNotFound);

            Subject subject = await _context.Subjects
                .Include(s => s.Tags)
                .FirstOrDefaultAsync(s => s.Id == command.SubjectId);

            if (subject == null)
                throw new LfmException(Messages.DataNotFound);

            if (command.TagIds.Exists(t => subject.Tags.All(tg => tg.Id != t)))
                throw new LfmException("Invalid subject tags.");
        }

        public override async Task<ICollection<CommandField>> GetPrettyCommand(EditMentorSubjectCommand command)
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

        private bool HasChangesAndApply(MentorsSubjectInfo subjectInfo, EditMentorSubjectCommand command)
        {
            bool hasChanges = false;

            if (subjectInfo.CostPerHour != command.CostPerHour)
            {
                subjectInfo.CostPerHour = command.CostPerHour;
                hasChanges = true;
            }
            
            if (subjectInfo.Description != command.Description)
            {
                subjectInfo.Description = command.Description;
                hasChanges = true;
            }

            var currentTags = subjectInfo.Tags.Select(t => t.TagId).ToList();
            if (currentTags.Count != command.TagIds.Count || !currentTags.All(command.TagIds.Contains))
            {
                subjectInfo.Tags = command.TagIds.Select(t => new MentorsSubjectTag
                {
                    TagId = t,
                    MentorsSubjectInfoId = subjectInfo.Id
                }).ToList();
                hasChanges = true;
            }

            return hasChanges;
        }
    }
}