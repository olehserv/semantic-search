using System.Collections.Generic;
using System.Linq;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Extensions;
using Lfm.Web.Mvc.App.UIRenderers.Models;
using Microsoft.AspNetCore.Http;

namespace Lfm.Web.Mvc.App.UIRenderers
{
    public static class UserCabinetUiRenderer
    {
        public static List<LinkItem> GetCabinetNavigationItems(HttpContext context)
        {
            if (context.User.Identity.IsAuthenticated)
            {
                if (context.User.IsMentor())
                {
                    return MentorNavItems;
                }
                if (context.User.IsStudent())
                {
                    return StudentNavItems;
                }
            }
            return new List<LinkItem>();
        }

        public static LinkItem GetCabinetIndex(HttpContext context)
        {
            if (context.User.Identity.IsAuthenticated)
            {
                if (context.User.IsMentor())
                {
                    return MentorNavItems.FirstOrDefault();
                }
                if (context.User.IsStudent())
                {
                    return StudentNavItems.FirstOrDefault();
                }
            }
            return default;
        }

        private static List<LinkItem> MentorNavItems =>
            new List<LinkItem>
            {
                new LinkItem
                {
                    Name = "Підтвердженні заявки",
                    ControllerName = "MentorUserCabinet",
                    ActionName = "ApprovedOrders"
                },
                new LinkItem
                {
                    Name = "Загальна інформація профілю",
                    ControllerName = "MentorUserCabinet",
                    ActionName = "GeneralInfo"
                },
                new LinkItem
                {
                    Name = "Мої предмети",
                    ControllerName = "MentorUserCabinet",
                    ActionName = "SubjectsInfo"
                },
                new LinkItem
                {
                    Name = "Мої персональні заявки",
                    ControllerName = "MentorUserCabinet",
                    ActionName = "PersonalOrders"
                },
                new LinkItem
                {
                    Name = "Потенційні заявки",
                    ControllerName = "MentorUserCabinet",
                    ActionName = "PotentialOrders"
                }
            };
        
        private static List<LinkItem> StudentNavItems =>
            new List<LinkItem>
            {
                new LinkItem
                {
                    Name = "Підтвердженні заявки",
                    ControllerName = "StudentUserCabinet",
                    ActionName = "ApprovedOrders"
                },
                new LinkItem
                {
                    Name = "Мої активні заявки",
                    ControllerName = "StudentUserCabinet",
                    ActionName = "LfmRequests"
                },
                new LinkItem
                {
                    Name = "Персональні заявки до викладачів",
                    ControllerName = "StudentUserCabinet",
                    ActionName = "PersonalRequestsToMentors"
                }
            };
    }
}