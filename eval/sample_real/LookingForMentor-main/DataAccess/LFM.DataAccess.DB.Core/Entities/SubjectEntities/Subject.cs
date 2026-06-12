using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.SubjectEntities
{
    [Table("Subjects")]
    public class Subject
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        public virtual IEnumerable<SubjectsTag> Tags { get; set; }
        
        public virtual IEnumerable<TagSubjectRelation> TagsRelations { get; set; }
    }
}