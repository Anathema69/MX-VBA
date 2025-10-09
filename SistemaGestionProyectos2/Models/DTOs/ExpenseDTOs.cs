namespace SistemaGestionProyectos2.Models.DTOs
{
    public class ExpenseStatistics
    {
        public decimal TotalExpenses { get; set; }
        public decimal PendingExpenses { get; set; }
        public decimal PaidExpenses { get; set; }
        public decimal OverdueExpenses { get; set; }
        public int ExpenseCount { get; set; }
        public decimal AverageExpense { get; set; }

        // Propiedades para compatibilidad con código existente
        public decimal TotalPending => PendingExpenses;
        public decimal TotalPaid => PaidExpenses;
        public decimal TotalOverdue => OverdueExpenses;
        public int PendingCount { get; set; }
        public int PaidCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal GrandTotal => TotalPending + TotalPaid;
    }
}
