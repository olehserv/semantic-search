using Lfm.Core.Common.Web.SessionAlerts;

namespace Lfm.Web.Mvc.App.UIRenderers
{
    public static class AlertUiRenderer
    {
        public static string GetAlertCssColorClassName(AlertDataModel alert)
        {
            return alert.Type switch
            {
                AlertTypes.Success => "success",
                AlertTypes.Error => "danger",
                AlertTypes.Warning => "warning",
                AlertTypes.Info => "info",
                _ => string.Empty
            };
        }
        
        public static string RetrieveAlertName(AlertDataModel alert)
        {
            return alert.Type switch
            {
                AlertTypes.Success => "Успіх",
                AlertTypes.Error => "Помилка",
                AlertTypes.Warning => "Попередження",
                AlertTypes.Info => "Інформація",
                _ => string.Empty
            };
        }
    }
}