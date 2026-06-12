using System;

namespace Lfm.Domain.Admin.Models.ReviewModels
{
    public class ManagerReviewModel
    {
        public int Id { get; set; }

        public string Email { get; set; }
        
        public string Name { get; set; }

        public DateTime? LastLoginTime { get; set; }
        
        public DateTime? LastActivityTime { get; set; }
        
        public bool Blocked { get; set; }
    }
}