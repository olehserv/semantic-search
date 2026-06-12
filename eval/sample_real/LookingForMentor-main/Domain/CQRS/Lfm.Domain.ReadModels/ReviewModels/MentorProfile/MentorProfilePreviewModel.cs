using System.Collections.Generic;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.Subject;

namespace Lfm.Domain.ReadModels.ReviewModels.MentorProfile
{
    public class MentorProfilePreviewModel
    {
        public bool WantReceivePersonalOrders { get; set; }
        
        public string  Surname { get; set; }
        
        public string  MiddleName { get; set; }
        
        public string AboutMe { get; set; }
        
        public string TownName { get; set; }

        public StudyingPlaces StudyingPlace { get; set; }
        
        public string Education { get; set; }
    }
}