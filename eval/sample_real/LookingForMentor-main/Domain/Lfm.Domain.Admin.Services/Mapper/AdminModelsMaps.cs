using AutoMapper;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.Administration;
using Lfm.Domain.Admin.Models.ReviewModels;

namespace Lfm.Domain.Admin.Services.Mapper
{
    internal class AdminModelsMaps : Profile
    {
        public AdminModelsMaps()
        {
            ManagerMaps();
        }

        void ManagerMaps()
        {
            CreateMap<LfmUser, ManagerReviewModel>()
                .ForMember(u => u.Id, source => source.MapFrom(s => s.Id))
                .ForMember(u => u.Email, source => source.MapFrom(s => s.NormalizedEmail))
                .ForMember(u => u.Name, source => source.MapFrom(s => s.Name))
                .ForMember(u => u.LastLoginTime, source => source.MapFrom(s => s.LastLoginTime))
                .ForMember(u => u.LastLoginTime, source => source.Ignore())
                .ForMember(u => u.LastActivityTime, source => source.Ignore());

            CreateMap<PendingManagerCreation, ManagerToCreateReviewModel>()
                .ForMember(u => u.PhoneNumber, source => source.MapFrom(s => s.PhoneNumber))
                .ForMember(u => u.Email, source => source.MapFrom(s => s.Email))
                .ForMember(u => u.Name, source => source.MapFrom(s => s.Name))
                .ForMember(u => u.CreationStamp, source => source.MapFrom(s => s.CreationStamp));
        }
    }
}