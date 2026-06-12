using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.Core.Common.Extensions;
using Lfm.Core.Common.Web.Configurations;
using Lfm.Core.Common.Web.Models;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.ReadModels.ReviewModels.Mentor;
using Lfm.Domain.ReadModels.SearchModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LFM.Domain.Read.Providers.Implementations
{
    internal class MentorsProvider : IMentorsProvider
    {
        private readonly IRepository<MentorsProfile> _mentorProfilesRepo;
        private readonly IMapper _mapper;
        private readonly AppConfigurations _appConfigs;

        public MentorsProvider(
            IRepository<MentorsProfile> mentorProfilesRepo, 
            IMapper mapper,
            IOptions<AppConfigurations> configOptions)
        {
            _mentorProfilesRepo = mentorProfilesRepo;
            _mapper = mapper;
            _appConfigs = configOptions.Value;
        }

        public async Task<PageList<MentorPreviewModel>> LookingForMentors(MentorsSearchModel searchModel, int? pageNumber)
        {
            var mentorsQuery = _mentorProfilesRepo.GetQueryable()
                .Where(m => m.IsVerified && m.WantReceivePersonalOrders && m.SubjectsInfo.Any())
                .ProjectTo<MentorPreviewModel>(_mapper.ConfigurationProvider);

            if (searchModel != null)
            {
                mentorsQuery = mentorsQuery
                    .AddConditionWhen(m => m.SubjectsList.Any(s => s.Id == searchModel.SubjectId), searchModel.SubjectId.HasValue)
                    .AddConditionWhen(m => m.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE 
                                           || searchModel.StudyingPlace == StudyingPlaces.ONLINE_AND_OFFLINE 
                                           || m.StudyingPlace == searchModel.StudyingPlace, 
                        searchModel.StudyingPlace.HasValue)
                    .AddConditionWhen(m => m.TownId == searchModel.TownId, searchModel.TownId.HasValue);
            }

            int count = await mentorsQuery.CountAsync();

            int skip = 0;
            if (pageNumber.HasValue && pageNumber.Value > 0)
            {
                skip = (pageNumber.Value - 1) * _appConfigs.SearchingMentorsPageSize;
            }

            var data = await mentorsQuery
                .Skip(skip)
                .Take(_appConfigs.SearchingMentorsPageSize)
                .ToListAsync();
            
            return new PageList<MentorPreviewModel>(data, count, pageNumber);
        }
        
        public async Task<MentorDetailedPreviewModel> GetMentorInfo(int mentorId)
        {
            MentorDetailedPreviewModel mentorsProfile = await _mentorProfilesRepo.GetQueryable()
                .Where(m => m.IsVerified && m.MentorId == mentorId) 
                .ProjectTo<MentorDetailedPreviewModel>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
            
            if (mentorsProfile == null)
                throw new LfmException(Messages.DataNotFound);

            return mentorsProfile;
        }

        public async Task<ContactMentorInfo> GetContactMentorInfo(int mentorId, int subjectId)
        {
            ContactMentorInfo mentorContactInfo = await _mentorProfilesRepo.GetQueryable()
                .Where(m => m.IsVerified && m.WantReceivePersonalOrders && m.MentorId == mentorId) 
                .ProjectTo<ContactMentorInfo>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
            
            if (mentorContactInfo == null)
                throw new LfmException(Messages.DataNotFound);
            
            ContactMentorInfo.SubjectInfo subjectInfo = await _mentorProfilesRepo.GetQueryable()
                .Where(m => m.MentorId == mentorId)
                .SelectMany(m => m.SubjectsInfo)
                .Where(s => s.SubjectId == subjectId)
                .ProjectTo<ContactMentorInfo.SubjectInfo>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();
            
            if (subjectInfo == null)
                throw new LfmException(Messages.DataNotFound);

            mentorContactInfo.Subject = subjectInfo;

            return mentorContactInfo;
        }
    }
}