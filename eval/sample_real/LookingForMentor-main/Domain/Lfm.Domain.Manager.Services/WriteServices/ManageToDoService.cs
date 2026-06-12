using System;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Administration.Common.Models;
using LFM.Domain.Write.ToDo;
using LFM.Domain.Write.ToDo.Handler;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Domain.Manager.Services.WriteServices
{
    public interface IManageToDoService
    {
        Task<BaseResponseModel> StartReviewing(int toDoId, int userId);

        Task ApproveTodo(int toDoId, int approverId);

        Task RejectToDo(int toDoId, int rejectorId, string reason);
    }

    internal class ManageToDoService : IManageToDoService
    {
        private readonly LfmDbContext _context;
        private readonly IToDoHandler _toDoHandler;
        
        public ManageToDoService(
            LfmDbContext context, 
            IToDoHandler toDoHandler)
        {
            _context = context;
            _toDoHandler = toDoHandler;
        }

        public async Task<BaseResponseModel> StartReviewing(int toDoId, int userId)
        {
            var model = await _context.ToDos
                .Where(t => !t.CheckerId.HasValue)
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Pending)
                .FirstOrDefaultAsync(t => t.Id == toDoId);

            if (model == default)
                return BaseResponseModel.Failure;

            model.CheckerId = userId;
            model.StatusId = (int) ToDoStatusEnum.OnReview;
            model.ModifiedDateTime = DateTime.Now;

            await _context.SaveChangesAsync();
            return BaseResponseModel.Success;
        }
        
        public async Task ApproveTodo(int toDoId, int approverId)
        {
            var model = await _context.ToDos
                .Include(t => t.Operation)
                .Where(t => t.CheckerId == approverId)
                .Where(t => t.StatusId == (int) ToDoStatusEnum.OnReview)
                .FirstOrDefaultAsync(t => t.Id == toDoId);

            if (model == default)
                throw new LfmException(Messages.DataNotFound, "Запит");

            model.StatusId = (int) ToDoStatusEnum.Approved;
            model.ModifiedDateTime = DateTime.Now;

            await _context.SaveChangesAsync();

            await _toDoHandler.Handle(new ToDo(model.JsonCommand, model.Operation.Code));
        }
        
        public async Task RejectToDo(int toDoId, int rejectorId, string reason)
        {
            var model = await _context.ToDos
                .Where(t => t.CheckerId == rejectorId)
                .Where(t => t.StatusId == (int) ToDoStatusEnum.OnReview)
                .FirstOrDefaultAsync(t => t.Id == toDoId);

            if (model == default)
                throw new LfmException(Messages.DataNotFound, "Запит");
            
            model.StatusId = (int) ToDoStatusEnum.Rejected;
            model.RejectReason = reason;
            model.ModifiedDateTime = DateTime.Now;
            
            await _context.SaveChangesAsync();
        }
    }
}