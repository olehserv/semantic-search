using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using LFM.Core.Common.Attributes;

namespace Lfm.Web.Mvc.Models.FormModels.UserCabinet.Mentor
{
    public class AddMentorsSubjectFormModel
    {
        [Range(50, 500)]
        public int CostPerHour { get; set; }

        [Range(1, int.MaxValue)]
        public int SubjectId { get; set; }

        [Required]
        [MaxLength(500)]
        [MinLength(100)]
        public string Description { get; set; }

        [NotEmptyList(ErrorMessage = "No tags selected.")]
        public List<int> TagIds { get; set; }
    }
}