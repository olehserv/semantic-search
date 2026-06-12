using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.SubjectEntities
{
    [Table("TagSubjectRelations")]
    public class TagSubjectRelation
    {
        public int SubjectId { get; set; }
        
        public virtual Subject Subject { get; set; }
        
        public int TagId { get; set; }

        public virtual SubjectsTag Tag { get; set; }
    }
}