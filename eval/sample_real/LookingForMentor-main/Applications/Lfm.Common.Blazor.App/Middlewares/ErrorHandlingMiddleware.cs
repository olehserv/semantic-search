using System;
using System.Threading.Tasks;
using Lfm.Core.Common.Web.Data;
using Microsoft.AspNetCore.Http;

namespace Lfm.Common.Blazor.App.Middlewares
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
            catch (Exception exc)
            {
                context.Response.Redirect(Constants.DefaultUrl, false);
            }
        }
    }
}