using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace LFM.DataAccess.DB.Core.Entities
{
    [Table("LfmUsers")]
    public class LfmUser : IdentityUser<int>
    {
        [Required]
        [MaxLength(20)]
        public string Name { get; set; }
        
        public DateTime? LastLoginTime { get; set; }
        
        public virtual ICollection<IdentityUserRole<int>> Roles { get; set; }
    }
}