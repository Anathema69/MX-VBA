using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Models
{
    public class EjecutorChip
    {
        public string Initials { get; set; }
        public string ShortName { get; set; }
        public SolidColorBrush Background { get; set; }
        public SolidColorBrush Foreground { get; set; }

        private static readonly (string bg, string fg)[] ChipPalette = new[]
        {
            ("#DBEAFE", "#1E40AF"), // Blue
            ("#D1FAE5", "#065F46"), // Green
            ("#FEF3C7", "#92400E"), // Amber
            ("#E0E7FF", "#3730A3"), // Indigo
            ("#FCE7F3", "#9D174D"), // Pink
            ("#CFFAFE", "#155E75"), // Cyan
            ("#FDE68A", "#78350F"), // Yellow
            ("#E9D5FF", "#6B21A8"), // Purple
            ("#FFE4E6", "#9F1239"), // Rose
            ("#D1D5DB", "#1F2937"), // Gray
        };

        public EjecutorChip(string fullName)
        {
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                : fullName.Substring(0, Math.Min(2, fullName.Length)).ToUpper();
            ShortName = parts.Length >= 2
                ? $"{parts[0]} {parts[1][0]}."
                : fullName;

            var colorIndex = Math.Abs(fullName.GetHashCode()) % ChipPalette.Length;
            var (bg, fg) = ChipPalette[colorIndex];
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
            Background.Freeze();
            Foreground.Freeze();
        }
    }

    public class OrderViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string ClientName { get; set; }
        public string Description { get; set; }
        public string VendorName { get; set; }
        public DateTime PromiseDate { get; set; }
        /// <summary>
        /// Porcentaje de avance del TRABAJO (0-100%). Editable manualmente.
        /// NO confundir con OrderPercentage (porcentaje de facturación).
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Porcentaje de FACTURACIÓN (0-100%). Calculado automáticamente por trigger de BD.
        /// NO confundir con ProgressPercentage (avance del trabajo).
        /// </summary>
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
        public decimal GastoOperativo { get; set; } // Suma de order_gastos_operativos (valor manual)
        public decimal GastoIndirecto { get; set; } // Suma de order_gastos_indirectos

        // Ejecutores asignados a la orden
        public string EjecutoresNombre { get; set; } = "";
        public List<int> EjecutoresIds { get; set; } = new();
        public List<EjecutorChip> EjecutorChips => string.IsNullOrWhiteSpace(EjecutoresNombre)
            ? new List<EjecutorChip>()
            : EjecutoresNombre.Split(", ").Select(n => new EjecutorChip(n)).ToList();
        public EjecutorChip EjecutorChipFirst => EjecutorChips.FirstOrDefault();
        public int EjecutorExtraCount => Math.Max(0, EjecutorChips.Count - 1);
        public bool HasEjecutorExtra => EjecutorExtraCount > 0;
        public string EjecutorExtraText => $"+{EjecutorExtraCount}";
        public bool TieneEjecutores => !string.IsNullOrWhiteSpace(EjecutoresNombre);

        // Comisión del vendedor (porcentaje decimal, ej: 5.00 = 5%)
        public decimal CommissionRate { get; set; }

        // Propiedades formateadas para mostrar en UI
        public string GastoMaterialFormatted => GastoMaterial.ToString("C");
        public string GastoOperativoFormatted => GastoOperativo.ToString("C");
        public string GastoIndirectoFormatted => GastoIndirecto.ToString("C");

        // Texto descriptivo para tooltip de Gasto Operativo
        public string GastoOperativoTooltip => CommissionRate > 0
            ? $"Gasto operativo (incluye comisión {CommissionRate:N2}%): {GastoOperativo:C}"
            : $"Gasto operativo: {GastoOperativo:C}";
    }
}