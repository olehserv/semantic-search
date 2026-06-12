using System;

namespace Lfm.Domain.ReadModels.ReviewModels.MentorProfile
{
    public class MentorPersonalOrderReviewModel
    {
        public int Id { get; set; }
        
        public string SubjectName { get; set; }

        public string TagName { get; set; }

        public string StudentName { get; set; }

        public DateTime CreationDateTime { get; set; }
    }
}