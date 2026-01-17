using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class SupplierPendingView : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<SupplierPendingViewModel> _allSuppliers;
        private ObservableCollection<SupplierPendingViewModel> _displayedSuppliers;
        private string _currentSearchText = "";
        private string _currentStatusFilter = "Todos";
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");

        public SupplierPendingView(UserSession currentUser)
        {
            InitializeComponent();

            _allSuppliers = new ObservableCollection<SupplierPendingViewModel>();
            _displayedSuppliers = new ObservableCollection<SupplierPendingViewModel>();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            MaximizeWithTaskbar();

            SuppliersItemsControl.ItemsSource = _displayedSuppliers;

            ShowLoadingState();

            _ = LoadPendingExpensesAsync();
        }

        private void MaximizeWithTaskbar()
        {
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
        }

        private void ShowLoadingState()
        {
            NoDataMessage.Visibility = Visibility.Collapsed;
            StatusText.Text = "Cargando cuentas por pagar...";
            TotalPendingText.Text = "---";
            TotalOverdueText.Text = "---";
            TotalDueSoonText.Text = "---";
            SupplierCountText.Text = "---";
            ResultCountText.Text = "Cargando...";
        }

        private async Task LoadPendingExpensesAsync()
        {
            try
            {
                StatusText.Text = "Cargando información...";

                var supabaseClient = _supabaseService.GetClient();

                // Obtener todos los gastos PENDIENTES
                var expensesResponse = await supabaseClient
                    .From<ExpenseDb>()
                    .Where(e => e.Status == "PENDIENTE")
                    .Order("f_expensedate", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var expenses = expensesResponse?.Models ?? new List<ExpenseDb>();

                // Obtener todos los proveedores
                var suppliersResponse = await supabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.IsActive == true)
                    .Get();

                var suppliers = suppliersResponse?.Models ?? new List<SupplierDb>();
                var suppliersDict = suppliers.ToDictionary(s => s.Id, s => s);

                _allSuppliers.Clear();

                decimal totalPending = 0;
                decimal totalOverdue = 0;
                decimal totalDueSoon = 0;

                DateTime today = DateTime.Today;
                DateTime dueSoonDate = today.AddDays(7);

                // Agrupar gastos por proveedor
                var groupedExpenses = expenses
                    .Where(e => e.SupplierId > 0)
                    .GroupBy(e => e.SupplierId);

                foreach (var group in groupedExpenses)
                {
                    var supplierId = group.Key;
                    var supplierExpenses = group.ToList();

                    // Obtener información del proveedor
                    var supplier = suppliersDict.ContainsKey(supplierId) ? suppliersDict[supplierId] : null;
                    var supplierName = supplier?.SupplierName ?? "Proveedor Desconocido";
                    var creditDays = supplier?.CreditDays ?? 0;

                    var viewModel = new SupplierPendingViewModel
                    {
                        SupplierId = supplierId,
                        SupplierName = supplierName,
                        CreditDays = creditDays,
                        TotalPending = supplierExpenses.Sum(e => e.TotalExpense),
                        ExpenseCount = supplierExpenses.Count
                    };

                    // Calcular vencimientos basados en f_scheduleddate
                    foreach (var expense in supplierExpenses)
                    {
                        // Usar f_scheduleddate si existe, sino calcular con fecha de compra + días de crédito
                        DateTime? dueDate = expense.ScheduledDate;
                        if (!dueDate.HasValue)
                        {
                            dueDate = expense.ExpenseDate.AddDays(creditDays);
                        }

                        if (dueDate.HasValue)
                        {
                            if (dueDate.Value < today)
                            {
                                viewModel.OverdueCount++;
                                viewModel.OverdueAmount += expense.TotalExpense;
                            }
                            else if (dueDate.Value <= dueSoonDate)
                            {
                                viewModel.DueSoonCount++;
                                viewModel.DueSoonAmount += expense.TotalExpense;
                            }
                        }
                    }

                    viewModel.HasOverdue = viewModel.OverdueCount > 0;
                    viewModel.HasDueSoon = viewModel.DueSoonCount > 0;

                    // Asignar color según estado
                    if (viewModel.HasOverdue)
                        viewModel.StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Rojo
                    else if (viewModel.HasDueSoon)
                        viewModel.StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amarillo
                    else
                        viewModel.StatusColor = new SolidColorBrush(Color.FromRgb(72, 187, 120)); // Verde

                    // Generar iniciales
                    var words = supplierName.Split(' ');
                    if (words.Length >= 2)
                        viewModel.SupplierInitials = $"{words[0][0]}{words[1][0]}".ToUpper();
                    else
                        viewModel.SupplierInitials = supplierName.Substring(0, Math.Min(2, supplierName.Length)).ToUpper();

                    // Formato del total
                    viewModel.TotalPendingFormatted = viewModel.TotalPending.ToString("C", _cultureMX);

                    _allSuppliers.Add(viewModel);

                    totalPending += viewModel.TotalPending;
                    totalOverdue += viewModel.OverdueAmount;
                    totalDueSoon += viewModel.DueSoonAmount;
                }

                // Actualizar UI
                TotalPendingText.Text = totalPending.ToString("C", _cultureMX);
                TotalOverdueText.Text = totalOverdue.ToString("C", _cultureMX);
                TotalDueSoonText.Text = totalDueSoon.ToString("C", _cultureMX);
                SupplierCountText.Text = _allSuppliers.Count.ToString();

                ApplyFilters();

                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
                StatusText.Text = "Listo";

                // Mostrar mensaje si no hay datos
                if (_allSuppliers.Count == 0)
                {
                    NoDataMessage.Visibility = Visibility.Visible;
                    SuppliersItemsControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NoDataMessage.Visibility = Visibility.Collapsed;
                    SuppliersItemsControl.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
            }
        }

        private void ApplyFilters()
        {
            if (ResultCountText == null) return;

            if (_allSuppliers == null || _allSuppliers.Count == 0)
            {
                _displayedSuppliers?.Clear();
                ResultCountText.Text = "0 proveedores";
                return;
            }

            IEnumerable<SupplierPendingViewModel> filtered = _allSuppliers;

            // Aplicar búsqueda
            if (!string.IsNullOrWhiteSpace(_currentSearchText))
            {
                filtered = filtered.Where(s =>
                    s.SupplierName != null &&
                    s.SupplierName.ToLower().Contains(_currentSearchText.ToLower()));
            }

            // Aplicar filtro de estado
            switch (_currentStatusFilter)
            {
                case "Con vencidos":
                    filtered = filtered.Where(s => s.HasOverdue);
                    break;
                case "Por vencer":
                    filtered = filtered.Where(s => s.HasDueSoon);
                    break;
                case "Al corriente":
                    filtered = filtered.Where(s => !s.HasOverdue && !s.HasDueSoon);
                    break;
            }

            // Ordenar: Primero por estado (vencidos, por vencer, al corriente), luego por monto descendente
            filtered = filtered
                .OrderByDescending(s => s.HasOverdue)
                .ThenByDescending(s => s.HasDueSoon)
                .ThenByDescending(s => s.TotalPending);

            // Actualizar colección mostrada
            _displayedSuppliers.Clear();
            foreach (var supplier in filtered.ToList())
            {
                _displayedSuppliers.Add(supplier);
            }

            ResultCountText.Text = $"{_displayedSuppliers.Count} proveedor{(_displayedSuppliers.Count != 1 ? "es" : "")}";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchText = SearchBox.Text;
            ApplyFilters();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusFilter.SelectedItem is ComboBoxItem item)
            {
                _currentStatusFilter = item.Content.ToString();
                ApplyFilters();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshButton.IsEnabled = false;
            await LoadPendingExpensesAsync();
            RefreshButton.IsEnabled = true;
        }

        private void ViewDetailButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var supplierId = (int)button.Tag;

            var supplierViewModel = _displayedSuppliers.FirstOrDefault(s => s.SupplierId == supplierId);
            if (supplierViewModel != null)
            {
                var detailWindow = new SupplierPendingDetailView(_currentUser, supplierId, supplierViewModel.SupplierName);
                detailWindow.ShowDialog();

                // Recargar después de cerrar el detalle
                _ = LoadPendingExpensesAsync();
            }
        }

        private void NewExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            // Abrir DetailView en modo selector de proveedor (sin proveedor preseleccionado)
            var detailWindow = new SupplierPendingDetailView(_currentUser);
            detailWindow.ShowDialog();

            // Recargar datos después de cerrar el detalle
            _ = LoadPendingExpensesAsync();
        }

        private void SuppliersButton_Click(object sender, RoutedEventArgs e)
        {
            // Abrir ventana de gestión de proveedores
            var supplierWindow = new SupplierManagementWindow();
            supplierWindow.ShowDialog();

            // Recargar datos después de cerrar
            _ = LoadPendingExpensesAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OverdueCard_Click(object sender, MouseButtonEventArgs e)
        {
            StatusFilter.SelectedIndex = 1;
            _currentStatusFilter = "Con vencidos";
            ApplyFilters();
        }

        private void DueSoonCard_Click(object sender, MouseButtonEventArgs e)
        {
            StatusFilter.SelectedIndex = 2;
            _currentStatusFilter = "Por vencer";
            ApplyFilters();
        }

        private void TotalPendingCard_Click(object sender, MouseButtonEventArgs e)
        {
            StatusFilter.SelectedIndex = 0;
            _currentStatusFilter = "Todos";
            ApplyFilters();
        }
    }

    // ViewModel para proveedores con gastos pendientes
    public class SupplierPendingViewModel : INotifyPropertyChanged
    {
        private int _supplierId;
        private string _supplierName;
        private string _supplierInitials;
        private int _creditDays;
        private decimal _totalPending;
        private string _totalPendingFormatted;
        private int _expenseCount;
        private int _overdueCount;
        private int _dueSoonCount;
        private decimal _overdueAmount;
        private decimal _dueSoonAmount;
        private bool _hasOverdue;
        private bool _hasDueSoon;
        private SolidColorBrush _statusColor;

        public int SupplierId
        {
            get => _supplierId;
            set { _supplierId = value; OnPropertyChanged(); }
        }

        public string SupplierName
        {
            get => _supplierName;
            set { _supplierName = value; OnPropertyChanged(); }
        }

        public string SupplierInitials
        {
            get => _supplierInitials;
            set { _supplierInitials = value; OnPropertyChanged(); }
        }

        public int CreditDays
        {
            get => _creditDays;
            set { _creditDays = value; OnPropertyChanged(); }
        }

        public decimal TotalPending
        {
            get => _totalPending;
            set { _totalPending = value; OnPropertyChanged(); }
        }

        public string TotalPendingFormatted
        {
            get => _totalPendingFormatted;
            set { _totalPendingFormatted = value; OnPropertyChanged(); }
        }

        public int ExpenseCount
        {
            get => _expenseCount;
            set { _expenseCount = value; OnPropertyChanged(); }
        }

        public int OverdueCount
        {
            get => _overdueCount;
            set { _overdueCount = value; OnPropertyChanged(); }
        }

        public int DueSoonCount
        {
            get => _dueSoonCount;
            set { _dueSoonCount = value; OnPropertyChanged(); }
        }

        public decimal OverdueAmount
        {
            get => _overdueAmount;
            set { _overdueAmount = value; OnPropertyChanged(); }
        }

        public decimal DueSoonAmount
        {
            get => _dueSoonAmount;
            set { _dueSoonAmount = value; OnPropertyChanged(); }
        }

        public bool HasOverdue
        {
            get => _hasOverdue;
            set { _hasOverdue = value; OnPropertyChanged(); }
        }

        public bool HasDueSoon
        {
            get => _hasDueSoon;
            set { _hasDueSoon = value; OnPropertyChanged(); }
        }

        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
