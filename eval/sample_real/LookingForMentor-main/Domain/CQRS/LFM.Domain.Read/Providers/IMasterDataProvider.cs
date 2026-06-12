using System.Collections.Generic;
using System.Threading.Tasks;
using Lfm.Domain.ReadModels.Common;

namespace LFM.Domain.Read.Providers
{
    public interface IMasterDataProvider
    {
        Task<ICollection<CommonReviewModel>> GetTownsList();

        Task<ICollection<CommonReviewModel>> GetSubjectsList();
    }
}