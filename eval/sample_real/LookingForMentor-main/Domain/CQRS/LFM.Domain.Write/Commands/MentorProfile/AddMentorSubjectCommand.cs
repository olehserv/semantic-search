using System.Collections.Generic;
using LFM.Domain.Write.ToDo;

namespace LFM.Domain.Write.Commands.MentorProfile
{
    public class AddMentorSubjectCommand : NeedsApproveCommand
    {
        public int MentorId { get; set; }
        
        public int CostPerHour { get; set; }
        
        public string Description { get; set; }

        public int SubjectId { get; set; }
            
        public List<int> TagIds { get; set; }
        
        public override ToDoOperationsEnum Operation => ToDoOperationsEnum.AddMentorSubject;
        public override string OperationUniqueKey => SubjectId.ToString();
    }
}