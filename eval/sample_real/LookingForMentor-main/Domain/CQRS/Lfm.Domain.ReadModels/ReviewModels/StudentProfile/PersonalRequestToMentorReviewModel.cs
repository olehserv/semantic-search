using System;

namespace Lfm.Domain.ReadModels.ReviewModels.StudentProfile
{
    public class PersonalRequestsToMentorsReviewModel
    {
        public int Id { get; set; }
        
        public string SubjectName { get; set; }

        public string TagName { get; set; }

        public string MentorName { get; set; }
        
        public int CostPerHour { get; set; }

        public DateTime CreationDateTime { get; set; }
    }
}