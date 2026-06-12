namespace LFM.Domain.Write.Commands.MentorProfile
{
    public class RejectPersonalOrderCommand : BaseCommand
    {
        public int MentorId { get; set; }

        public int OrderRequestId { get; set; }

        public string Reason { get; set; }
    }
}