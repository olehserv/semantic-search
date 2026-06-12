using System;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using Lfm.Core.Common.Web.SessionAlerts;
using Lfm.Domain.Common.Extensions;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.ResultModels;
using LFM.Domain.Write.ToDo;
using LFM.Domain.Write.ToDo.CreateService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LFM.Domain.Write.Mediator
{
    internal sealed class Mediator : IMediator
    {
        private readonly IServiceProvider _serviceProvider;

        public Mediator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<TCommandResult> ExecuteCommand<TCommand, TCommandResult>(TCommand command) 
            where TCommand : ICommand, new()
            where TCommandResult : CommandResult, new()
        {
            ICommandHandler<TCommand, TCommandResult> handler = this.RetrieveCommandHandler<TCommand, TCommandResult>();
            return await handler.ExecuteAsync(command);
        }
        
        public async Task<TCommandResult> ExecuteNeedsApproveCommand<TCommand, TCommandResult>(TCommand command) 
            where TCommand : NeedsApproveCommand, new()
            where TCommandResult : CommandResult, new()
        {
            var createToDoService = _serviceProvider.GetRequiredService<ICreateToDoService>();
            var context = _serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            
            await createToDoService.CreateToDo(command, context.User.GetId());
            context.Alert(Messages.ToDoCreated, AlertTypes.Info);
            return new TCommandResult();
        }

        public async Task<CommandResult> ExecuteCommand<TCommand>(TCommand command) where TCommand : ICommand, new()
        {
            return await ExecuteCommand<TCommand, CommandResult>(command);
        }

        private ICommandHandler<TCommand, TCommandResult> RetrieveCommandHandler<TCommand, TCommandResult>()
            where TCommand : ICommand, new()
            where TCommandResult : CommandResult
        {
            ICommandHandler<TCommand, TCommandResult> handler = _serviceProvider.GetService<ICommandHandler<TCommand, TCommandResult>>();
            
            if (handler == null) 
                throw new Exception($"Command handler for {nameof(TCommand)} not found");

            return handler;
        }
    }
}