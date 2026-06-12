using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.Domain.Write.ToDo.Handler
{
    internal sealed class ToDoHandler : IToDoHandler
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ToDoHandler(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Handle(ToDo toDo)
        {
            await FindHandler(toDo.OperationCode).HandleToDo(toDo.JsonCommand);
        }

        private INeedsApproveCommandHandler FindHandler(string code)
        {
            var scope = _serviceScopeFactory.CreateScope();
            var handler = scope.ServiceProvider
                .GetServices<INeedsApproveCommandHandler>()
                .FirstOrDefault(h => h.Operation.Code == code);
            
            return handler ?? throw new LfmException(Messages.SystemError);
        }
    }
}