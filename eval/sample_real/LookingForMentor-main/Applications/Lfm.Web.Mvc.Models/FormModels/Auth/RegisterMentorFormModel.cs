using System.ComponentModel.DataAnnotations;

namespace Lfm.Web.Mvc.Models.FormModels.Auth
{
    public sealed class RegisterMentorFormModel
    {
        [Required]
        [Display(Name = "Ім'я")]
        public string Name { get; set; }
        
        [Required]
        [EmailAddress]
        [Display(Name = "Електронна пошта")]
        public string Email { get; set; }
        
        [Required]
        [Phone]
        [Display(Name = "Номер телефону")]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "{0} повинен мати довжину від {2} до {1} символів.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Підтвердження паролю")]
        [Compare("Password", ErrorMessage = "Паролі не співпадають.")]
        public string ConfirmPassword { get; set; }
    }
}