using System.ComponentModel.DataAnnotations;
using LFM.Core.Common.Data;

namespace Lfm.Admin.Blazor.App.Models
{
    public class CreateManagerFormModel
    {
        [Required(ErrorMessage = Messages.RequiredField)]
        [EmailAddress(ErrorMessage = Messages.EmailIncorrect)]
        [Display(Name = "Електронна пошта")]
        public string Email { get; set; }

        [Required(ErrorMessage = Messages.RequiredField)]
        [MaxLength(50, ErrorMessage = Messages.MaxLenght)]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }

        [Required(ErrorMessage = Messages.RequiredField)]
        [Phone(ErrorMessage = Messages.PhoneIncorrect)]
        [Display(Name = "Номер телефону")]
        public string PhoneNumber { get; set; }
    }
}