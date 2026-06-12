using System;

namespace Lfm.Web.Mvc.App.UIRenderers.Models
{
    public class CheckBox<T>
    {
        public string Name { get; set; }

        public T Value { get; }
        
        public string Checked { get; }

        public CheckBox(T value, Func<T, bool> isCheckedFunc)
        {
            Value = value;
            if (isCheckedFunc.Invoke(Value))
            {
                Checked = "checked";
            }
        }
    }
}