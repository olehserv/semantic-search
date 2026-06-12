using System;

namespace Lfm.Domain.ReadModels.ReviewModels.StudentProfile
{
    public class LfmRequestReviewModel
    {
        public int Id { get; set; }
        
        public string SubjectName { get; set; }

        public string TagName { get; set; }

        public int CostFrom { get; set; }
        
        public int CostTo { get; set; }

        public DateTime CreationDateTime { get; set; }
    }
}