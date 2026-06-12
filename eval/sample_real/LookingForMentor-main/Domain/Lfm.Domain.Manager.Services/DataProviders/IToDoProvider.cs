using System.Collections.Generic;
using System.Threading.Tasks;
using Lfm.Core.Common.Web.Models;
using Lfm.Domain.Manager.Models.ReviewModels;
using Lfm.Domain.Manager.Models.SearchModel;

namespace Lfm.Domain.Manager.Services.DataProviders
{
    public interface IToDoProvider
    {
        Task<PageList<ToDoReviewModel>> SearchPendingToDos(SearchToDosModel searchModel, int pageNo,
            int? pageSize = null);
        
        Task<PageList<RejectedToDoReviewModel>> GetRejectedToDos(int pageNo, int? pageSize = null);

        Task<PageList<ToDoReviewModel>> GetReviewingToDos(int pageNo, int reviewerId, int? pageSize = null);
        
        Task<PageList<ToDoReviewModel>> GetApprovedToDos(int pageNo, int approverId, int? pageSize = null);


        Task<ToDoDetailedReviewModel> GetDetailedPendingToDo(int toDoId);

        Task<ToDoDetailedReviewModel> GetDetailedReviewingToDo(int toDoId, int reviewerId);

        Task<ICollection<OperationReviewModel>> GetPerformingOperations();

        Task<ICollection<ToDoUserReviewModel>> GetPerformingUsers();
    }
}