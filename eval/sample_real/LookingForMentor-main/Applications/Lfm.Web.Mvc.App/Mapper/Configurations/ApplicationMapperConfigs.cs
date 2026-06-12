using System.Linq;
using AutoMapper;
using Lfm.Core.Common.Web.Extensions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using Lfm.Domain.ReadModels.ReviewModels.MentorProfile;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.Commands.Order;
using LFM.Domain.Write.Commands.StudentProfile;
using Lfm.Web.Mvc.Models.FormModels.Auth;
using Lfm.Web.Mvc.Models.FormModels.Mentor;
using Lfm.Web.Mvc.Models.FormModels.UserCabinet.Mentor;
using Lfm.Web.Mvc.Models.FormModels.UserCabinet.Student;

namespace Lfm.Web.Mvc.App.Mapper.Configurations
{
    internal class ApplicationMapperConfigs : Profile
    {
        public ApplicationMapperConfigs()
        {
            CreateAuthModelsMaps();
            CreateMentorProfileModelsMaps();
            CreateOrdersModelsMaps();
        }

        private void CreateAuthModelsMaps()
        {
            CreateMap<LoginFormModel, LoginUserCommand>();
            
            CreateMap<RegisterMentorFormModel, RegisterMentorCommand>();
            
            CreateMap<RegisterStudentFormModel, RegisterStudentCommand>();
        }
        
        private void CreateMentorProfileModelsMaps()
        {
            CreateMap<MentorsProfile, EditMentorsProfileFormModel>()
                .ForMember(x => x.Name, o => o.Ignore())
                .ForMember(x => x.ProfileImageFormFile, o => o.Ignore());
            
            CreateMap<EditMentorsProfileFormModel, EditMentorProfileCommand>()
                .ForMember(x => x.MentorId, o => o.Ignore())
                .ForMember(x => x.ProfileImageBytes, o => o.MapFrom(p => p.ProfileImageFormFile.GetBytes()));

            CreateMap<AddMentorsSubjectFormModel, AddMentorSubjectCommand>()
                .ForMember(x => x.MentorId, o => o.Ignore());
            
            CreateMap<EditMentorsSubjectFormModel, EditMentorSubjectCommand>()
                .ForMember(x => x.MentorId, o => o.Ignore());
            
            CreateMap<MentorSubjectReviewModel, EditMentorsSubjectFormModel>()
                .ForMember(x => x.TagIds, o => o.MapFrom(p => p.SelectedTags.Select(t => t.Id).ToList()));
        }

        private void CreateOrdersModelsMaps()
        {
            CreateMap<ContactMentorFormModel, CreatePersonalOrderToMentorCommand>()
                .ForMember(x => x.StudentId, o => o.Ignore())
                .ForMember(x => x.SubjectId, o => o.MapFrom(p => p.Lesson.SubjectId))
                .ForMember(x => x.TagId, o => o.MapFrom(p => p.Lesson.TagId))
                .ForMember(x => x.StudyingPlace, o => o.MapFrom(p => p.Lesson.StudyingPlace))
                .ForMember(x => x.AmountOfLessonsPerWeek, o => o.MapFrom(p => p.Lesson.AmountOfLessonsPerWeek))
                .ForMember(x => x.LessonDuration, o => o.MapFrom(p => p.Lesson.LessonDuration))
                .ForMember(x => x.StudentName, o => o.MapFrom(p => p.UserContact.Name))
                .ForMember(x => x.StudentEmail, o => o.MapFrom(p => p.UserContact.Email))
                .ForMember(x => x.StudentPhoneNumber, o => o.MapFrom(p => p.UserContact.PhoneNumber))
                .ForMember(x => x.WhenToPractice, o => o.MapFrom(p => p.Additional.WhenToPractice))
                .ForMember(x => x.WhichHelp, o => o.MapFrom(p => p.Additional.WhichHelp))
                .ForMember(x => x.AdditionalWishes, o => o.MapFrom(p => p.Additional.AdditionalWishes));

            CreateMap<CreateLookingForMentorRequestFormModel, CreateLookingForMentorRequestCommand>()
                .ForMember(x => x.StudentId, o => o.Ignore());
        }
    }
}