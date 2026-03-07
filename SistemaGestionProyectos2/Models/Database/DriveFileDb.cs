using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("drive_files")]
    public class DriveFileDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("folder_id")]
        public int FolderId { get; set; }

        [Column("file_name")]
        public string FileName { get; set; }

        [Column("storage_path")]
        public string StoragePath { get; set; }

        [Column("file_size")]
        public long? FileSize { get; set; }

        [Column("content_type")]
        public string ContentType { get; set; }

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        [Column("uploaded_at")]
        public DateTime? UploadedAt { get; set; }
    }
}
