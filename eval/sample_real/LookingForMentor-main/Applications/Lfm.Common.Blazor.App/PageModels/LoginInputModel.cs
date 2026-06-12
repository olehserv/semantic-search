using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Lfm.Common.Blazor.App.PageModels
{
    public class LoginInputModel
    {
        [Required]
        [EmailAddress]
        [DisplayName("Електронна пошта")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [DisplayName("Пароль")]
        public string Password { get; set; }

        [Display(Name = "Запам'ятати мене?")]
        public bool RememberMe { get; set; }
    }
}