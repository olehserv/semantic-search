using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.SubjectEntities
{
    [Table("SubjectsTags")]
    public class SubjectsTag
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [Required]
        public virtual IEnumerable<Subject> Subjects { get; set; }
        
        
        public virtual IEnumerable<TagSubjectRelation> SubjectsRelations { get; set; }
    }
}