using System.Collections.Generic;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.Subject;

namespace Lfm.Domain.ReadModels.ReviewModels.MentorProfile
{
    public class MentorSubjectReviewModel
    {
        public int CostPerHour { get; set; }
        
        public string Description { get; set; }

        public int SubjectId { get; set; }

        public string SubjectName { get; set; }
        
        public virtual List<CommonReviewModel> SelectedTags { get; set; }
    }
}