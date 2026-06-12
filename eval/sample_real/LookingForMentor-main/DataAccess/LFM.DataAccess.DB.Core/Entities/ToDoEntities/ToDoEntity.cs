using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.ToDoEntities
{
    [Table("ToDos")]
    public class ToDoEntity
    {
        public int Id { get; set; }

        public int? CheckerId { get; set; }
        
        public int StatusId { get; set; }

        public string RejectReason { get; set; }

        public string JsonCommand { get; set; }
        
        public string PrettyCommand { get; set; }

        public int OperationCodeId { get; set; }
        
        public string OperationUniqueKey { get; set; }

        public int CreatedByUserId { get; set; }
        
        public DateTime CreatedDateTime { get; set; }

        public DateTime ModifiedDateTime { get; set; }

        public virtual ToDoOperation Operation { get; set; }

        public virtual ToDoStatus Status { get; set; }
        
        public virtual LfmUser Checker { get; set; }
        
        public virtual LfmUser CreatedByUser { get; set; }
    }
}