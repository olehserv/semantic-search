using LFM.DataAccess.DB.Core.Types;
using System.ComponentModel.DataAnnotations;
using LFM.Core.Common.Data;

namespace Lfm.Web.Mvc.Models.FormModels.Mentor
{
    public class ContactMentorFormModel
    {
        public int MentorId { get; set; }

        [Required(ErrorMessage = Messages.InvalidRequest)]
        public LessonInfo Lesson { get; set; }

        [Required(ErrorMessage = Messages.InvalidRequest)]
        public UserContactInfo UserContact { get; set; }

        [Required(ErrorMessage = Messages.InvalidRequest)]
        public AdditionalInfo Additional { get; set; }
        
        public class LessonInfo
        {
            public int SubjectId { get; set; }

            [Required(ErrorMessage = "'Training direction' field is required.")]
            public int TagId { get; set; }

            [Required(ErrorMessage = "'Studying place' field is required.")]
            public StudyingPlaces StudyingPlace { get; set; }

            [Required(ErrorMessage = "'Amount of lessons per week' field is required.")]
            [Range(1, 7, ErrorMessage = "Amount of lessons per week cannot be more than 7 and less than 1.")]
            public int AmountOfLessonsPerWeek { get; set; } = 1;
            
            [Required(ErrorMessage = "'Lesson duration' field is required.")]
            public LessonDuration LessonDuration { get; set; }
        }

        public class UserContactInfo
        {
            [Required(ErrorMessage = "'Name' field is required.")]
            [MaxLength(20)]
            public string Name { get; set; }

            [Required(ErrorMessage = "'Phone number' field is required.")]
            [Phone]
            public string PhoneNumber { get; set; }
            
            [Required(ErrorMessage = "'Email' field is required.")]
            [EmailAddress]
            public string Email { get; set; }
        }

        public class AdditionalInfo
        {
            [MaxLength(255)]
            public string WhenToPractice { get; set; }

            [MaxLength(255)]
            public string WhichHelp { get; set; }

            [MaxLength(255)]
            public string AdditionalWishes { get; set; }
        }
    }
}