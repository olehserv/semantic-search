using System.Linq;
using LFM.DataAccess.DB.Core.Context;

namespace LFM.DataAccess.DB.Core.Repository
{
    internal class Repository<TEntity> : IRepository<TEntity>
        where TEntity : class
    {
        private readonly LfmDbContext _context;

        public Repository(LfmDbContext context)
        {
            _context = context;
        }

        public IQueryable<TEntity> GetQueryable()
        {
            return _context.Set<TEntity>().AsQueryable();
        }
    }
}