namespace Lfm.Web.Mvc.App.UIRenderers.Models
{
    public class SelectItem<T>
    {
        public T Value { get; set; }
        
        public string Name { get; set; }

        public string Selected { get; set; }
    }
}