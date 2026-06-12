using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using Lfm.Core.Common.Web.Configurations;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Caching.CachingModels;
using Lfm.Domain.Common.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lfm.Domain.Common.Caching.User
{
    internal class UserCachingService : IUserCachingService
    {
        private readonly ConcurrentDictionary<int, (LfmUserCacheModel User, DateTime ExpirationDateTime)> _userCacheStorage;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AppConfigurations _appConfig;
        
        public UserCachingService(IServiceScopeFactory scopeFactory, IOptions<AppConfigurations> appConfig)
        {
            _userCacheStorage = new ConcurrentDictionary<int, (LfmUserCacheModel, DateTime)>();
            _scopeFactory = scopeFactory;
            _appConfig = appConfig.Value;
        }
        
        public Task<bool> TryGetUser(out LfmUserCacheModel cachedUser)
        {
            cachedUser = null;
            int userId = GetUserId();
            if (_userCacheStorage.TryGetValue(userId, out var cachedValue))
            {
                if (DateTime.UtcNow >= cachedValue.ExpirationDateTime)
                {
                    _userCacheStorage.TryRemove(userId, out _);
                }
                else
                {
                    cachedUser = cachedValue.User;
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }
        
        public Task<bool> TryCacheUser((LfmUser Model, LfmIdentityRolesEnum Role) user)
        {
            if (_userCacheStorage.ContainsKey(user.Model.Id)) 
                return  Task.FromResult(false);

            var expirationDateTime = DateTime.UtcNow.AddHours(_appConfig.UserSessionExpirationHours);
            var cacheUser = new LfmUserCacheModel(user.Model, user.Role);
            bool isAdded = this._userCacheStorage.TryAdd(user.Model.Id, (cacheUser, expirationDateTime));
            return Task.FromResult(isAdded);
        }

        public async Task EnsureUserCache()
        {
            int userId = GetUserId();
            if (!_userCacheStorage.ContainsKey(userId))
                await TryCacheUser();
        }

        public Task RemoveUserFromCache()
        {
            int userId = GetUserId();
            _userCacheStorage.TryRemove(userId, out _);
            return Task.CompletedTask;
        }
        
        private async Task<bool> TryCacheUser()
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            SignInManager<LfmUser> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<LfmUser>>();

            LfmUser user = await signInManager.UserManager.FindByIdAsync(signInManager.Context.User.GetId().ToString());
            LfmIdentityRolesEnum role = signInManager.Context.User.GetRole();
        
            var expirationDateTime = DateTime.UtcNow.AddHours(_appConfig.UserSessionExpirationHours);
            var cacheUser = new LfmUserCacheModel(user, role);
        
            bool isAdded = _userCacheStorage.TryAdd(user.Id, (cacheUser, expirationDateTime));
            scope.Dispose();
            return isAdded;   
        }

        private int GetUserId()
        {
            using var scope = _scopeFactory.CreateScope();
            var contextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            return contextAccessor.HttpContext.User.GetId();
        }
    }
}