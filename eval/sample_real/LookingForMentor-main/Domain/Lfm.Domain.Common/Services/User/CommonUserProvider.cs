using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Repository;
using LFM.DataAccess.DB.Core.Types;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Domain.Common.Services.User
{
    internal class CommonUserProvider : ICommonUserProvider
    {
        private readonly IRepository<LfmUser> _usersRepo;
        private readonly IMapper _mapper;

        public CommonUserProvider(
            IRepository<LfmUser> usersRepo, 
            IMapper mapper)
        {
            _usersRepo = usersRepo;
            _mapper = mapper;
        }

        public async Task<TModel> GetUser<TModel>(int userId, LfmIdentityRolesEnum role)
            where TModel : class, new()
        {
            int roleId = (int) role;

            var user = await _usersRepo.GetQueryable()
                .Where(u => u.Id == userId)
                .Where(u => u.Roles.Any(r => r.RoleId == roleId))
                .ProjectTo<TModel>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (user == default)
                throw new LfmException(Messages.DataNotFound, "Користувача");

            return user;
        }
        
        public async Task<(ICollection<TModel> data, int totalCount)> GetUsers<TModel>(
            LfmIdentityRolesEnum role, 
            int skip = 0, int? limit = null) where TModel : class, new()
        {
            int roleId = (int) role;

            var query = _usersRepo.GetQueryable()
                .Where(u => u.Roles.Any(r => r.RoleId == roleId))
                .ProjectTo<TModel>(_mapper.ConfigurationProvider);

            int totalCount = await query.CountAsync();

            var users = await (limit > 0 
                ? 
                query.Skip(skip).Take(limit.Value).ToListAsync() 
                :
                query.ToListAsync());

            return (users, totalCount);
        }
    }
}