using System.Collections.Generic;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.ReadModels.ReviewModels.Mentor
{
    public class MentorPreviewModel
    {
        public int MentorId { get; set; }

        public string Name { get; set; }

        public IEnumerable<Subject> SubjectsList { get; set; }

        public string TownName { get; set; }
        
        public int TownId { get; set; }

        public StudyingPlaces? StudyingPlace { get; set; }
        
        public class Subject
        {
            public int Id { get; set; }
            
            public string Name { get; set; }
        }
    }
}