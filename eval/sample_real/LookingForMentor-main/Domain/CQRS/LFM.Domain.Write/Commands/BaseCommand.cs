using LFM.Domain.Write.Declarations;

namespace LFM.Domain.Write.Commands
{
    public abstract class BaseCommand : ICommand
    {
        public bool NeedsApprove { get; } = false;
    }
}