using System.ComponentModel.DataAnnotations;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Web.Mvc.Models.FormModels.UserCabinet.Student
{
    public class CreateLookingForMentorRequestFormModel
    {
        [Range(1, int.MaxValue)]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "'Training direction' field is required.")]
        [Range(1, int.MaxValue)]
        public int TagId { get; set; }

        [Required(ErrorMessage = "'Studying place' field is required.")]
        [Range(1, 3)]
        public StudyingPlaces StudyingPlace { get; set; }

        [Required(ErrorMessage = "'Amount of lessons per week' field is required.")]
        [Range(1, 7, ErrorMessage = "Amount of lessons per week cannot be more than 7 and less than 1.")]
        public int AmountOfLessonsPerWeek { get; set; } = 1;
        
        [Required(ErrorMessage = "'Lesson duration' field is required.")]
        public LessonDuration LessonDuration { get; set; }

        [Required(ErrorMessage = "'Minimum cost' field is required.")]
        [Range(50, 500)]
        public int CostFrom { get; set; } = 50;

        [Required(ErrorMessage = "'Maximum cost' field is required.")]
        [Range(50, 500)]
        public int CostTo { get; set; } = 50;
    
        [MaxLength(255)]
        public string WhenToPractice { get; set; }

        [MaxLength(255)]
        public string WhichHelp { get; set; }

        [MaxLength(255)]
        public string AdditionalWishes { get; set; }
    }
}