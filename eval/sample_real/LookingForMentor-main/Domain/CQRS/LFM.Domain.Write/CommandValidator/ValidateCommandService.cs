using System.Threading.Tasks;
using LFM.Domain.Write.Declarations;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.Domain.Write.CommandValidator
{
    internal interface IValidateCommandService
    {
        Task Validate<TCommand>(TCommand command) where TCommand : ICommand;
    }

    internal class ValidateCommandService : IValidateCommandService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ValidateCommandService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Validate<TCommand>(TCommand command) 
            where TCommand : ICommand
        {
            await RetrieveCommandValidator<TCommand>().IsValid(command);
        }

        private ICommandValidator<TCommand> RetrieveCommandValidator<TCommand>() 
            where TCommand : ICommand
        {
            var scope = _serviceScopeFactory.CreateScope();
            var commandValidator = scope.ServiceProvider
                .GetService<ICommandValidator<TCommand>>();
            
            return commandValidator;
        }
    }
}