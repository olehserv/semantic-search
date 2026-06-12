using System.Collections.Generic;
using AutoMapper;
using LFM.DataAccess.DB.Core.Entities.ToDoEntities;
using Lfm.Domain.Administration.Common.Models;
using Lfm.Domain.Manager.Models.ReviewModels;
using LFM.Domain.Write.PrettyCommandConverter;
using Newtonsoft.Json;

namespace Lfm.Domain.Manager.Services.Mapper
{
    internal class ManagersModelsMaps : Profile
    {
        public ManagersModelsMaps()
        {
            CreateToDoModelsMaps();
        }

        void CreateToDoModelsMaps()
        {
            CreateMap<ToDoEntity, ToDoReviewModel>()
                .ForMember(t => t.Id, source => source.MapFrom(s => s.Id))
                .ForMember(t => t.OperationCode, source => source.MapFrom(s => s.Operation.Code))
                .ForMember(t => t.CreatedByUser, source => source.MapFrom(s => s.CreatedByUser.UserName))
                .ForMember(t => t.CreatedDateTime, source => source.MapFrom(s => s.CreatedDateTime));
            
            CreateMap<ToDoEntity, RejectedToDoReviewModel>()
                .ForMember(t => t.Id, source => source.MapFrom(s => s.Id))
                .ForMember(t => t.OperationCode, source => source.MapFrom(s => s.Operation.Code))
                .ForMember(t => t.CreatedByUser, source => source.MapFrom(s => s.CreatedByUser.UserName))
                .ForMember(t => t.CreatedDateTime, source => source.MapFrom(s => s.CreatedDateTime))
                .ForMember(t => t.RejectedDateTime, source => source.MapFrom(s => s.ModifiedDateTime))
                .ForMember(t => t.RejectedByAdmin, source => source.MapFrom(s => s.Checker.Name))
                .ForMember(t => t.RejectedReason, source => source.MapFrom(s => s.RejectReason));

            CreateMap<ToDoEntity, BaseResponseModel>()
                .ForMember(t => t.IsSuccess, source => source.MapFrom(s => true));
            
            CreateMap<ToDoEntity, ToDoDetailedReviewModel>()
                .IncludeBase<ToDoEntity, BaseResponseModel>()
                .ForMember(t => t.Id, source => source.MapFrom(s => s.Id))
                .ForMember(t => t.OperationCode, source => source.MapFrom(s => s.Operation.Code))
                .ForMember(t => t.CreatedByUser, source => source.MapFrom(s => s.CreatedByUser.UserName))
                .ForMember(t => t.CreatedDateTime, source => source.MapFrom(s => s.CreatedDateTime))
                .ForMember(t => t.OperationDescription, source => source.MapFrom(s => s.Operation.Description))
                .ForMember(t => t.Command, source => source.MapFrom(s => JsonConvert.DeserializeObject<ICollection<CommandField>>(s.PrettyCommand)));
        }
    }
}