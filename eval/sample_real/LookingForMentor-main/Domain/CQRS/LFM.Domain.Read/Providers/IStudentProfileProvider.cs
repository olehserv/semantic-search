using System.Collections.Generic;
using System.Threading.Tasks;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.StudentProfile;

namespace LFM.Domain.Read.Providers
{
    public interface IStudentProfileProvider
    {
        Task<IEnumerable<LfmRequestReviewModel>> GetLfmRequests(int studentId);
        
        Task<LfmRequestDetailsReviewModel> GetLfmRequestDetails(int studentId, int orderId);

        Task<IEnumerable<CommonReviewModel>> GetAvailableSubjectsToOrders(int studentId);
        
        Task<IEnumerable<PersonalRequestsToMentorsReviewModel>> GetPersonalRequestsToMentors(int studentId);
        
        Task<PersonalRequestToMentorDetailsReviewModel> GetPersonalRequestToMentorDetails(int studentId, int orderId);
        
        Task<IEnumerable<ApprovedRequestReviewModel>> GetApprovedRequests(int studentId);
        
        Task<ApprovedRequestDetailsReviewModel> GetApprovedRequestDetails(int studentId, int orderId);
    }
}