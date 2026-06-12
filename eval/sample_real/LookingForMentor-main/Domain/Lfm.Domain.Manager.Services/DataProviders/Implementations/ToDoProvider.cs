using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.Core.Common.Extensions;
using Lfm.Core.Common.Web.Extensions;
using Lfm.Core.Common.Web.Models;
using LFM.DataAccess.DB.Core.Entities.ToDoEntities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Manager.Models.ReviewModels;
using Lfm.Domain.Manager.Models.SearchModel;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Domain.Manager.Services.DataProviders.Implementations
{
    public class ToDoProvider : IToDoProvider
    {
        private readonly IRepository<ToDoEntity> _toDoRepo;
        private readonly IMapper _mapper;

        public ToDoProvider(
            IRepository<ToDoEntity> toDoRepo, 
            IMapper mapper)
        {
            _toDoRepo = toDoRepo;
            _mapper = mapper;
        }

        public Task<PageList<ToDoReviewModel>> SearchPendingToDos(SearchToDosModel searchModel, int pageNo, int? pageSize = null)
        {
            var query = _toDoRepo.GetQueryable()
                .Where(t => !t.CheckerId.HasValue)
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Pending)
                .AddConditionWhen(t => t.OperationCodeId == searchModel.OperationId, searchModel?.OperationId != null)
                .AddConditionWhen(t => t.CreatedByUserId == searchModel.UserId, searchModel?.UserId != null)
                .OrderBy(t => t.CreatedDateTime)
                .ProjectTo<ToDoReviewModel>(_mapper.ConfigurationProvider);
            
            return query.GetPageList(pageNo, pageSize);
        }

        public Task<PageList<RejectedToDoReviewModel>> GetRejectedToDos(int pageNo, int? pageSize = null)
        {
            var query = _toDoRepo.GetQueryable()
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Rejected)
                .OrderBy(t => t.CreatedDateTime)
                .ProjectTo<RejectedToDoReviewModel>(_mapper.ConfigurationProvider);
            
            return query.GetPageList(pageNo, pageSize);
        }
        
        public Task<PageList<ToDoReviewModel>> GetReviewingToDos(int pageNo, int reviewerId, int? pageSize = null)
        {
            var query = _toDoRepo.GetQueryable()
                .Where(t => t.StatusId == (int) ToDoStatusEnum.OnReview)
                .Where(t => t.CheckerId == reviewerId)
                .OrderBy(t => t.CreatedDateTime)
                .ProjectTo<ToDoReviewModel>(_mapper.ConfigurationProvider);
            
            return query.GetPageList(pageNo, pageSize);
        }

        public Task<PageList<ToDoReviewModel>> GetApprovedToDos(int pageNo, int approverId, int? pageSize = null)
        {
            var query = _toDoRepo.GetQueryable()
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Approved)
                .Where(t => t.CheckerId == approverId)
                .OrderBy(t => t.CreatedDateTime)
                .ProjectTo<ToDoReviewModel>(_mapper.ConfigurationProvider);
            
            return query.GetPageList(pageNo, pageSize);
        }

        public async Task<ToDoDetailedReviewModel> GetDetailedPendingToDo(int toDoId)
        {
            var model = await _toDoRepo.GetQueryable()
                            .Where(t => !t.CheckerId.HasValue)
                            .Where(t => t.StatusId == (int) ToDoStatusEnum.Pending)
                            .OrderBy(t => t.CreatedDateTime)
                            .ProjectTo<ToDoDetailedReviewModel>(_mapper.ConfigurationProvider)
                            .FirstOrDefaultAsync(t => t.Id == toDoId) 
                        ?? new ToDoDetailedReviewModel
                        {
                            IsSuccess = false,
                        };

            return model;
        }
        
        public async Task<ToDoDetailedReviewModel> GetDetailedReviewingToDo(int toDoId, int reviewerId)
        {
            var model = await _toDoRepo.GetQueryable()
                            .Where(t => t.StatusId == (int) ToDoStatusEnum.OnReview)
                            .Where(t => t.CheckerId == reviewerId)
                            .OrderBy(t => t.CreatedDateTime)
                            .ProjectTo<ToDoDetailedReviewModel>(_mapper.ConfigurationProvider)
                            .FirstOrDefaultAsync(t => t.Id == toDoId)
                        ?? new ToDoDetailedReviewModel
                        {
                            IsSuccess = false,
                        };
            
            return model;
        }
        
        public async Task<ICollection<OperationReviewModel>> GetPerformingOperations()
        {
            return await _toDoRepo.GetQueryable()
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Pending)
                .Select(t => new OperationReviewModel
                {
                    Id = t.OperationCodeId,
                    Name = t.Operation.Description
                })
                .OrderBy(o => o.Name)
                .GroupBy(t => t)
                .Select(t => t.Key)
                .ToListAsync();
        }
        
        public async Task<ICollection<ToDoUserReviewModel>> GetPerformingUsers()
        {
            return await _toDoRepo.GetQueryable()
                .Where(t => t.StatusId == (int) ToDoStatusEnum.Pending)
                .Select(t => new ToDoUserReviewModel
                {
                    Id = t.CreatedByUserId,
                    Email = t.CreatedByUser.Email
                })
                .OrderBy(u => u.Email)
                .GroupBy(t => t)
                .Select(t => t.Key)
                .ToListAsync();
        }

    }
}