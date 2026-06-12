using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LFM.DataAccess.DB.Core.Entities.MentorEntities
{
    [Table("MentorsProfileImages")]
    public class MentorsProfileImage
    {
        public int Id { get; set; }

        /// <summary>
        /// Base64 string
        /// </summary>
        [Required]
        [MaxLength(50_000)]
        public string Image { get; set; }
    }
}