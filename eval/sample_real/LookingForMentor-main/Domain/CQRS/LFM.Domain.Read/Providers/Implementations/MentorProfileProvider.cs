using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.Core.Types;
using LFM.Domain.Read.EntityProvideServices;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.MentorProfile;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Read.Providers.Implementations
{
    internal class MentorProfileProvider : IMentorProfileProvider
    {
        private readonly IRepository<MentorsProfile> _mentorsProfileRepo;
        private readonly IRepository<MentorsSubjectInfo> _mentorsSubjectInfo;
        private readonly IRepository<OrderRequest> _ordersRepo;
        private readonly IRepository<ApprovedOrder> _approvedOrderRepo;
        private readonly IMapper _mapper;
        private readonly SubjectProvideService _subjectProvideService;

        public MentorProfileProvider(
            IRepository<MentorsProfile> mentorsProfileRepo,
            IRepository<MentorsSubjectInfo> mentorsSubjectInfo,
            IRepository<OrderRequest> ordersRepo, 
            IRepository<ApprovedOrder> approvedOrderRepo,
            IMapper mapper, 
            SubjectProvideService subjectProvideService)
        {
            _mentorsProfileRepo = mentorsProfileRepo;
            _mentorsSubjectInfo = mentorsSubjectInfo;
            _ordersRepo = ordersRepo;
            _approvedOrderRepo = approvedOrderRepo;
            _mapper = mapper;
            _subjectProvideService = subjectProvideService;
        }

        public async Task<T> GetGeneralInfo<T>(int mentorId) where T : class
        {
            T mentorsProfile = await _mentorsProfileRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId) 
                .ProjectTo<T>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
            
            if (mentorsProfile == null)
                throw new LfmException(Messages.DataNotFound, "Профілю");

            return mentorsProfile;
        }

        public async Task<IEnumerable<MentorSubjectReviewModel>> GetSubjectsInfo(int mentorId)
        {
            var subjectsInfo = await _mentorsSubjectInfo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .ProjectTo<MentorSubjectReviewModel>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            return subjectsInfo;
        }

        public async Task<IEnumerable<CommonReviewModel>> GetAvailableSubjects(int mentorId)
        {
            var subjects = (await _subjectProvideService.GetSubjects<CommonReviewModel>()).ToList();
            
            var existingSubjects = await _mentorsSubjectInfo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .Select(m => m.SubjectId)
                .ToListAsync();

            return subjects.Where(s => !existingSubjects.Contains(s.Id)).ToList();
        }

        public async Task<MentorSubjectReviewModel> GetSubject(int mentorId, int subjectId)
        {
            var subject = await _mentorsSubjectInfo.GetQueryable()
                .Where(m => m.MentorId == mentorId && m.SubjectId == subjectId)
                .ProjectTo<MentorSubjectReviewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            return subject;
        }

        public async Task<bool> CanAddSubject(int mentorId, int subjectId)
        {
            var query = _mentorsProfileRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId);

            if ((await query.CountAsync()) != 1)
                throw new LfmException(Messages.DataNotFound, "Користувача");

            return !await query.AnyAsync(m => m.SubjectsInfo.Any(s => s.SubjectId == subjectId));
        }

        public async Task<byte[]> GetAvatar(int mentorId)
        {
            var avatar = await _mentorsProfileRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .Select(m => m.ProfileImage)
                .SingleOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(avatar?.Image))
            {
                return Convert.FromBase64String(avatar.Image);
            }

            return null;
        }
        
        public async Task<IEnumerable<MentorPersonalOrderReviewModel>> GetPersonalOrdersRequests(int mentorId)
        {
            var data = await _ordersRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .ProjectTo<MentorPersonalOrderReviewModel>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            return data;
        }
        
        public async Task<MentorPersonalOrderDetailedReviewModel> GetPersonalOrderRequestDetails(int mentorId, int orderId)
        {
            var order = await _ordersRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId && m.Id == orderId)
                .ProjectTo<MentorPersonalOrderDetailedReviewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (order == null)
                throw new LfmException(Messages.DataNotFound);

            return order;
        }

        public async Task<IEnumerable<MentorsApprovedOrderMinReviewModel>> GetApprovedOrders(int mentorId)
        {
            var data = await _approvedOrderRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .ProjectTo<MentorsApprovedOrderMinReviewModel>(_mapper.ConfigurationProvider)
                .ToListAsync();
            
            return data;
        }
        
        public async Task<MentorsApprovedOrderDetailedReviewModel> GetApprovedOrderDetails(int mentorId, int orderId)
        {
            var order = await _approvedOrderRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId && m.Id == orderId)
                .ProjectTo<MentorsApprovedOrderDetailedReviewModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (order == null)
                throw new LfmException(Messages.DataNotFound, "Заявки");

            return order;
        }

        public async Task<IEnumerable<MentorPotentialOrderReviewModel>> GetPotentialOrders(int mentorId)
        {
            var mentorInfo = await _mentorsProfileRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .Include(t => t.SubjectsInfo)
                .ThenInclude(t => t.Tags)
                .Select(m => new { m.StudyingPlace, Subjects = m.SubjectsInfo })
                .FirstOrDefaultAsync();

            if (mentorInfo == null || !mentorInfo.Subjects.Any())
                throw new LfmException(Messages.MentorNotAbleToGetPotentialOrders);
            
            var subjectsList = mentorInfo.Subjects.ToList();
            var subjectsIds = subjectsList.Select(s => s.SubjectId).ToArray();
            var mentorStudyingPlace = mentorInfo.StudyingPlace;

            var data = _ordersRepo.GetQueryable()
                .Include(o => o.Subject)
                .Include(o => o.SubjectsTag)
                .Where(o => !o.MentorId.HasValue && 
                            subjectsIds.Any(s => s == o.SubjectId) && 
                            (o.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE || 
                             mentorStudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE || 
                             o.StudyingPlace == mentorStudyingPlace))
                .ToList();

            data = data.Where(o =>
                    subjectsList.FirstOrDefault(s => s.SubjectId == o.SubjectId)?.Tags.Exists(t => t.TagId == o.TagId) == true &&
                    subjectsList.FirstOrDefault(s => s.SubjectId == o.SubjectId)?.CostPerHour >= o.CostFrom &&
                    subjectsList.FirstOrDefault(s => s.SubjectId == o.SubjectId)?.CostPerHour <= o.CostTo)
                .ToList();

            var orders = _mapper.Map<ICollection<MentorPotentialOrderReviewModel>>(data);
            
            return orders;
        }

        public async Task<MentorPotentialOrderDetailsReviewModel> GetPotentialOrderDetails(int mentorId, int orderId)
        {
            var entity = await _ordersRepo.GetQueryable()
                .Include(o => o.Subject)
                .Include(o => o.SubjectsTag)
                .Include(o => o.InterestedMentors)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (entity == null)
                throw new LfmException(Messages.DataNotFound, "Потенційної заявки");
            
            var mentorInfo = await _mentorsProfileRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .Include(t => t.SubjectsInfo)
                .ThenInclude(t => t.Tags)
                .Select(m => new { m.StudyingPlace, Subject = m.SubjectsInfo.FirstOrDefault(s => s.SubjectId == entity.SubjectId) })
                .FirstOrDefaultAsync();

            if (mentorInfo?.Subject == null)
                throw new LfmException(Messages.MentorNotAbleToGetPotentialOrders);

            if ((entity.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE ||
                mentorInfo.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE ||
                mentorInfo.StudyingPlace == entity.StudyingPlace) && 
                mentorInfo.Subject.Tags.Select(t => t.TagId).Contains(entity.TagId) &&
                mentorInfo.Subject.CostPerHour >= entity.CostFrom &&
                mentorInfo.Subject.CostPerHour <= entity.CostTo)
            {
                var order = _mapper.Map<MentorPotentialOrderDetailsReviewModel>(entity);

                if (entity.InterestedMentors.Any(m => m.MentorId == mentorId))
                {
                    order.IsInterestRequestSend = true;
                }

                return order;
            }

            throw new LfmException(Messages.DataNotFound, "Потенційної заявки");
        }
    }
}