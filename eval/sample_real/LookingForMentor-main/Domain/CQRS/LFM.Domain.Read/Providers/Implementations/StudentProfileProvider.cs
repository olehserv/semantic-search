using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.Domain.Read.EntityProvideServices;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.StudentProfile;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Read.Providers.Implementations
{
    internal class StudentProfileProvider : IStudentProfileProvider
    {
        private readonly IRepository<OrderRequest> _orderReqRepo;
        private readonly IRepository<ApprovedOrder> _approvedOrderRepo;
        private readonly IMapper _mapper;
        private readonly SubjectProvideService _subjectProvideService;

        public StudentProfileProvider(
            IRepository<OrderRequest> orderReqRepo,
            IMapper mapper, 
            SubjectProvideService subjectProvideService, 
            IRepository<ApprovedOrder> approvedOrderRepo)
        {
            _orderReqRepo = orderReqRepo;
            _mapper = mapper;
            _subjectProvideService = subjectProvideService;
            _approvedOrderRepo = approvedOrderRepo;
        }

        public async Task<IEnumerable<LfmRequestReviewModel>> GetLfmRequests(int studentId)
        {
            var data = await _orderReqRepo.GetQueryable()
                .Where(m => m.StudentId == studentId && !m.MentorId.HasValue)
                .ProjectTo<LfmRequestReviewModel>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            return data;
        }

        public async Task<LfmRequestDetailsReviewModel> GetLfmRequestDetails(int studentId, int orderId)
        {
            var order = await _orderReqRepo.GetQueryable()
                .Where(m => m.StudentId == studentId && !m.MentorId.HasValue && m.Id == orderId)
                .ProjectTo<LfmRequestDetailsReviewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (order == null)
                throw new LfmException(Messages.DataNotFound, "Активної заявки");

            return order;
        }

        public async Task<IEnumerable<CommonReviewModel>> GetAvailableSubjectsToOrders(int studentId)
        {
            var subjects = (await _subjectProvideService.GetSubjects<CommonReviewModel>()).ToList();

            var existingSubjectsOrders = await _orderReqRepo.GetQueryable()
                .Where(m => !m.MentorId.HasValue && m.StudentId == studentId)
                .Select(m => m.SubjectId)
                .ToListAsync();

            return subjects.Where(s => !existingSubjectsOrders.Contains(s.Id)).ToList();
        }

        public async Task<IEnumerable<PersonalRequestsToMentorsReviewModel>> GetPersonalRequestsToMentors(int studentId)
        {
            var data = await _orderReqRepo.GetQueryable()
                .Where(m => m.MentorId.HasValue && m.StudentId == studentId)
                .ProjectTo<PersonalRequestsToMentorsReviewModel>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            return data;
        }

        public async Task<PersonalRequestToMentorDetailsReviewModel> GetPersonalRequestToMentorDetails(int studentId, int orderId)
        {
            var order = await _orderReqRepo.GetQueryable()
                .Where(m => m.MentorId.HasValue && m.StudentId == studentId && m.Id == orderId)
                .ProjectTo<PersonalRequestToMentorDetailsReviewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (order == null)
                throw new LfmException(Messages.DataNotFound);

            return order;
        }

        public async Task<IEnumerable<ApprovedRequestReviewModel>> GetApprovedRequests(int studentId)
        {
            var data = await _approvedOrderRepo.GetQueryable()
                .Where(m => m.StudentId == studentId)
                .ProjectTo<ApprovedRequestReviewModel>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            return data;
        }

        public async Task<ApprovedRequestDetailsReviewModel> GetApprovedRequestDetails(int studentId, int orderId)
        {
            var order = await _approvedOrderRepo.GetQueryable()
                .Where(m => m.StudentId == studentId && m.Id == orderId)
                .ProjectTo<ApprovedRequestDetailsReviewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (order == null)
                throw new LfmException(Messages.DataNotFound, "Підтвердженої заявки");

            return order;
        }
    }
}