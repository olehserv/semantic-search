using Lfm.Core.Common.Web.Data;
using Lfm.Core.Common.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lfm.Core.Common.Web.Extensions
{
    public static class ActionExtensions
    {
        /// <summary>
        /// Redirects to Constants.Default url if returnUrlModel is null, else -- redirects to returnUrlModel.ReturnUrl
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="returnUrlModel"></param>
        /// <returns></returns>
        public static IActionResult Redirect(this Controller controller, ReturnUrlModel returnUrlModel)
        {
            if (returnUrlModel?.ReturnUrl != null)
            {
                return controller.LocalRedirect(returnUrlModel.ReturnUrl);
            }
            return controller.LocalRedirect(Constants.DefaultUrl);
        }
    }
}