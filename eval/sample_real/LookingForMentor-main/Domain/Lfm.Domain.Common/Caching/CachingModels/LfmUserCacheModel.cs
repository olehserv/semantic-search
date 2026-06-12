using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;

namespace Lfm.Domain.Common.Caching.CachingModels
{
    public class LfmUserCacheModel
    {
        public int Id { get; }

        public string Name { get; }

        public string Email { get; }
        
        public string LoginName { get; }
        
        public string PhoneNumber { get; }

        public LfmIdentityRolesEnum Role { get; }

        public LfmUserCacheModel(LfmUser user, LfmIdentityRolesEnum role)
        {
            Id = user.Id;
            Name = user.Name;
            Email = user.Email;
            LoginName = user.UserName;
            PhoneNumber = user.PhoneNumber;
            Role = role;
        }
    }
}