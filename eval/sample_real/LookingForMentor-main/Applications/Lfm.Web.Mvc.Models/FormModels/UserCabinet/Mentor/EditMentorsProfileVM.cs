using System.ComponentModel.DataAnnotations;
using LFM.DataAccess.DB.Core.Types;
using Microsoft.AspNetCore.Http;

namespace Lfm.Web.Mvc.Models.FormModels.UserCabinet.Mentor
{
    public class EditMentorsProfileFormModel
    {
        [Required]
        [MaxLength(20)]
        public string Name { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string  Surname { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string  MiddleName { get; set; }
        
        public IFormFile ProfileImageFormFile { get; set; }

        [Required]
        [MaxLength(250)]
        public string AboutMe { get; set; }
        
        [Required]
        [Display(Name = "Town")]
        public int? TownId { get; set; }

        [Required]
        public StudyingPlaces? StudyingPlace { get; set; }
        
        [Required]
        [MaxLength(250)]
        public string Education { get; set; }
        
        public bool WantReceivePersonalOrders { get; set; }
    }
}