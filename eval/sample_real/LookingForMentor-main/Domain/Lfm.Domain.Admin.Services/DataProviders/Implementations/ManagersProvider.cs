using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Lfm.Core.Common.Web.Extensions;
using Lfm.Core.Common.Web.Models;
using LFM.DataAccess.DB.Core.Entities.Administration;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Admin.Models.ReviewModels;
using Lfm.Domain.Common.Services.User;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Domain.Admin.Services.DataProviders.Implementations
{
    internal class ManagersProvider : IManagersProvider
    {
        private readonly ICommonUserProvider _userProvider;
        private readonly IRepository<PendingManagerCreation> _pendingManagersRepo;
        private readonly IRepository<BlockedManager> _blockedManagersRepo;
        private readonly IMapper _mapper;

        public ManagersProvider(
            ICommonUserProvider userProvider, 
            IRepository<PendingManagerCreation> pendingManagersRepo, 
            IMapper mapper, 
            IRepository<BlockedManager> blockedManagersRepo)
        {
            _userProvider = userProvider;
            _pendingManagersRepo = pendingManagersRepo;
            _mapper = mapper;
            _blockedManagersRepo = blockedManagersRepo;
        }

        public async Task<PageList<ManagerReviewModel>> GetManagersList(int pageNo, int? pageSize = null)
        {
            var managers = await _userProvider.GetUsers<ManagerReviewModel>(
                LfmIdentityRolesEnum.Manager,
                (pageNo - 1) * (pageSize ?? 0), pageSize);

            var managersIds = managers.data.Select(m => m.Id);
            
            var blockedManagers = await _blockedManagersRepo
                .GetQueryable()
                .Where(m => managersIds.Contains(m.ManagerId))
                .ToListAsync();
            
            foreach (var man in managers.data)
            {
                man.Blocked = blockedManagers.Any(m => m.ManagerId == man.Id);
            }

            return new PageList<ManagerReviewModel>(managers.data, managers.totalCount, pageNo);
        }
        
        public async Task<PageList<ManagerToCreateReviewModel>> GetManagersToCreate(int pageNo, int? pageSize = null)
        {
            var query = _pendingManagersRepo.GetQueryable()
                .Where(p => !p.IsActivated)
                .ProjectTo<ManagerToCreateReviewModel>(_mapper.ConfigurationProvider);

            return await query.GetPageList(pageNo, pageSize);
        }
    }
}