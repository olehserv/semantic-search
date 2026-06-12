using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.Administration
{
    [Table("ManagerActivityLogTypes")]
    public class ManagerActivityLogType
    {
        public int Id { get; set; }

        public string Code { get; set; }

        public string Description { get; set; }

        public virtual ICollection<ManagerActivityLog> Logs { get; set; }
    } 
}