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
using LFM.Domain.Read.EntityProvideServices;
using Lfm.Domain.ReadModels.ReviewModels.Subject;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Read.Providers.Implementations
{
    internal class SubjectsProvider : ISubjectsProvider
    {
        private readonly IRepository<Subject> _subjectsRepo;
        private readonly IMapper _mapper;
        private readonly SubjectProvideService _subjectProvideService;

        public SubjectsProvider(
            IRepository<Subject> subjectsRepo, 
            IMapper mapper, 
            SubjectProvideService subjectProvideService)
        {
            _subjectsRepo = subjectsRepo;
            _mapper = mapper;
            _subjectProvideService = subjectProvideService;
        }

        public async Task<IEnumerable<SubjectReviewModel>> GetAllSubjects()
        {
            return (await _subjectProvideService.GetSubjects<SubjectReviewModel>()).ToList();
        }

        public async Task<SubjectReviewModel> GetSubject(int subjectId)
        {
            var subject = await _subjectProvideService.GetSubjectById<SubjectReviewModel>(subjectId);
            if (subject == null)
                throw new LfmException(Messages.DataNotFound, "Subject");

            return subject;
        }

        public async Task<bool> IsExists(int subjectId)
        {
            if (await _subjectProvideService.GetSubjectById<SubjectReviewModel>(subjectId) == null)
            {
                return await _subjectsRepo.GetQueryable()
                    .AnyAsync(s => s.Id == subjectId);
            }

            return true;
        }
    }
}