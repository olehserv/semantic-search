using System.Threading.Tasks;
using Lfm.Core.Common.Web.Models;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.Mentor;
using Lfm.Domain.ReadModels.SearchModels;

namespace LFM.Domain.Read.Providers
{
    public interface IMentorsProvider
    {
        Task<PageList<MentorPreviewModel>> LookingForMentors(MentorsSearchModel searchModel, int? pageNumber);
        
        Task<MentorDetailedPreviewModel> GetMentorInfo(int mentorId);

        Task<ContactMentorInfo> GetContactMentorInfo(int mentorId, int subjectId);
    }
}