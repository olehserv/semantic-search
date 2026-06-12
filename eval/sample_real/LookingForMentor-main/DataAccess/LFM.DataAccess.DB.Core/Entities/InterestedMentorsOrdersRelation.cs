using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities
{
    [Table("InterestedMentorsOrdersRelations")]

    public class InterestedMentorsOrdersRelation
    {
        public int OrderRequestId { get; set; }
        
        public virtual OrderRequest OrderRequest { get; set; }
        
        public int MentorId { get; set; }

        public virtual LfmUser Mentor { get; set; }
    }
}