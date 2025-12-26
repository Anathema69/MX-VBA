using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Lógica de interacción para PendingIncomesView.xaml
    /// </summary>
    public partial class PendingIncomesView : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<ClientPendingViewModel> _allClients;
        private ObservableCollection<ClientPendingViewModel> _displayedClients;
        private string _currentSearchText = "";
        private string _currentStatusFilter = "Todos";
        

        public PendingIncomesView(UserSession currentUser)
        {
            InitializeComponent();

            _allClients = new ObservableCollection<ClientPendingViewModel>();
            _displayedClients = new ObservableCollection<ClientPendingViewModel>();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            ClientsItemsControl.ItemsSource = _displayedClients;

            // Mostrar estado de carga
            ShowLoadingState();

            // Cargar datos de manera asíncrona
            _ = LoadPendingIncomesAsync();
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
            // Ocultar el mensaje de "no hay datos"
            NoDataMessage.Visibility = Visibility.Collapsed;

            // Mostrar indicador de carga (puedes agregar un ProgressBar en el XAML)
            StatusText.Text = "Cargando ingresos pendientes...";

            // Valores iniciales mientras carga
            TotalPendingText.Text = "---";
            TotalOverdueText.Text = "---";
            TotalDueSoonText.Text = "---";
            ClientCountText.Text = "---";
            ResultCountText.Text = "Cargando...";
        }

        private async Task LoadPendingIncomesAsync()
        {
            try
            {
                StatusText.Text = "Cargando información...";

                // Una sola llamada optimizada
                var data = await _supabaseService.GetAllPendingIncomesData();

                _allClients.Clear();

                decimal totalPending = 0;
                decimal totalOverdue = 0;
                decimal totalDueSoon = 0;

                DateTime today = DateTime.Today;
                DateTime dueSoonDate = today.AddDays(7);

                foreach (var clientData in data.ClientsWithPendingInvoices)
                {
                    var viewModel = new ClientPendingViewModel
                    {
                        ClientId = clientData.ClientId,
                        ClientName = clientData.ClientName,
                        TotalPending = clientData.TotalPending,
                        InvoiceCount = clientData.Invoices.Count
                    };

                    // Calcular vencimientos
                    foreach (var invoice in clientData.Invoices)
                    {
                        if (invoice.DueDate.HasValue)
                        {
                            if (invoice.DueDate.Value < today)
                            {
                                viewModel.OverdueCount++;
                                viewModel.OverdueAmount += invoice.Total ?? 0;
                            }
                            else if (invoice.DueDate.Value <= dueSoonDate)
                            {
                                viewModel.DueSoonCount++;
                                viewModel.DueSoonAmount += invoice.Total ?? 0;
                            }
                        }
                    }

                    viewModel.HasOverdue = viewModel.OverdueCount > 0;
                    viewModel.HasDueSoon = viewModel.DueSoonCount > 0;

                    // Asignar color
                    if (viewModel.HasOverdue)
                        viewModel.StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    else if (viewModel.HasDueSoon)
                        viewModel.StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    else
                        viewModel.StatusColor = new SolidColorBrush(Color.FromRgb(72, 187, 120));

                    // Generar iniciales
                    var words = viewModel.ClientName.Split(' ');
                    if (words.Length >= 2)
                        viewModel.ClientInitials = $"{words[0][0]}{words[1][0]}".ToUpper();
                    else
                        viewModel.ClientInitials = viewModel.ClientName.Substring(0, Math.Min(2, viewModel.ClientName.Length)).ToUpper();

                    _allClients.Add(viewModel);

                    totalPending += viewModel.TotalPending;
                    totalOverdue += viewModel.OverdueAmount;
                    totalDueSoon += viewModel.DueSoonAmount;
                }

                // Actualizar UI
                var culture = new CultureInfo("es-MX");
                TotalPendingText.Text = totalPending.ToString("C", culture);
                TotalOverdueText.Text = totalOverdue.ToString("C", culture);
                TotalDueSoonText.Text = totalDueSoon.ToString("C", culture);
                ClientCountText.Text = _allClients.Count.ToString();

                ApplyFilters();

                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
                StatusText.Text = "Listo";

                // Mostrar mensaje si no hay datos
                if (_allClients.Count == 0)
                {
                    NoDataMessage.Visibility = Visibility.Visible;
                    ClientsItemsControl.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NoDataMessage.Visibility = Visibility.Collapsed;
                    ClientsItemsControl.Visibility = Visibility.Visible;
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
            // Verificar que los controles existan
            if (ResultCountText == null)
                return;

            // Verificar que tengamos datos
            if (_allClients == null || _allClients.Count == 0)
            {
                if (_displayedClients != null)
                    _displayedClients.Clear();
                ResultCountText.Text = "0 clientes";
                return;
            }

            // Convertir a enumerable para aplicar filtros
            IEnumerable<ClientPendingViewModel> filtered = _allClients;

            // Aplicar búsqueda
            if (!string.IsNullOrWhiteSpace(_currentSearchText))
            {
                filtered = filtered.Where(c =>
                    c.ClientName != null &&
                    c.ClientName.ToLower().Contains(_currentSearchText.ToLower()));
            }

            // Aplicar filtro de estado
            switch (_currentStatusFilter)
            {
                case "Con vencidas":
                    filtered = filtered.Where(c => c.HasOverdue);
                    break;
                case "Por vencer":
                    filtered = filtered.Where(c => c.HasDueSoon);
                    break;
                case "Al corriente":
                    filtered = filtered.Where(c => !c.HasOverdue && !c.HasDueSoon);
                    break;
                    // "Todos" no necesita filtro adicional
            }

            // Aplicar ordenamiento PRIMERO LOS VENCIDOS, LUEGO POR VENCER, LUEGO AL CORRIENTE EN ORDEN ALFABÉTICO 
            filtered = filtered
                .OrderByDescending(c => c.HasOverdue)
                .ThenByDescending(c => c.HasDueSoon)
                .ThenBy(c => c.ClientName);



            // Limpiar la colección mostrada y agregar los elementos filtrados
            if (_displayedClients != null)
            {
                _displayedClients.Clear();

                // Convertir a lista para evitar múltiples enumeraciones
                var filteredList = filtered?.ToList() ?? new List<ClientPendingViewModel>();

                foreach (var client in filteredList)
                {
                    _displayedClients.Add(client);
                }

                // Actualizar contador
                ResultCountText.Text = $"{_displayedClients.Count} cliente{(_displayedClients.Count != 1 ? "s" : "")}";
            }
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
            RefreshDetailButton.IsEnabled = false;
            await LoadPendingIncomesAsync();
            RefreshDetailButton.IsEnabled = true;
        }

        private void ViewDetailButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var clientId = (int)button.Tag;

            var clientViewModel = _displayedClients.FirstOrDefault(c => c.ClientId == clientId);
            if (clientViewModel != null)
            {
                var detailWindow = new PendingIncomesDetailView(_currentUser, clientId, clientViewModel.ClientName);
                detailWindow.ShowDialog();

                // Recargar después de cerrar el detalle por si hubo cambios
                _ = LoadPendingIncomesAsync();
            }
        }

        // Manejo de clic en el botón de regresar
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Manejo de clic en las tarjetas de resumen para el caso de facturas VENCIDAS
        private void OverdueCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Cambiar el filtro a "Con vencidas"
            StatusFilter.SelectedIndex = 1; // Index de "Con vencidas"
            _currentStatusFilter = "Con vencidas";
            ApplyFilters();
        }

        // Manejo de clic en las tarjetas de resumen para el caso de facturas por vencer
        private void DueSoonCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Cambiar el filtro a "Por vencer"
            StatusFilter.SelectedIndex = 2; // Index de "Por vencer"
            _currentStatusFilter = "Por vencer";
            ApplyFilters();
        }

        // Manejo de clic en las tarjetas de resumen para el caso de facturas (TODAS)
        private void TotalPendingCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Cambiar el filtro a "Todos"
            StatusFilter.SelectedIndex = 0; // Index de "Todos"
            _currentStatusFilter = "Todos";
            ApplyFilters();
        }
    }

    // ViewModels
    public class ClientPendingViewModel : INotifyPropertyChanged
    {
        private int _clientId;
        private string _clientName;
        private string _clientInitials;
        private decimal _totalPending;
        private int _invoiceCount;
        private int _overdueCount;
        private int _dueSoonCount;
        private decimal _overdueAmount;
        private decimal _dueSoonAmount;
        private bool _hasOverdue;
        private bool _hasDueSoon;

        private SolidColorBrush _statusColor;

        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set
            {
                _statusColor = value;
                OnPropertyChanged();
            }
        }

        public int ClientId
        {
            get => _clientId;
            set { _clientId = value; OnPropertyChanged(); }
        }

        public string ClientName
        {
            get => _clientName;
            set { _clientName = value; OnPropertyChanged(); }
        }

        public string ClientInitials
        {
            get => _clientInitials;
            set { _clientInitials = value; OnPropertyChanged(); }
        }

        public decimal TotalPending
        {
            get => _totalPending;
            set { _totalPending = value; OnPropertyChanged(); }
        }

        public int InvoiceCount
        {
            get => _invoiceCount;
            set { _invoiceCount = value; OnPropertyChanged(); }
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Converters
    public class StatusToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is decimal overdueAmount && values[1] is decimal dueSoonAmount)
            {
                if (overdueAmount > 0)
                    return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Rojo
                if (dueSoonAmount > 0)
                    return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amarillo

                return new SolidColorBrush(Color.FromRgb(72, 187, 120)); // Verde
            }

            return new SolidColorBrush(Color.FromRgb(226, 232, 240)); // Gris por defecto
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}