using System;
using System.Collections.Generic;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.ReadModels.Common;

namespace Lfm.Domain.ReadModels.ReviewModels.StudentProfile
{
    public class LfmRequestDetailsReviewModel
    {
        public int Id { get; set; }
        
        public string SubjectName { get; set; }

        public string TagName { get; set; }

        public StudyingPlaces StudyingPlace { get; set; }

        public int AmountOfLessonsPerWeek { get; set; }
            
        public LessonDuration LessonDuration { get; set; }

        public int CostFrom { get; set; }

        public int CostTo { get; set; }
        
        public string WhenToPractice { get; set; }

        public string WhichHelp { get; set; }

        public string AdditionalWishes { get; set; }

        public DateTime CreationDateTime { get; set; }

        public ICollection<CommonReviewModel> MentorsInteresting { get; set; }
    }
}