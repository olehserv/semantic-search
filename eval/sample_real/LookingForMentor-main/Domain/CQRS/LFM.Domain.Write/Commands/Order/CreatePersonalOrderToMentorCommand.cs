using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Declarations;

namespace LFM.Domain.Write.Commands.Order
{
    public class CreatePersonalOrderToMentorCommand : BaseCommand
    {
        public int MentorId { get; set; }
        
        public int? StudentId { get; set; }
        
        public int SubjectId { get; set; }

        public int TagId { get; set; }

        public StudyingPlaces StudyingPlace { get; set; }

        public int AmountOfLessonsPerWeek { get; set; }
            
        public LessonDuration LessonDuration { get; set; }
        
        public string StudentName { get; set; }

        public string StudentPhoneNumber { get; set; }
            
        public string StudentEmail { get; set; }
        
        public string WhenToPractice { get; set; }

        public string WhichHelp { get; set; }

        public string AdditionalWishes { get; set; }
    }
}