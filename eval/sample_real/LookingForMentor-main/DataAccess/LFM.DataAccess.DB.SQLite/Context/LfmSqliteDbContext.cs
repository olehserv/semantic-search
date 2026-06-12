using LFM.DataAccess.DB.Core.Context;
using Microsoft.EntityFrameworkCore;

namespace LFM.DataAccess.DB.SQLite.Context
{
    public class LfmSqliteDbContext : LfmDbContext
    {
        public LfmSqliteDbContext() : base()
        {
        }
        
        public LfmSqliteDbContext(DbContextOptions options) : base(options)
        {
        }
    }
}