using System.Collections.Generic;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.ReadModels.ReviewModels.Mentor
{
    public class MentorDetailedPreviewModel
    {
        public int MentorId { get; set; }

        public string Name { get; set; }

        public IEnumerable<SubjectInfo> SubjectsList { get; set; }

        public string TownName { get; set; }

        public StudyingPlaces StudyingPlace { get; set; }

        public string EducationInfo { get; set; }

        public string AboutInfo { get; set; }

        public bool WantReceivePersonalOrders { get; set; }
        
        //TODO: Add rating
        //TODO: Add reviews amount
        //TODO: Add comments list

        public class SubjectInfo
        {
            public int SubjectId { get; set; }
            
            public string SubjectName { get; set; }
            
            public int CostPerHour { get; set; }

            public List<string> Tags { get; set; }
        }
    }
}