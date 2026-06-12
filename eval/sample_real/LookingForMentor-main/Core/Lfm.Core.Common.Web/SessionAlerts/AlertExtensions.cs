using System.Collections.Generic;
using Lfm.Core.Common.Web.Data;
using Lfm.Core.Common.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;

namespace Lfm.Core.Common.Web.SessionAlerts
{
    public static class AlertExtensions
    {
        private const string AlertKey = "AlertsDataKey";
        
        private static void AddAlert(this HttpContext context, AlertDataModel alert)
        {
            List<AlertDataModel> sessionAlerts = context.Session.GetObject<List<AlertDataModel>>(AlertKey) ??
                                                 new List<AlertDataModel>();

            sessionAlerts.Add(alert);
            context.Session.SetObject(AlertKey, sessionAlerts);
        }

        public static void Alert(this HttpContext context, string message, AlertTypes type, params string[] messageParameters)
        {
            context.AddAlert(new AlertDataModel(string.Format(message, messageParameters), type));
        }

        public static void AlertError(this Controller controller, string message, params string[] messageParameters)
        {
            controller.HttpContext.AddAlert(new AlertDataModel(string.Format(message, messageParameters), AlertTypes.Error));
        }
        
        public static void AlertWarning(this Controller controller, string message, params string[] messageParameters)
        {
            controller.HttpContext.AddAlert(new AlertDataModel(string.Format(message, messageParameters), AlertTypes.Warning));
        }
        
        public static void AlertSuccess(this Controller controller, string message, params string[] messageParameters)
        {
            controller.HttpContext.AddAlert(new AlertDataModel(string.Format(message, messageParameters), AlertTypes.Success));
        }
        
        public static void AlertInfo(this Controller controller, string message, params string[] messageParameters)
        {
            controller.HttpContext.AddAlert(new AlertDataModel(string.Format(message, messageParameters), AlertTypes.Info));
        }

        public static List<AlertDataModel> RetrieveAlerts(this RazorPage page)
        {
            var alerts = page.Context.Session.GetObject<List<AlertDataModel>>(AlertKey);
            page.Context.Session.Remove(AlertKey);
            return alerts;
        }
        
        public static void AlertErrorAndRedirect(string errorMessage, HttpContext context, params string[] messageParameters)
        {
            context.Alert(errorMessage, AlertTypes.Error, messageParameters);
            context.Response.Redirect(Constants.DefaultUrl, false);
        }
    }
}