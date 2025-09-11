using System.Windows;

namespace SistemaGestionProyectos2.Views
{
    public partial class PayrollHistoryWindow : Window
    {
        public PayrollHistoryWindow(int? employeeId, string employeeName)
        {
            InitializeComponent();
            Title = $"Historial - {employeeName}";
        }
    }
}