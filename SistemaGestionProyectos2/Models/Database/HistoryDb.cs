using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("order_history")]
    public class OrderHistoryDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("action")]
        public string Action { get; set; }

        [Column("field_name")]
        public string FieldName { get; set; }

        [Column("old_value")]
        public string OldValue { get; set; }

        [Column("new_value")]
        public string NewValue { get; set; }

        [Column("change_description")]
        public string ChangeDescription { get; set; }

        [Column("ip_address")]
        public string IpAddress { get; set; }

        [Column("changed_at")]
        public DateTime? ChangedAt { get; set; }
    }

    [Table("t_vendor_commission_payment")]
    public class VendorCommissionPaymentDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }
        
        // Este método controla si el Id debe serializarse
        public bool ShouldSerializeId() => Id > 0;

        [Column("f_order")]
        public int OrderId { get; set; }

        [Column("f_vendor")]
        public int VendorId { get; set; }

        [Column("commission_amount")]
        public decimal CommissionAmount { get; set; }

        [Column("commission_rate")]
        public decimal CommissionRate { get; set; }

        [Column("payment_status")]
        public string PaymentStatus { get; set; }

        [Column("payment_date")]
        public DateTime? PaymentDate { get; set; }

        [Column("payment_reference")]
        public string PaymentReference { get; set; }

        [Column("notes")]
        public string Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }
    }
}
