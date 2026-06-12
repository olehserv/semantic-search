using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.ToDoEntities
{
    [Table("ToDoStatuses")]
    public class ToDoStatus
    {
        public int Id { get; set; }

        public string StatusName { get; set; }

        public virtual ICollection<ToDoEntity> ToDos { get; set; }
    }
}