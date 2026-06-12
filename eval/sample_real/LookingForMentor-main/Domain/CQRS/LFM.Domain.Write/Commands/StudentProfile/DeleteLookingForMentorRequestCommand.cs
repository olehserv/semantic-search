namespace LFM.Domain.Write.Commands.StudentProfile
{
    public class DeleteLookingForMentorRequestCommand : BaseCommand
    {
        public int OrderId { get; set; }
        
        public int StudentId { get; set; }
    }
}