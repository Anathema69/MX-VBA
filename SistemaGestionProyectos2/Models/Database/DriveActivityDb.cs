using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("drive_activity")]
    public class DriveActivityDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)] public int Id { get; set; }
        [Column("user_id")] public int? UserId { get; set; }
        [Column("action")] public string Action { get; set; } = "";
        [Column("target_type")] public string TargetType { get; set; } = "";
        [Column("target_id")] public int TargetId { get; set; }
        [Column("target_name")] public string? TargetName { get; set; }
        [Column("folder_id")] public int? FolderId { get; set; }
        [Column("metadata")] public string? Metadata { get; set; }
        [Column("created_at")] public DateTime? CreatedAt { get; set; }
    }
}
