namespace LFM.Domain.Write.Commands.MentorProfile
{
    public class ApprovePersonalOrderCommand : BaseCommand
    {
        public int MentorId { get; set; }

        public int OrderRequestId { get; set; }
    }
}