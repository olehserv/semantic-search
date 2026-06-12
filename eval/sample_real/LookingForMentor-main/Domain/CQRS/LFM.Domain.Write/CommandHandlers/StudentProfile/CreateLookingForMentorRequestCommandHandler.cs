using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Commands.StudentProfile;
using LFM.Domain.Write.PrettyCommandConverter;
using LFM.Domain.Write.ResultModels;
using LFM.Domain.Write.ToDo;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Write.CommandHandlers.StudentProfile
{
    internal class CreateLookingForMentorRequestCommandHandler :
        BaseNeedsApproveCommandHandler<CreateLookingForMentorRequestCommand, CommandResult>
    {
        private readonly LfmDbContext _context;
        private readonly IMapper _mapper;

        public CreateLookingForMentorRequestCommandHandler(
            LfmDbContext context, 
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        
        public override ToDoOperationsEnum Operation => ToDoOperationsEnum.CreateLfmRequest;

        public override async Task<CommandResult> ExecuteAsync(CreateLookingForMentorRequestCommand command)
        {
            await IsValid(command);
            
            OrderRequest order = _mapper.Map<CreateLookingForMentorRequestCommand, OrderRequest>(command);

            _context.OrdersRequests.Add(order);

            await _context.SaveChangesAsync();
            
            return new CommandResult(true);
        }

        public override async Task IsValid(CreateLookingForMentorRequestCommand command)
        {
            var query = _context.OrdersRequests
                .Where(o => o.StudentId == command.StudentId);

            if (await query.CountAsync() >= 10)
                throw new LfmException(Messages.OrderRequestFailed);

            // if (!await query.AnyAsync(o => o.SubjectId == command.SubjectId))
            // {
            //     string subjectName = (await _context.Subjects.FirstOrDefaultAsync(s => s.Id == command.SubjectId)).Name;
            //     throw new LfmException(Messages.OrderRequestAlreadyExist, subjectName);
            // }
        }

        public override async Task<ICollection<CommandField>> GetPrettyCommand(CreateLookingForMentorRequestCommand command)
        {
            var subject = await _context.Subjects
                .Include(s => s.Tags)
                .Where(s => s.Id == command.SubjectId)
                .FirstOrDefaultAsync();
            
            return new List<CommandField>
            {
                new CommandField("Назва предмету", subject.Name),
                new CommandField("Напрямок підгтовки", subject.Tags
                    .FirstOrDefault(t => t.Id == command.TagId)
                    ?.Name),
                new CommandField("Місце проведення занять", command.StudyingPlace switch
                {
                    StudyingPlaces.ONLINE_ONLY => "онлайн",
                    StudyingPlaces.OFFLINE_ONLY => "оффлайн",
                    _ => "онлайн або оффлайн"
                }),
                new CommandField("Кількість занять за тиждень", command.AmountOfLessonsPerWeek),
                new CommandField("Тривалість занять", command.LessonDuration switch
                {
                    LessonDuration.ONE_HOUR => "1 година",
                    LessonDuration.ONE_HALF_HOUR => "1,5 години",
                    LessonDuration.TWO_HOURS => "2 години",
                    _ => "більше 2 годин"
                }),
                new CommandField("Мінімальна ціна", command.CostFrom),
                new CommandField("Максимальна ціна", command.CostTo),
                new CommandField("Ідентифікатор студента", command.StudentId),
                new CommandField("Імя студента", command.StudentName),
                new CommandField("Номер телефону студента", command.StudentPhoneNumber),
                new CommandField("Електронна пошта студента", command.StudentEmail),
                new CommandField("Місце проведення занять", command.WhenToPractice),
                new CommandField("З чим потрібна допомога", command.WhichHelp),
                new CommandField("Додаткові побажання", command.AdditionalWishes)
            };
        }
    }
}