using System.Linq;

namespace LFM.DataAccess.DB.Core.Repository
{
    public interface IRepository<TEntity>
        where TEntity : class
    {
        IQueryable<TEntity> GetQueryable();
    }
}