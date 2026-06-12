using System.Collections.Generic;
using System.Threading.Tasks;
using LFM.Domain.Write.ToDo;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.Domain.Write.PrettyCommandConverter
{
    internal interface IPrettyCommandService
    {
        Task<ICollection<CommandField>> GetPrettyCommand<TCommand>(TCommand command) 
            where TCommand : NeedsApproveCommand;
    }
    
    internal class PrettyCommandService : IPrettyCommandService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public PrettyCommandService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }
        
        public async Task<ICollection<CommandField>> GetPrettyCommand<TCommand>(TCommand command) 
            where TCommand : NeedsApproveCommand
        {
            return await RetrieveConvertor<TCommand>().GetPrettyCommand(command);
        }

        private IPrettyCommandConverter<TCommand> RetrieveConvertor<TCommand>() 
            where TCommand : NeedsApproveCommand
        {
            var scope = _serviceScopeFactory.CreateScope();
            var commandConvertor = scope.ServiceProvider
                .GetService<IPrettyCommandConverter<TCommand>>();
            
            return commandConvertor;
        }
    }
}