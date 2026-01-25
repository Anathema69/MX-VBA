using System;

namespace SistemaGestionProyectos2.Models
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public string VendorName { get; set; }
        public DateTime PromiseDate { get; set; }
        public int ProgressPercentage { get; set; }
        public int OrderPercentage { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Total { get; set; }

        public decimal InvoicedAmount { get; set; }
        public string Status { get; set; }
        public bool Invoiced { get; set; }
        public DateTime? LastInvoiceDate { get; set; }

        public string InvoicedAmountFormatted => InvoicedAmount.ToString("C");
        public decimal PendingAmount => Total - InvoicedAmount;
        public string PendingAmountFormatted => PendingAmount.ToString("C");
        public double InvoicedPercentage => Total > 0 ? (double)(InvoicedAmount / Total * 100) : 0;

        // Propiedad para alternar colores por mes
        public bool EsMesImpar => OrderDate.Month % 2 == 1;

        // Columnas v2.0 - Gastos
        public decimal GastoMaterial { get; set; }  // Calculado desde t_expense (PAGADO)
        public decimal GastoOperativo { get; set; } // Suma de order_gastos_operativos

        // Propiedades formateadas para mostrar en UI
        public string GastoMaterialFormatted => GastoMaterial.ToString("C");
        public string GastoOperativoFormatted => GastoOperativo.ToString("C");
    }
}