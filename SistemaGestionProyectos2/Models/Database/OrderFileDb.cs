using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("order_files")]
    public class OrderFileDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        public bool ShouldSerializeId() => Id > 0;

        [Column("f_order")]
        public int OrderId { get; set; }

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

        [Column("vendor_id")]
        public int? VendorId { get; set; }

        [Column("commission_id")]
        public int? CommissionId { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
