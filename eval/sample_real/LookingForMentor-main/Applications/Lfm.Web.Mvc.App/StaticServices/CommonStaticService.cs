using Lfm.Core.Common.Web.Extensions;
using Lfm.Domain.ReadModels.SearchModels;
using Microsoft.AspNetCore.Http;

namespace Lfm.Web.Mvc.App.StaticServices
{
    public static class CommonStaticService
    {
        private const string LastSearchMentorsRequest = "LastSearchMentorsRequest";
        
        public static void PushLastSearchMentorsRequest(HttpContext context, MentorsSearchModel searchModel)
        {
            context.Session.SetObject(LastSearchMentorsRequest, searchModel);
        }
        
        public static MentorsSearchModel PullLastSearchMentorsRequest(HttpContext context)
        {
            return context.Session.GetObject<MentorsSearchModel>(LastSearchMentorsRequest) ?? new MentorsSearchModel();
        }
    }
}