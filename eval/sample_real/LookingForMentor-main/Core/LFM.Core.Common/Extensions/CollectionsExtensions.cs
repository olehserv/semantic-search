using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LFM.Core.Common.Extensions
{
    public static class CollectionsExtensions
    {
        public static string ToSeparatedString(this IEnumerable<string> collection, string separator)
        {
            var list = collection?.ToList() ?? new List<string>();
            
            if (list.Any() != true)
                return string.Empty;

            if (list.Count == 1)
                return list.First();
            
            return String.Join(separator, list);
        }
        
        public static IQueryable<T> AddConditionWhen<T>(this IQueryable<T> filter, Expression<Func<T, bool>> condition, bool when)
        {
            if (when)
            {
                return filter.Where(condition);
            }

            return filter;
        }
    }
}