using System.Threading.Tasks;

namespace LFM.Domain.Write.ToDo.Handler
{
    public interface IToDoHandler
    {
        Task Handle(ToDo toDo);
    }
}