using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class PayrollHistoryWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<PayrollHistoryViewModel> _historyItems;
        private int? _employeeId;
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX"); // Para el formato de moneda

        public PayrollHistoryWindow(int? employeeId, string employeeName)
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;
            _historyItems = new ObservableCollection<PayrollHistoryViewModel>();
            _employeeId = employeeId;

            HistoryDataGrid.ItemsSource = _historyItems;

            if (employeeId.HasValue)
            {
                SubtitleText.Text = $"Empleado: {employeeName}";
            }

            LoadHistory();
        }

        private async void LoadHistory()
        {
            try
            {
                var history = await _supabaseService.GetPayrollHistory(_employeeId);

                _historyItems.Clear();

                foreach (var item in history)
                {
                    var vm = new PayrollHistoryViewModel
                    {
                        Id = item.Id,
                        Employee = item.Employee,
                        EffectiveDate = item.EffectiveDate,
                        ChangeType = item.ChangeType,
                        ChangeSummary = item.ChangeSummary ?? "Sin descripción",
                        MonthlyPayroll = item.MonthlyPayroll ?? 0,
                        CreatedAt = item.CreatedAt ?? DateTime.Now
                    };

                    // Asignar color según tipo
                    switch (item.ChangeType)
                    {
                        case "SALARY_CHANGE":
                            vm.ChangeTypeDisplay = "SALARIO";
                            vm.TypeColor = new SolidColorBrush(Color.FromRgb(91, 63, 249));
                            break;
                        case "PROMOTION":
                            vm.ChangeTypeDisplay = "PROMOCIÓN";
                            vm.TypeColor = new SolidColorBrush(Color.FromRgb(72, 187, 120));
                            break;
                        case "BENEFIT_UPDATE":
                            vm.ChangeTypeDisplay = "PRESTACIONES";
                            vm.TypeColor = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                            break;
                        case "NEW_HIRE":
                            vm.ChangeTypeDisplay = "ALTA";
                            vm.TypeColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                            break;
                        case "TERMINATION":
                            vm.ChangeTypeDisplay = "BAJA";
                            vm.TypeColor = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            break;
                        default:
                            vm.ChangeTypeDisplay = "CAMBIO";
                            vm.TypeColor = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                            break;
                    }

                    _historyItems.Add(vm);
                }

                // Actualizar estadísticas
                TotalChangesText.Text = _historyItems.Count.ToString();
                if (_historyItems.Any())
                {
                    LastChangeText.Text = _historyItems.First().EffectiveDate.ToString("dd/MM/yyyy");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar historial: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Exportación a Excel disponible en versión completa",
                "MVP", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class PayrollHistoryViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Employee { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string ChangeType { get; set; }
        public string ChangeTypeDisplay { get; set; }
        public string ChangeSummary { get; set; }
        public decimal MonthlyPayroll { get; set; }
        public DateTime CreatedAt { get; set; }
        public SolidColorBrush TypeColor { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}