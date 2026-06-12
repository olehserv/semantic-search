using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Lfm.Core.Common.Web.Extensions
{
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            if (value == null)
            {
                return default;
            }
            try
            {
                var obj = JsonConvert.DeserializeObject<T>(value);
                return obj;
            }
            catch
            {
                return default;
            }
        }
    }
}