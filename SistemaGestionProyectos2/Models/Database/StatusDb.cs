using Postgrest.Attributes;
using Postgrest.Models;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("order_status")]
    public class OrderStatusDb : BaseModel
    {
        [PrimaryKey("f_orderstatus", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_name")]
        public string Name { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }
    }

    [Table("invoice_status")]
    public class InvoiceStatusDb : BaseModel
    {
        [PrimaryKey("f_invoicestat", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_name")]
        public string Name { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; }
    }
}
