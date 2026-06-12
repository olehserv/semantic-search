using System.Collections.Generic;
using Lfm.Core.Common.Web.Models;
using Lfm.Domain.ReadModels.ReviewModels.Mentor;
using Lfm.Domain.ReadModels.SearchModels;

namespace Lfm.Web.Mvc.Models
{
    public class MentorsListPageModel : AbstractSearchingListPageModel<MentorPreviewModel, MentorsSearchModel>
    {
        public MentorsListPageModel(PageList<MentorPreviewModel> items, MentorsSearchModel searchModel, int pageSize) 
            : base(items, searchModel, pageSize)
        {
        }

        public override Dictionary<string, string> GetAllRouteData()
        {
            return new Dictionary<string, string>
            {
                { nameof(MentorsSearchModel.SubjectId), SearchModel.SubjectId.ToString() },
                { nameof(MentorsSearchModel.StudyingPlace), SearchModel.StudyingPlace.ToString() },
                { nameof(MentorsSearchModel.TownId), SearchModel.TownId.ToString() }
            };
        }
    }
}