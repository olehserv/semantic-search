namespace LFM.Domain.Write.PrettyCommandConverter
{
    public class CommandField
    {
        public string Name { get; set; }

        public object Value { get; set; }

        public bool IsVisible { get; set; }

        public bool? IsImage { get; set; }

        public CommandField()
        {
            
        }

        public CommandField(
            string name, 
            object value)
        {
            Name = name;
            Value = value;
            IsVisible = value != default;
            IsImage = null;
        }
        
        public CommandField(
            string name, 
            object value,
            bool isVisible,
            bool? isImage = null)
        {
            Name = name;
            Value = value;
            IsVisible = isVisible;
            IsImage = isImage;
        }
    }
}