using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace SistemaGestionProyectos2.Models.Database
{
    [Table("t_invoice")]
    public class InvoiceDb : BaseModel
    {
        [PrimaryKey("f_invoice")]
        public int Id { get; set; }

        [Column("f_order")]
        public int? OrderId { get; set; }

        [Column("f_folio")]
        public string Folio { get; set; }

        [Column("f_invoicedate")]
        public DateTime? InvoiceDate { get; set; }

        [Column("f_receptiondate")]
        public DateTime? ReceptionDate { get; set; }

        [Column("f_subtotal")]
        public decimal? Subtotal { get; set; }

        [Column("f_total")]
        public decimal? Total { get; set; }

        [Column("f_invoicestat")]
        public int? InvoiceStatus { get; set; }

        [Column("f_paymentdate")]
        public DateTime? PaymentDate { get; set; }

        [Column("due_date")]
        public DateTime? DueDate { get; set; }

        [Column("payment_method")]
        public string PaymentMethod { get; set; }

        [Column("payment_reference")]
        public string PaymentReference { get; set; }

        [Column("balance_due")]
        public decimal? BalanceDue { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }
    }
}
