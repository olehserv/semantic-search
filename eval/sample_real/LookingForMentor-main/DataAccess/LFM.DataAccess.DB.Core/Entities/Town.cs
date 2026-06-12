using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities
{
    [Table("UkrainianTowns")]
    public class Town
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
    }
}