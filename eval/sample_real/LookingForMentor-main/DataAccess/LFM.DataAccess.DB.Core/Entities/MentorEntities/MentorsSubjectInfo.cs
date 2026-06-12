using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;

namespace LFM.DataAccess.DB.Core.Entities.MentorEntities
{
    [Table("MentorsSubjectsInfo")]
    public class MentorsSubjectInfo
    {
        public int Id { get; set; }
        
        public int MentorId { get; set; }

        [Range(50, 500)]
        public int CostPerHour { get; set; }
        
        [Required]
        [MaxLength(500)]
        [MinLength(100)]
        public string Description { get; set; }

        public int SubjectId { get; set; }

        public virtual Subject Subject { get; set; }
        
        public virtual List<MentorsSubjectTag> Tags { get; set; }
    }
}