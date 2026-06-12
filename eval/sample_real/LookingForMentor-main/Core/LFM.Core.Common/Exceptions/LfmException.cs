using System;

namespace LFM.Core.Common.Exceptions
{
    public class LfmException : Exception
    {
        public LfmException(string message) : base(message)
        {
            
        }
        
        public LfmException(string message, params object[] parameters) 
            : base(string.Format(message, parameters))
        {
            
        }
    }
}