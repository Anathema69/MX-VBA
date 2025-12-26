using Newtonsoft.Json;

namespace SistemaGestionProyectos2.Models.DTOs
{
    /// <summary>
    /// DTO para el resultado de la función RPC delete_order_with_audit
    /// </summary>
    public class DeleteOrderResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("deleted_order_id")]
        public int? DeletedOrderId { get; set; }
    }

    /// <summary>
    /// DTO para el resultado de la función RPC can_delete_order
    /// </summary>
    public class CanDeleteOrderResult
    {
        [JsonProperty("can_delete")]
        public bool CanDelete { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("invoice_count")]
        public int InvoiceCount { get; set; }

        [JsonProperty("expense_count")]
        public int ExpenseCount { get; set; }

        [JsonProperty("commission_count")]
        public int CommissionCount { get; set; }
    }
}
