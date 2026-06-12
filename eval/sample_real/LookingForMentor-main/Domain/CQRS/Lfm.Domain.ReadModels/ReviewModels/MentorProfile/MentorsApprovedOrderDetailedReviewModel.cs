using System;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.ReadModels.ReviewModels.MentorProfile
{
    public class MentorsApprovedOrderDetailedReviewModel
    {
        public string SubjectName { get; set; }

        public string TagName { get; set; }

        public StudyingPlaces StudyingPlace { get; set; }

        public int AmountOfLessonsPerWeek { get; set; }
            
        public LessonDuration LessonDuration { get; set; }

        public int CostPerHour { get; set; }
        
        public string StudentName { get; set; }
        
        public string StudentPhoneNumber { get; set; }
        
        public string StudentEmail { get; set; }
        
        public string WhenToPractice { get; set; }

        public string WhichHelp { get; set; }

        public string AdditionalWishes { get; set; }

        public DateTime ApprovedDateTime { get; set; }
    }
}