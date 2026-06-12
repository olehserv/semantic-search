using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lfm.Core.Common.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Core.Common.Web.Extensions
{
    public static class CommonExtensions
    {
        public static byte[] GetBytes(this IFormFile formFile)
        {
            byte[] bytes = null;
            try
            {
                if (formFile != null)
                {
                    using var binaryReader = new BinaryReader(formFile.OpenReadStream());
                    bytes = binaryReader.ReadBytes((int) formFile.Length);
                }
            }
            catch
            { }
            return bytes;
        }
        
        
        public static async Task<PageList<T>> GetPageList<T>(this IQueryable<T> query, int pageNo, int? pageSize = null)
        {
            int totalCount = await query.CountAsync();

            var dataList = await (pageSize > 0 
                ? 
                query.Skip((pageNo - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync() 
                :
                query.ToListAsync());
            
            return new PageList<T>(dataList, totalCount, pageNo);
        }
    }
}