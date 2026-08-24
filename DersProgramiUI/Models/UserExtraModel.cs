using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace DersProgramiUI.Models
{
    [Table("users_extra")]
    public class UserExtraModel : BaseModel
    {
        [PrimaryKey("id", false)]
        [Column("id")]
        public string UserId { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("expire_date")]
        public DateTime ExpireDate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}