using System.Collections.Generic;

namespace Lfm.Web.Mvc.App.UIRenderers.Models
{
    public class LinkItem
    {
        public string Name { get; set; }

        public string ControllerName { get; set; }
        
        public string ActionName { get; set; }
        

        public Dictionary<string, string> AllRouteData { get; set; }
    }
}