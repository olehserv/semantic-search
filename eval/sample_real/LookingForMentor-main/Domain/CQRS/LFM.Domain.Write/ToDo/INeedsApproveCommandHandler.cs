using System.Threading.Tasks;

namespace LFM.Domain.Write.ToDo
{
    internal interface INeedsApproveCommandHandler
    {
        ToDoOperationsEnum Operation { get; }

        Task HandleToDo(string jsonCommand);
    }
}