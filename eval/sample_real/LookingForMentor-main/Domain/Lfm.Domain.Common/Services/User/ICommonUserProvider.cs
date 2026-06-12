using System.Collections.Generic;
using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.Common.Services.User
{
    public interface ICommonUserProvider
    {
        Task<TModel> GetUser<TModel>(int userId, LfmIdentityRolesEnum role)
            where TModel : class, new();

        Task<(ICollection<TModel> data, int totalCount)> GetUsers<TModel>(LfmIdentityRolesEnum role, int pageNo = 1, int? pageSize = null)
            where TModel : class, new();
    }
}