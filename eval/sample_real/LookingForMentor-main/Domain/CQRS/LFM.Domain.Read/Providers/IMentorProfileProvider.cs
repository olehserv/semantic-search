using System.Collections.Generic;
using System.Threading.Tasks;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.MentorProfile;

namespace LFM.Domain.Read.Providers
{
    public interface IMentorProfileProvider
    {
        Task<T> GetGeneralInfo<T>(int mentorId) where T : class;

        Task<IEnumerable<MentorSubjectReviewModel>> GetSubjectsInfo(int mentorId);

        Task<IEnumerable<CommonReviewModel>> GetAvailableSubjects(int mentorId);

        Task<MentorSubjectReviewModel> GetSubject(int mentorId, int subjectId);

        Task<bool> CanAddSubject(int mentorId, int subjectId);
        
        Task<byte[]> GetAvatar(int mentorId);

        Task<IEnumerable<MentorPersonalOrderReviewModel>> GetPersonalOrdersRequests(int mentorId);

        Task<MentorPersonalOrderDetailedReviewModel> GetPersonalOrderRequestDetails(int mentorId, int orderId);

        Task<IEnumerable<MentorsApprovedOrderMinReviewModel>> GetApprovedOrders(int mentorId);

        Task<MentorsApprovedOrderDetailedReviewModel> GetApprovedOrderDetails(int mentorId, int orderId);
        
        
        Task<IEnumerable<MentorPotentialOrderReviewModel>> GetPotentialOrders(int mentorId);

        Task<MentorPotentialOrderDetailsReviewModel> GetPotentialOrderDetails(int mentorId, int orderId);
    }
}