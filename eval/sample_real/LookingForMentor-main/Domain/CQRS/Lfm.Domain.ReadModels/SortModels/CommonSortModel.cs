using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.ReadModels.SortModels
{
    public class CommonSortModel<T> where T : System.Enum
    {
        public T SortParameter { get; set; }

        public bool Asc { get; set; } = true;
    }
}