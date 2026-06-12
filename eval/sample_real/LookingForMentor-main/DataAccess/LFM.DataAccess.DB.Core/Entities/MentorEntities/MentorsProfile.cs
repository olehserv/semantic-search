using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LFM.DataAccess.DB.Core.Types;

namespace LFM.DataAccess.DB.Core.Entities.MentorEntities
{
    [Table("MentorsProfiles")]
    public class MentorsProfile
    {
        public bool IsVerified { get; set; }

        public int MentorId { get; set; }
        
        public virtual LfmUser Mentor { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string  Surname { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string  MiddleName { get; set; }

        public int? ProfileImageId { get; set; }
        public virtual MentorsProfileImage ProfileImage { get; set; }

        [Required]
        [MaxLength(250)]
        public string AboutMe { get; set; }

        public virtual List<MentorsSubjectInfo> SubjectsInfo { get; set; }
        
        
        public int? TownId { get; set; }

        public virtual Town Town { get; set; }

        public StudyingPlaces? StudyingPlace { get; set; }
        
        [Required]
        [MaxLength(250)]
        public string Education { get; set; }
        
        public bool WantReceivePersonalOrders { get; set; } = true;
    }
}