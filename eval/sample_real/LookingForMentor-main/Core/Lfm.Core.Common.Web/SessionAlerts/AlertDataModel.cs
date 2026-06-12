namespace Lfm.Core.Common.Web.SessionAlerts
{
    public sealed class AlertDataModel
    {
        public readonly string Message;

        public readonly AlertTypes Type;
        
        public AlertDataModel(string message, AlertTypes type)
        {
            Message = message;
            Type = type;
        }
    }
}