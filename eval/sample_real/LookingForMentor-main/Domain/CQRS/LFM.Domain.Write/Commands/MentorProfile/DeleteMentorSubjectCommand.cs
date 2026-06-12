namespace LFM.Domain.Write.Commands.MentorProfile
{
    public class DeleteMentorSubjectCommand : BaseCommand
    {
        public int MentorId { get; set; }
        
        public int SubjectId { get; set; }
    }
}