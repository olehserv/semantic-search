using System.Threading.Tasks;
using LFM.Domain.Write.Declarations;

namespace LFM.Domain.Write.CommandValidator
{
    internal interface ICommandValidator<in TCommand>
        where TCommand : ICommand
    {
        Task IsValid(TCommand command);
    }
}