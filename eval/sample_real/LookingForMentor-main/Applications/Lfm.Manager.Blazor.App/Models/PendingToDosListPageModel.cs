using System.Collections.Generic;
using Lfm.Core.Common.Web.Models;
using Lfm.Domain.Manager.Models.ReviewModels;
using Lfm.Domain.Manager.Models.SearchModel;

namespace Lfm.Manager.Blazor.App.Models
{
    public class PendingToDosListPageModel : AbstractSearchingListPageModel<ToDoReviewModel, SearchToDosModel>
    {
        public PendingToDosListPageModel(PageList<ToDoReviewModel> items, SearchToDosModel searchModel, int pageSize) 
            : base(items, searchModel, pageSize)
        {
        }

        public override Dictionary<string, string> GetAllRouteData()
        {
            return new Dictionary<string, string>
            {
                { nameof(SearchToDosModel.OperationId), SearchModel.OperationId.ToString() },
                { nameof(SearchToDosModel.UserId), SearchModel.UserId.ToString() }
            };
        }
    }
}