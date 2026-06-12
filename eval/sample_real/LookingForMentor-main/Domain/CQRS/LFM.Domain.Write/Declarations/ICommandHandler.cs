using System.Threading.Tasks;
using LFM.Domain.Write.ResultModels;

namespace LFM.Domain.Write.Declarations
{
    internal interface ICommandHandler<TCommand, TCommandResult> 
        where TCommand : ICommand, new()
        where TCommandResult : CommandResult
    {
        Task<TCommandResult> ExecuteAsync(TCommand command);
    }
}