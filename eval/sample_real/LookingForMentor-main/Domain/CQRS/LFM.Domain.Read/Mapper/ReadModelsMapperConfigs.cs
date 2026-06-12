using System.Linq;
using AutoMapper;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.MentorEntities;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;
using Lfm.Domain.ReadModels.Common;
using Lfm.Domain.ReadModels.ReviewModels.Mentor;
using Lfm.Domain.ReadModels.ReviewModels.MentorProfile;
using Lfm.Domain.ReadModels.ReviewModels.StudentProfile;
using Lfm.Domain.ReadModels.ReviewModels.Subject;

namespace LFM.Domain.Read.Mapper
{
    internal class ReadModelsMapperConfigs : Profile
    {
        public ReadModelsMapperConfigs()
        {
            CreateMentorProfileMaps();
            CreateSubjectMaps();
            CreateStudentProfileMaps();
        }

        private void CreateMentorProfileMaps()
        {
            CreateMap<MentorsSubjectTag, CommonReviewModel>()
                .ForMember(x => x.Id, o => o.MapFrom(p => p.TagId))
                .ForMember(x => x.Name, o => o.MapFrom(p => p.Tag.Name));

            CreateMap<MentorsProfile, MentorProfilePreviewModel>()
                .ForMember(x => x.TownName, o => o.MapFrom(p => p.Town.Name));
            
            CreateMap<MentorsSubjectInfo, MentorSubjectReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.SelectedTags, o => o.MapFrom(p => p.Tags));

            CreateMap<MentorsProfile, MentorPreviewModel>()
                .ForMember(x => x.Name, o => o.MapFrom(p => p.Mentor.Name))
                .ForMember(x => x.SubjectsList, o => o.MapFrom(p => p.SubjectsInfo))
                .ForMember(x => x.TownName, o => o.MapFrom(p => p.Town.Name));
            
            CreateMap<MentorsSubjectInfo, MentorPreviewModel.Subject>()
                .ForMember(x => x.Id, o => o.MapFrom(p => p.SubjectId))
                .ForMember(x => x.Name, o => o.MapFrom(p => p.Subject.Name));
            
            CreateMap<MentorsProfile, MentorDetailedPreviewModel>()
                .ForMember(x => x.Name, o => o.MapFrom(p => p.Mentor.Name))
                .ForMember(x => x.TownName, o => o.MapFrom(p => p.Town.Name))
                .ForMember(x => x.EducationInfo, o => o.MapFrom(p => p.Education))
                .ForMember(x => x.AboutInfo, o => o.MapFrom(p => p.AboutMe))
                .ForMember(x => x.SubjectsList, o => o.MapFrom(p => p.SubjectsInfo));
            
            CreateMap<MentorsSubjectInfo, MentorDetailedPreviewModel.SubjectInfo>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.Tags, o => o.MapFrom(p => p.Tags.Select(t => t.Tag.Name)));

            CreateMap<MentorsProfile, ContactMentorInfo>()
                .ForMember(x => x.Name, o => o.MapFrom(p => p.Mentor.Name))
                .ForMember(x => x.Subject, o => o.Ignore());

            CreateMap<MentorsSubjectInfo, ContactMentorInfo.SubjectInfo>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name));

            CreateMap<ApprovedOrder, MentorsApprovedOrderMinReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name));
            
            CreateMap<ApprovedOrder, MentorsApprovedOrderDetailedReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name));

            CreateMap<OrderRequest, MentorPotentialOrderReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name));
            
            CreateMap<OrderRequest, MentorPotentialOrderDetailsReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.IsInterestRequestSend, o => o.Ignore());
            
            CreateMap<OrderRequest, MentorPersonalOrderReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.CreationDateTime, o => o.MapFrom(p => p.CreationDateTime));
            
            CreateMap<OrderRequest, MentorPersonalOrderDetailedReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name));
        }

        private void CreateStudentProfileMaps()
        {
            CreateMap<OrderRequest, LfmRequestReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.CreationDateTime, o => o.MapFrom(p => p.CreationDateTime));

            CreateMap<InterestedMentorsOrdersRelation, CommonReviewModel>()
                .ForMember(x => x.Id, o => o.MapFrom(p => p.MentorId))
                .ForMember(x => x.Name, o => o.MapFrom(p => p.Mentor.Name));
            
            CreateMap<OrderRequest, LfmRequestDetailsReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.MentorsInteresting, o => o.MapFrom(p => p.InterestedMentors));



            CreateMap<OrderRequest, PersonalRequestsToMentorsReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.MentorName, o => o.MapFrom(p => p.Mentor.Name));
            
            CreateMap<OrderRequest, PersonalRequestToMentorDetailsReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.MentorName, o => o.MapFrom(p => p.Mentor.Name));

            CreateMap<ApprovedOrder, ApprovedRequestReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.MentorName, o => o.MapFrom(p => p.Mentor.Name));

            CreateMap<ApprovedOrder, ApprovedRequestDetailsReviewModel>()
                .ForMember(x => x.SubjectName, o => o.MapFrom(p => p.Subject.Name))
                .ForMember(x => x.TagName, o => o.MapFrom(p => p.SubjectsTag.Name))
                .ForMember(x => x.MentorName, o => o.MapFrom(p => p.Mentor.Name))
                .ForMember(x => x.MentorEmail, o => o.MapFrom(p => p.Mentor.Email))
                .ForMember(x => x.MentorPhoneNumber, o => o.MapFrom(p => p.Mentor.PhoneNumber));
        }

        private void CreateSubjectMaps()
        {
            CreateMap<SubjectsTag, CommonReviewModel>();
            
            CreateMap<Subject, SubjectReviewModel>()
                .ForMember(x => x.Tags, o => o.MapFrom(p => p.Tags));

            CreateMap<SubjectReviewModel, CommonReviewModel>();
        }
    }
}