namespace LFM.Domain.Write.ResultModels
{
    public class ApproveMentorProposeResult : IdCommandResult
    {
        public ApproveMentorProposeResult() : base()
        {
        }
        
        public ApproveMentorProposeResult(bool isSuccess) : base(isSuccess)
        {
        }

        public string MentorName { get; set; }
    }
}