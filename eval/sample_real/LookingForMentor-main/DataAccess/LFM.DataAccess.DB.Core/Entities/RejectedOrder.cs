using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;
using LFM.DataAccess.DB.Core.Types;

namespace LFM.DataAccess.DB.Core.Entities
{
    [Table("RejectedOrders")]
    public class RejectedOrder
    {
        public int Id { get; set; }
        
        public int MentorId { get; set; }
        
        public int StudentId { get; set; }

        public int SubjectId { get; set; }

        public int TagId { get; set; }

        public StudyingPlaces StudyingPlace { get; set; }

        public int AmountOfLessonsPerWeek { get; set; }
            
        public LessonDuration LessonDuration { get; set; }

        [Range(50, 500)]
        public int? CostFrom { get; set; }
        
        [Range(50, 500)]
        public int? CostTo { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string StudentName { get; set; }

        [Required]
        [Phone]
        public string StudentPhoneNumber { get; set; }
            
        [Required]
        [EmailAddress]
        public string StudentEmail { get; set; }
        
        [MaxLength(255)]
        public string WhenToPractice { get; set; }

        [MaxLength(255)]
        public string WhichHelp { get; set; }

        [MaxLength(255)]
        public string AdditionalWishes { get; set; }

        public DateTime RejectedDateTime { get; set; }
        
        [Required]
        public string RejectReason { get; set; }
        
        
        public virtual LfmUser Mentor { get; set; }
        
        public virtual LfmUser Student { get; set; }

        public virtual Subject Subject { get; set; }
        
        public virtual SubjectsTag SubjectsTag { get; set; }
    }
}