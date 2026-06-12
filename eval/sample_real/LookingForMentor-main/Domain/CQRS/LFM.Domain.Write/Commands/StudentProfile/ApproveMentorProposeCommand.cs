namespace LFM.Domain.Write.Commands.StudentProfile
{
    public class ApproveMentorProposeCommand : BaseCommand
    {
        public int OrderId { get; set; }

        public int MentorId { get; set; }

        public int StudentId { get; set; }
    }
}