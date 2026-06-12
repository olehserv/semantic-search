using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ToDo;

namespace LFM.Domain.Write.Commands.MentorProfile
{
    public class EditMentorProfileCommand : NeedsApproveCommand
    {
        public int MentorId { get; set; }
        
        public string Name { get; set; }

        public string  Surname { get; set; }

        public string  MiddleName { get; set; }
        
        public byte[] ProfileImageBytes { get; set; }
        
        public string AboutMe { get; set; }
        
        
        public int TownId { get; set; }

        public StudyingPlaces? StudyingPlace { get; set; }
        
        public string Education { get; set; }
        
        public bool WantReceivePersonalOrders { get; set; }

        public override ToDoOperationsEnum Operation => ToDoOperationsEnum.EditMentorProfile;
        public override string OperationUniqueKey => MentorId.ToString();
    }
}