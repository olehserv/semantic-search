using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.Domain.Read.Caching;
using Lfm.Domain.ReadModels.ReviewModels.Subject;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Read.EntityProvideServices
{
    internal class SubjectProvideService
    {
        private readonly IRepository<Subject> _subjectsRepo;
        private readonly IMapper _mapper;
        private readonly SubjectCachingService _subjectsCachingService;

        public SubjectProvideService(
            IRepository<Subject> subjectsRepo, 
            IMapper mapper, SubjectCachingService 
                subjectsCachingService)
        {
            _subjectsRepo = subjectsRepo;
            _mapper = mapper;
            _subjectsCachingService = subjectsCachingService;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">Should be mapped from Lfm.Domain.ReadModels.ReviewModels.Subject.SubjectReviewModel</typeparam>
        /// <returns></returns>
        /// <exception cref="LfmException"></exception>
        public async Task<ICollection<T>> GetSubjects<T>() where T : class
        {
            if (!await _subjectsCachingService.TryGetAllSubjects(out var subjects))
            {
                subjects = await _subjectsRepo.GetQueryable()
                    .ProjectTo<SubjectReviewModel>(_mapper.ConfigurationProvider)
                    .ToListAsync();

                if (!subjects.Any())
                    throw new LfmException(Messages.DataNotFound);

                await _subjectsCachingService.TryCacheAllSubjects(subjects);
            }

            return _mapper.Map<ICollection<T>>(subjects);
        }
        
        public async Task<T> GetSubjectById<T>(int subjectId) where T : class
        {
            if (!await _subjectsCachingService.TryGetById(subjectId, out var subject))
            {
                subject = await _subjectsRepo.GetQueryable()
                    .ProjectTo<SubjectReviewModel>(_mapper.ConfigurationProvider)
                    .FirstOrDefaultAsync();
            }

            return _mapper.Map<T>(subject);
        }
    }
}