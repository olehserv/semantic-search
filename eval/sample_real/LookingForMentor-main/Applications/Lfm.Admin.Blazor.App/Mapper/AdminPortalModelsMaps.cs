using AutoMapper;
using Lfm.Admin.Blazor.App.Models;
using Lfm.Domain.Admin.Models.WriteModels;

namespace Lfm.Admin.Blazor.App.Mapper
{
    internal class AdminPortalModelsMaps : Profile
    {
        public AdminPortalModelsMaps()
        {
            CreateManagersModelsMaps();
        }

        void CreateManagersModelsMaps()
        {
            CreateMap<CreateManagerFormModel, CreateManagerModel>()
                .ForMember(u => u.Email, source => source.MapFrom(s => s.Email))
                .ForMember(u => u.Name, source => source.MapFrom(s => s.Name))
                .ForMember(u => u.PhoneNumber, source => source.MapFrom(s => s.PhoneNumber));
        }
    }
}