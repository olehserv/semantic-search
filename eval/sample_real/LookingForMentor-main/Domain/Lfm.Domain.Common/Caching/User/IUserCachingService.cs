using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Caching.CachingModels;

namespace Lfm.Domain.Common.Caching.User
{
    public interface IUserCachingService
    {
        Task<bool> TryGetUser(out LfmUserCacheModel cachedUser);
        
        Task<bool> TryCacheUser((LfmUser Model, LfmIdentityRolesEnum Role) user);

        Task EnsureUserCache();

        Task RemoveUserFromCache();
    }
}