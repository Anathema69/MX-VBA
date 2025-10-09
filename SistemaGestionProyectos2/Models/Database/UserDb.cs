using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("users")]
    public class UserDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("username")]
        public string Username { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
    }
}
