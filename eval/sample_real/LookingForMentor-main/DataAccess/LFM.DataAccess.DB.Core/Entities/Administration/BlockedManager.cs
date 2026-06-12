using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.Administration
{
    [Table("BlockedManagers")]
    public class BlockedManager
    {
        public int Id { get; set; }

        public int ManagerId { get; set; }
        
        public virtual LfmUser Manager { get; set; }
    }
}