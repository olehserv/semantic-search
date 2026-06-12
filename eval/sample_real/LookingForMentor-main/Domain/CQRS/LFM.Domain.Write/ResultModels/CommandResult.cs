namespace LFM.Domain.Write.ResultModels
{
    public class CommandResult
    {
        public bool IsSuccess { get; }
        
        public CommandResult(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }
        
        public CommandResult() : this(true)
        {
        }
    }
}