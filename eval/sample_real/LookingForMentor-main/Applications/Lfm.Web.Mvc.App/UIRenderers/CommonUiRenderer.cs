using System;
using System.Collections.Generic;
using System.Linq;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.ReadModels.Common;
using Lfm.Web.Mvc.App.UIRenderers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lfm.Web.Mvc.App.UIRenderers
{
    public static class CommonUiRenderer
    {
        public static string ActiveLink(HttpContext context, string controllerName, string actionName)
        {
            if (context.GetRouteValue("controller").ToString() == controllerName &&
                context.GetRouteValue("action").ToString() == actionName)
            {
                return "active";
            }
            return string.Empty;
        }
        
        public static string LessonDurationToString(LessonDuration? duration)
        {
            return duration switch
            {
                LessonDuration.ONE_HOUR => "1 година",
                LessonDuration.ONE_HALF_HOUR => "1.5 години",
                LessonDuration.TWO_HOURS => "2 години",
                _ => "Більше ніж 2 години"
            };
        }
        
        public static List<CheckBox<LessonDuration>> LessonDurationCheckboxes(LessonDuration? currentDuration = null)
        {
            List<CheckBox<LessonDuration>> checkboxInfo = new List<CheckBox<LessonDuration>>
            {
                new CheckBox<LessonDuration>(LessonDuration.ONE_HOUR, c => c == currentDuration)
                {
                    Name = LessonDurationToString(LessonDuration.ONE_HOUR),
                },
                new CheckBox<LessonDuration>(LessonDuration.ONE_HALF_HOUR, c => c == currentDuration)
                {
                    Name = LessonDurationToString(LessonDuration.ONE_HALF_HOUR),
                },
                new CheckBox<LessonDuration>(LessonDuration.TWO_HOURS, c => c == currentDuration)
                {
                    Name = LessonDurationToString(LessonDuration.TWO_HOURS),
                },
                new CheckBox<LessonDuration>(LessonDuration.MORE, c => c == currentDuration)
                {
                    Name = LessonDurationToString(LessonDuration.MORE),
                }
            };

            return checkboxInfo;
        }
        
        public static string StudyingPlaceToString(StudyingPlaces? place)
        {
            return place switch
            {
                StudyingPlaces.ONLINE_ONLY => "Тільки онлайн",
                StudyingPlaces.OFFLINE_ONLY => "Тільки офлайн",
                StudyingPlaces.ONLINE_AND_OFFLINE => "Онлайн або офлайн",
                _ => string.Empty
            };
        }
        
        public static List<CheckBox<StudyingPlaces>> StudyingPlacesCheckboxes(StudyingPlaces? currentPlace = null)
        {
            List<CheckBox<StudyingPlaces>> checkboxInfo = new List<CheckBox<StudyingPlaces>>
            {
                new CheckBox<StudyingPlaces>(StudyingPlaces.ONLINE_ONLY, c => c == currentPlace)
                {
                    Name = StudyingPlaceToString(StudyingPlaces.ONLINE_ONLY),
                },
                new CheckBox<StudyingPlaces>(StudyingPlaces.OFFLINE_ONLY, c => c == currentPlace)
                {
                    Name = StudyingPlaceToString(StudyingPlaces.OFFLINE_ONLY),
                },
                new CheckBox<StudyingPlaces>(StudyingPlaces.ONLINE_AND_OFFLINE, c => c == currentPlace)
                {
                    Name = StudyingPlaceToString(StudyingPlaces.ONLINE_AND_OFFLINE),
                }
            };

            return checkboxInfo;
        }
        
        public static List<SelectItem<StudyingPlaces>> StudyingPlacesSelectItems(StudyingPlaces? currentPlace = null)
        {
            string selectedAttr = "selected";
            List<SelectItem<StudyingPlaces>> checkboxInfo = new List<SelectItem<StudyingPlaces>>
            {
                new SelectItem<StudyingPlaces>
                {
                    Name = StudyingPlaceToString(StudyingPlaces.ONLINE_ONLY),
                    Value = StudyingPlaces.ONLINE_ONLY,
                    Selected = currentPlace == StudyingPlaces.ONLINE_ONLY ? selectedAttr : string.Empty
                },
                new SelectItem<StudyingPlaces>
                {
                    Name = StudyingPlaceToString(StudyingPlaces.OFFLINE_ONLY),
                    Value = StudyingPlaces.OFFLINE_ONLY,
                    Selected = currentPlace == StudyingPlaces.OFFLINE_ONLY ? selectedAttr : string.Empty
                },
                new SelectItem<StudyingPlaces>
                {
                    Name = StudyingPlaceToString(StudyingPlaces.ONLINE_AND_OFFLINE),
                    Value = StudyingPlaces.ONLINE_AND_OFFLINE,
                    Selected = currentPlace == StudyingPlaces.ONLINE_AND_OFFLINE ? selectedAttr : string.Empty
                }
            };

            return checkboxInfo;
        }

        public static string GetDaysPassed(DateTime dateTime)
        {
            if (DateTime.UtcNow < dateTime)
                return string.Empty;

            TimeSpan subtractedDate = DateTime.UtcNow.Subtract(dateTime);
            if (subtractedDate.Days >= 1)
                return $"{subtractedDate.Days} днів тому.";

            if (subtractedDate.Hours >= 1)
            {
                return $"{subtractedDate.Hours} годин тому.";
            }

            return $"Менше години тому.";
        }

        public static IEnumerable<CheckBox<int>> GetCheckboxList(IEnumerable<CommonReviewModel> source, IEnumerable<int> checkedItems)
        {
            return source.Select(s => new CheckBox<int>(s.Id, checkedItems.Contains){Name = s.Name});
        }
    }
}