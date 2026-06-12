using System.Threading.Tasks;
using Lfm.Core.Common.Web.Models;
using Lfm.Domain.Admin.Models.ReviewModels;

namespace Lfm.Domain.Admin.Services.DataProviders
{
    public interface IManagersProvider
    {
        Task<PageList<ManagerReviewModel>> GetManagersList(int pageNo, int? pageSize = null);

        Task<PageList<ManagerToCreateReviewModel>> GetManagersToCreate(int pageNo, int? pageSize = null);
    }
}