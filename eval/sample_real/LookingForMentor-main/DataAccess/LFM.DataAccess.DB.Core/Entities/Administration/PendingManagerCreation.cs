using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.Administration
{
    [Table("PendingManagerCreations")]
    public class PendingManagerCreation
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string Name { get; set; }

        public string PhoneNumber { get; set; }

        public string CreationStamp { get; set; }

        public bool IsActivated { get; set; } = false;
    }
}