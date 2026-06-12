using System.Collections.Generic;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.Domain.Write.CommandValidator;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.PrettyCommandConverter;
using LFM.Domain.Write.ResultModels;
using Newtonsoft.Json;

namespace LFM.Domain.Write.ToDo
{
    internal abstract class BaseNeedsApproveCommandHandler<TCommand, TCommandResult> 
        : ICommandHandler<TCommand, TCommandResult>, 
            ICommandValidator<TCommand>, 
            INeedsApproveCommandHandler,
            IPrettyCommandConverter<TCommand>
        where TCommand : NeedsApproveCommand, new()
        where TCommandResult : CommandResult
    {
        public abstract ToDoOperationsEnum Operation { get; }

        public abstract Task<TCommandResult> ExecuteAsync(TCommand command);

        public Task HandleToDo(string jsonCommand) => ExecuteAsync(GetCommand(jsonCommand));

        private TCommand GetCommand(string jsonCommand)
        {
            try
            {
                return JsonConvert.DeserializeObject<TCommand>(jsonCommand);
            }
            catch
            {
                throw new LfmException(Messages.SystemError);
            }
        }

        public abstract Task IsValid(TCommand command);

        public abstract Task<ICollection<CommandField>> GetPrettyCommand(TCommand command);
    }
}