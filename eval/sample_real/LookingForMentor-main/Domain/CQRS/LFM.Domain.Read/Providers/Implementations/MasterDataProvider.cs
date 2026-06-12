using System.Collections.Generic;
using System.Threading.Tasks;
using LFM.Domain.Read.EntityProvideServices;
using Lfm.Domain.ReadModels.Common;

namespace LFM.Domain.Read.Providers.Implementations
{
    internal class MasterDataProvider : IMasterDataProvider
    {
        private readonly TownsProvideService _townsProvideService;
        private readonly SubjectProvideService _subjectProvideService;

        public MasterDataProvider(
            TownsProvideService townsProvideService,
            SubjectProvideService subjectProvideService)
        {
            _townsProvideService = townsProvideService;
            _subjectProvideService = subjectProvideService;
        }

        public async Task<ICollection<CommonReviewModel>> GetTownsList()
        {
            return await _townsProvideService.GetTowns();
        }
        
        public async Task<ICollection<CommonReviewModel>> GetSubjectsList()
        {
            return await _subjectProvideService.GetSubjects<CommonReviewModel>();
        }
    }
}