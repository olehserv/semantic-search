using System.Collections;
using System.Collections.Generic;

namespace Lfm.Core.Common.Web.Models
{
    public class PageList<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _data;

        public int TotalCount { get; }

        public int PageNumber { get; }

        public PageList(IEnumerable<T> data, int totalCount, int? pageNumber = 1)
        {
            this._data = data;
            this.TotalCount = totalCount;
            this.PageNumber = pageNumber ?? 1;
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}