using System.ComponentModel.DataAnnotations;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.ReadModels.SearchModels
{
    public class MentorsSearchModel
    {
        [Range(1, int.MaxValue)]
        public int? SubjectId { get; set; }
        
        public StudyingPlaces? StudyingPlace { get; set; }
    
        [Range(1, int.MaxValue)]
        public int? TownId { get; set; }
    }
}