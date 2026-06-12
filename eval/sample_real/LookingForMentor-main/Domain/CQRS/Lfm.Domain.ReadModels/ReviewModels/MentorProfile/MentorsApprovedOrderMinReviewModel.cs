using System;

namespace Lfm.Domain.ReadModels.ReviewModels.MentorProfile
{
    public class MentorsApprovedOrderMinReviewModel
    {
        public int Id { get; set; }
        
        public string SubjectName { get; set; }

        public string TagName { get; set; }

        public string StudentName { get; set; }
        
        public string StudentPhoneNumber { get; set; }
        
        public string StudentEmail { get; set; }
        
        public DateTime ApprovedDateTime { get; set; }
    }
}