using System;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using Lfm.Core.Common.Web.SessionAlerts;
using Microsoft.AspNetCore.Http;

namespace Lfm.Web.Mvc.App.Middlewares
{
    internal class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (LfmException lfmError)
            {
                //log exception
                AlertExtensions.AlertErrorAndRedirect(lfmError.Message, context);
            }
            catch (Exception exc)
            {
                //log exception
                AlertExtensions.AlertErrorAndRedirect(Messages.SystemError, context);
            }
        }
    }
}