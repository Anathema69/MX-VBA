using Newtonsoft.Json;

namespace SistemaGestionProyectos2.Models.DTOs
{
    /// <summary>
    /// Resultado de v_inventory_category_summary (para cards de la pantalla principal)
    /// </summary>
    public class CategorySummaryDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("display_order")]
        public int DisplayOrder { get; set; }

        [JsonProperty("total_products")]
        public int TotalProducts { get; set; }

        [JsonProperty("total_stock")]
        public decimal TotalStock { get; set; }

        [JsonProperty("low_stock_count")]
        public int LowStockCount { get; set; }

        [JsonProperty("total_value")]
        public decimal TotalValue { get; set; }

        [JsonProperty("health_percent")]
        public int HealthPercent { get; set; }
    }

    /// <summary>
    /// Resultado de fn_get_inventory_stats (KPIs globales)
    /// </summary>
    public class InventoryStatsDto
    {
        [JsonProperty("total_products")]
        public int TotalProducts { get; set; }

        [JsonProperty("total_low_stock")]
        public int TotalLowStock { get; set; }

        [JsonProperty("total_categories")]
        public int TotalCategories { get; set; }

        [JsonProperty("total_value")]
        public decimal TotalValue { get; set; }
    }

    /// <summary>
    /// Resultado de fn_adjust_stock (ajuste seguro de stock)
    /// </summary>
    public class StockAdjustResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("previous_stock")]
        public decimal? PreviousStock { get; set; }

        [JsonProperty("new_stock")]
        public decimal? NewStock { get; set; }

        [JsonProperty("movement_type")]
        public string MovementType { get; set; }

        [JsonProperty("quantity")]
        public decimal? Quantity { get; set; }
    }
}
