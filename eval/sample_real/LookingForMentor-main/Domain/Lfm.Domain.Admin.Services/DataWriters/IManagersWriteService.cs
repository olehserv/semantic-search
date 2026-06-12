using System.Threading.Tasks;
using Lfm.Domain.Admin.Models.WriteModels;

namespace Lfm.Domain.Admin.Services.DataWriters
{
    public interface IManagersWriteService
    {
        Task CreateManager(CreateManagerModel createModel);

        Task BlockManager(int managerId);
        
        Task UnBlockManager(int managerId);
    }
}