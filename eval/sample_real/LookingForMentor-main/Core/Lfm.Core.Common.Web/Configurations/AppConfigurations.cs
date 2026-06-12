using Lfm.Core.Common.Web.Data;

namespace Lfm.Core.Common.Web.Configurations
{
    public class AppConfigurations
    {
        public uint UserSessionExpirationHours { get; set; } = Constants.DefaultUserSessionExpirationHours;

        public int SearchingMentorsPageSize { get; set; } = 10;
    }
}