using System.Collections.Generic;
using System.Threading.Tasks;
using LFM.Domain.Write.ToDo;

namespace LFM.Domain.Write.PrettyCommandConverter
{
    internal interface IPrettyCommandConverter<in TCommand> 
        where TCommand : NeedsApproveCommand
    {
        Task<ICollection<CommandField>> GetPrettyCommand(TCommand command);
    }
}