using System;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using Lfm.Core.Common.Web.SessionAlerts;
using Microsoft.AspNetCore.Mvc;

namespace Lfm.Web.Mvc.App.Extensions
{
    public static class ControllerExtensions
    {
        public static async Task<IActionResult> HandleAction(this Controller controller, 
            Func<Task<IActionResult>> actionFunc, IActionResult defaultResult)
        {
            if (controller.ModelState.IsValid)
            {
                try
                {
                    return await actionFunc.Invoke();
                }
                catch (LfmException exc)
                {
                    controller.AlertError(exc.Message);
                }
                catch
                {
                    controller.AlertError(Messages.SystemError);
                }
            }

            return defaultResult;
        }
    }
}