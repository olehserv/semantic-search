using System.ComponentModel.DataAnnotations;
using Lfm.Core.Common.Web.Models;

namespace Lfm.Web.Mvc.Models.FormModels.Auth
{
    public sealed class LoginFormModel : ReturnUrlModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Електронна пошта")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }

        [Display(Name = "Запам'ятати мене?")]
        public bool RememberMe { get; set; } = true;
    }
}