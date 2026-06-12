using System.Collections.Generic;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.Subject;

namespace Lfm.Domain.ReadModels.ReviewModels.Mentor
{
    public class ContactMentorInfo
    {
        public int MentorId { get; set; }
        
        public string Name { get; set; }
        
        public StudyingPlaces StudyingPlace { get; set; }
        
        public SubjectInfo Subject { get; set; }

        public class SubjectInfo
        {
            public int SubjectId { get; set; }
        
            public string SubjectName { get; set; }
            
            public int CostPerHour { get; set; }

            public List<CommonReviewModel> Tags { get; set; }
        }
    }
}