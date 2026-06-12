using System.Linq;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using Lfm.Core.Common.Web.SessionAlerts;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Lfm.Web.Mvc.App.Attributes.Action
{
    /// <summary>
    /// Creates alerts if model state is not valid
    /// </summary>
    public class AlertModelStateErrorsAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                if (context.ModelState.ErrorCount > 0)
                {
                    var errors = context.ModelState.Values
                        .Where(v => v.Errors.Count > 0)
                        .Select(v => v.Errors)
                        .FirstOrDefault();

                    if (errors != null)
                    {
                        foreach (var e in errors)
                        {
                            context.HttpContext.Alert(e.ErrorMessage, AlertTypes.Error);
                        }
                    }
                }
                else
                {
                    context.HttpContext.Alert(Messages.SystemError, AlertTypes.Error);
                }
            }

            base.OnActionExecuting(context);
        }
    }
}