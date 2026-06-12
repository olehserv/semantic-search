using System.ComponentModel.DataAnnotations.Schema;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;

namespace LFM.DataAccess.DB.Core.Entities.MentorEntities
{
    [Table("MentorsSubjectsTags")]
    public class MentorsSubjectTag
    {
        public int MentorsSubjectInfoId { get; set; }

        public virtual MentorsSubjectInfo MentorsSubjectInfo { get; set; }
        
        
        public int TagId { get; set; }

        public virtual SubjectsTag Tag { get; set; }
    }
}