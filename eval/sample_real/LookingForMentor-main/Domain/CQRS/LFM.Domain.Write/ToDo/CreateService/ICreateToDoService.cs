using System.Threading.Tasks;

namespace LFM.Domain.Write.ToDo.CreateService
{
    internal interface ICreateToDoService
    {
        Task CreateToDo<TCommand>(TCommand command, int requestedUserId)
            where TCommand : NeedsApproveCommand;
    }
}