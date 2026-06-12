using System;
using System.Linq;
using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities.ToDoEntities;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Write.CommandValidator;
using LFM.Domain.Write.PrettyCommandConverter;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace LFM.Domain.Write.ToDo.CreateService
{
    internal sealed class CreateToDoService : ICreateToDoService
    {
        private readonly LfmDbContext _context;
        private readonly IValidateCommandService _validateCommandService;
        private readonly IPrettyCommandService _prettyCommandService;
        
        public CreateToDoService(
            LfmDbContext context, 
            IValidateCommandService validateCommandService, 
            IPrettyCommandService prettyCommandService)
        {
            _context = context;
            _validateCommandService = validateCommandService;
            _prettyCommandService = prettyCommandService;
        }

        public async Task CreateToDo<TCommand>(TCommand command, int requestedUserId)
            where TCommand : NeedsApproveCommand
        {
            await _validateCommandService.Validate(command);
            
            var existedToDo = await _context.ToDos
                .Where(t => t.CreatedByUserId == requestedUserId)
                .Where(t => t.OperationCodeId == command.Operation.Id)
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Pending)
                .Where(t => t.OperationUniqueKey == command.OperationUniqueKey)
                .FirstOrDefaultAsync();

            var jsonCommand = JsonConvert.SerializeObject(command);

            var prettyCommand = await _prettyCommandService.GetPrettyCommand(command);
            var commandBody = JsonConvert.SerializeObject(prettyCommand);
            
            if (existedToDo != null)
            {
                existedToDo.JsonCommand = jsonCommand;
                existedToDo.PrettyCommand = commandBody;
                await _context.SaveChangesAsync();
                return;
            }

            ToDoEntity toDo = new ToDoEntity
            {
                StatusId = (int)ToDoStatusEnum.Pending,
                JsonCommand = jsonCommand,
                PrettyCommand = commandBody,
                OperationCodeId = command.Operation.Id,
                CreatedByUserId = requestedUserId,
                CreatedDateTime = DateTime.Now,
                ModifiedDateTime = DateTime.Now
            };

            _context.ToDos.Add(toDo);
            await _context.SaveChangesAsync();
        }
    }
}