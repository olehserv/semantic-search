using System;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace LFM.Core.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class NotEmptyListAttribute : ValidationAttribute
    {
        private const string DefaultError = "'{0}' must have at least one element.";
        public NotEmptyListAttribute ( ) 
            : base(DefaultError) 
        {
        }

        public override bool IsValid (object value)
        {
            return (value is IList list && list.Count > 0);
        }

        public override string FormatErrorMessage ( string name )
        {
            return String.Format(this.ErrorMessageString, name);
        }
    }
}