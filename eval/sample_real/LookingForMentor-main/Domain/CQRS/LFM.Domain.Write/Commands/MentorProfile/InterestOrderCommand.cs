namespace LFM.Domain.Write.Commands.MentorProfile
{
    public class InterestOrderCommand : BaseCommand
    {
        public int OrderId { get; set; }

        public int MentorId { get; set; }
    }
}