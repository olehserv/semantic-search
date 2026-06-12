using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.Administration
{
    [Table("ManagerActivityLogs")]
    public class ManagerActivityLog
    {
        public int Id { get; set; }

        public int ManagerId { get; set; }

        public int LogTypeId { get; set; }

        public DateTime LogDateTime { get; set; }

        public virtual LfmUser Manager { get; set; }

        public virtual ManagerActivityLogType LogType { get; set; }
    }
}