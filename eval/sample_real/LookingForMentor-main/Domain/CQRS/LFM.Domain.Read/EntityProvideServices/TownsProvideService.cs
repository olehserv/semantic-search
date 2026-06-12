using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.Domain.Read.Caching;
using Lfm.Domain.ReadModels.Common;
using Microsoft.EntityFrameworkCore;

namespace LFM.Domain.Read.EntityProvideServices
{
    internal class TownsProvideService
    {
        private readonly IRepository<Town> _townsRepo;
        private readonly TownsCachingService _townsCachingService;

        public TownsProvideService(
            IRepository<Town> townsRepo, 
            TownsCachingService townsCachingService)
        {
            _townsRepo = townsRepo;
            _townsCachingService = townsCachingService;
        }
        
        public async Task<ICollection<CommonReviewModel>> GetTowns()
        {
            if (!await _townsCachingService.TryGetAllTowns(out var towns))
            {
                towns = await _townsRepo.GetQueryable()
                    .Select(t => new CommonReviewModel
                    {
                        Id = t.Id,
                        Name = t.Name
                    })
                    .ToListAsync();

                if (!towns.Any())
                    throw new LfmException(Messages.DataNotFound);

                await _townsCachingService.TryCacheAllTowns(towns);
            }

            return towns;
        }
    }
}