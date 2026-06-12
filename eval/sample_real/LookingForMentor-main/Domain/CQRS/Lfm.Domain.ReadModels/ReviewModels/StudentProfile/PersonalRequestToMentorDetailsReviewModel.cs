using System;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.ReadModels.ReviewModels.StudentProfile
{
    public class PersonalRequestToMentorDetailsReviewModel
    {
        public int Id { get; set; }
        
        public string SubjectName { get; set; }

        public string TagName { get; set; }
        
        
        public int MentorId { get; set; }

        public string MentorName { get; set; }
        
        public int CostPerHour { get; set; }
        
        public StudyingPlaces StudyingPlace { get; set; }

        public int AmountOfLessonsPerWeek { get; set; }
            
        public LessonDuration LessonDuration { get; set; }
        
        public string WhenToPractice { get; set; }

        public string WhichHelp { get; set; }

        public string AdditionalWishes { get; set; }

        public DateTime CreationDateTime { get; set; }
    }
}