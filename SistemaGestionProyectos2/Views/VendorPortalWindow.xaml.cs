using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Lógica de interacción para VendorPortalWindow.xaml
    /// </summary>
    public partial class VendorPortalWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<VendorCommissionCardViewModel> _allCommissions;
        private ObservableCollection<VendorCommissionCardViewModel> _displayedCommissions;
        private int? _vendorId;
        
        public VendorPortalWindow(UserSession currentUser)
        {
            
            // PRIMERO: Inicializar las colecciones
            _allCommissions = new ObservableCollection<VendorCommissionCardViewModel>();
            _displayedCommissions = new ObservableCollection<VendorCommissionCardViewModel>();

            // SEGUNDO: Inicializar componentes
            InitializeComponent();

            // TERCERO: Asignar el resto
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;

            // CUARTO: Vincular la colección al ItemsControl
            CommissionsItemsControl.ItemsSource = _displayedCommissions;

            InitializeUI();
            _ = LoadDataAsync();
        }

        private void InitializeUI()
        {
            VendorNameText.Text = _currentUser.FullName;
            Title = $"Portal de Comisiones - {_currentUser.FullName}";
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Primero obtener el ID del vendedor
                await GetVendorId();

                if (_vendorId == null)
                {
                    ShowNoDataMessage("No se encontró información de vendedor para su usuario.\nContacte al administrador.");
                    return;
                }

                // Cargar las comisiones
                await LoadCommissions();
                UpdateStatistics();
                LastUpdateText.Text = DateTime.Now.ToString("HH:mm");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar comisiones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GetVendorId()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Buscar el vendedor asociado al usuario actual
                var response = await supabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.UserId == _currentUser.Id)
                    .Single();

                if (response != null)
                {
                    _vendorId = response.Id;
                    System.Diagnostics.Debug.WriteLine($"Vendedor encontrado: ID {_vendorId} para usuario {_currentUser.Id}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo vendedor: {ex.Message}");
            }
        }

        private async Task LoadCommissions()
        {
            try
            {
                if (_vendorId == null) return;

                var supabaseClient = _supabaseService.GetClient();

                // 1. Cargar registros de t_vendor_commission_payment SOLO PENDIENTES para este vendedor
                var commissionsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Select("*")
                    .Where(x => x.VendorId == _vendorId.Value)
                    .Where(x => x.PaymentStatus == "pending")
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var commissions = commissionsResponse?.Models ?? new System.Collections.Generic.List<VendorCommissionPaymentDb>();

                if (commissions.Count == 0)
                {
                    ShowNoDataMessage("¡Todas tus comisiones han sido pagadas! 🎉");
                    _allCommissions.Clear();
                    ApplyFilters();
                    return;
                }

                HideNoDataMessage();

                // Obtener IDs de órdenes
                var orderIds = commissions.Select(c => c.OrderId).Distinct().ToList();

                // 2. Cargar las órdenes
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Get();

                var allOrders = ordersResponse?.Models ?? new System.Collections.Generic.List<OrderDb>();
                var orders = allOrders.Where(o => orderIds.Contains(o.Id))
                    .ToDictionary(o => o.Id);

                // 3. Cargar los clientes
                var clientIds = orders.Values.Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value).Distinct().ToList();

                var clientsResponse = await supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();

                var allClients = clientsResponse?.Models ?? new System.Collections.Generic.List<ClientDb>();
                var clients = allClients.Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id);

                // 4. Construir los ViewModels
                _allCommissions.Clear();

                foreach (var commission in commissions)
                {
                    OrderDb order = orders.ContainsKey(commission.OrderId) ? orders[commission.OrderId] : null;
                    ClientDb client = null;

                    if (order?.ClientId.HasValue == true && clients.ContainsKey(order.ClientId.Value))
                    {
                        client = clients[order.ClientId.Value];
                    }

                    var cardVm = new VendorCommissionCardViewModel
                    {
                        // IDs
                        OrderId = commission.OrderId,

                        // Información de orden
                        OrderNumber = order?.Po ?? $"ORD-{commission.OrderId}",
                        OrderDate = order?.PoDate ?? DateTime.Now,

                        // Información del cliente
                        CompanyName = client?.Name ?? "Sin Cliente",

                        // Descripción
                        Description = order?.Description ?? "Sin descripción",

                        // Información financiera
                        Subtotal = order?.SaleSubtotal ?? 0,
                        CommissionRate = commission.CommissionRate,
                        CommissionAmount = commission.CommissionAmount,

                        // Notas
                        Notes = commission.Notes ?? ""


                    };

                    _allCommissions.Add(cardVm);
                }

                // 5. Aplicar filtros y actualizar UI
                ApplyFilters();

                System.Diagnostics.Debug.WriteLine($"Se cargaron {_allCommissions.Count} comisiones pendientes");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar comisiones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            // Validación de seguridad
            if (_displayedCommissions == null)
            {
                _displayedCommissions = new ObservableCollection<VendorCommissionCardViewModel>();
                if (CommissionsItemsControl != null)
                    CommissionsItemsControl.ItemsSource = _displayedCommissions;
            }

            if (_allCommissions == null)
            {
                _allCommissions = new ObservableCollection<VendorCommissionCardViewModel>();
                return;
            }

            _displayedCommissions.Clear();

            var filtered = _allCommissions.AsEnumerable();
            var searchText = SearchBox?.Text?.ToLower() ?? "";

            // Aplicar filtro de búsqueda
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(c =>
                    c.OrderNumber.ToLower().Contains(searchText) ||
                    c.CompanyName.ToLower().Contains(searchText) ||
                    c.Description.ToLower().Contains(searchText));
            }

            // Ordenar por fecha descendente (más recientes primero)
            var sorted = filtered.OrderByDescending(c => c.OrderDate);

            foreach (var commission in sorted)
            {
                _displayedCommissions.Add(commission);
            }

            // Mostrar/ocultar mensaje de no datos
            if (_displayedCommissions.Count == 0 && !string.IsNullOrWhiteSpace(searchText))
            {
                ShowNoDataMessage($"No se encontraron resultados para '{searchText}'");
            }
            else if (_displayedCommissions.Count == 0)
            {
                ShowNoDataMessage("No tienes comisiones pendientes");
            }
            else
            {
                HideNoDataMessage();
            }
        }

        private void UpdateStatistics()
        {
            // Total de órdenes
            OrderCountText.Text = _allCommissions.Count.ToString();

            // Total pendiente (todas las comisiones mostradas están pendientes)
            var totalPending = _allCommissions.Sum(c => c.CommissionAmount);
            TotalPendingText.Text = totalPending.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
            TotalCommissionText.Text = totalPending.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        }

        private void ShowNoDataMessage(string message)
        {
            if (NoDataMessage != null)
            {
                NoDataMessage.Visibility = Visibility.Visible;
                // Actualizar el mensaje si es necesario (requeriría agregar un TextBlock con Name en el XAML)
            }
            if (CommissionsItemsControl != null)
            {
                CommissionsItemsControl.Visibility = Visibility.Collapsed;
            }
        }

        private void HideNoDataMessage()
        {
            if (NoDataMessage != null)
            {
                NoDataMessage.Visibility = Visibility.Collapsed;
            }
            if (CommissionsItemsControl != null)
            {
                CommissionsItemsControl.Visibility = Visibility.Visible;
            }
        }

        // Eventos
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "¿Está seguro que desea cerrar sesión?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Si se cierra la ventana, volver al login
            if (Application.Current.Windows.OfType<LoginWindow>().Count() == 0)
            {
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
            }
            base.OnClosed(e);
        }
    }

    // ViewModel para las cards del vendedor
    public class VendorCommissionCardViewModel : INotifyPropertyChanged
    {
        // Formatos numéricos para binding directo
        public string CommissionAmountNumber => CommissionAmount.ToString("N2");
        
        // IDs
        public int OrderId { get; set; }

        // Información de la orden
        public string OrderNumber { get; set; }
        private DateTime _orderDate;
        public DateTime OrderDate
        {
            get => _orderDate;
            set
            {
                _orderDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OrderDateFormatted));
            }
        }

        // Información del cliente
        public string CompanyName { get; set; }
        public string Description { get; set; }

        // Información financiera
        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubtotalFormatted));
            }
        }

        private decimal _commissionRate;
        public decimal CommissionRate
        {
            get => _commissionRate;
            set
            {
                _commissionRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CommissionRateFormatted));
            }
        }

        private decimal _commissionAmount;
        public decimal CommissionAmount
        {
            get => _commissionAmount;
            set
            {
                _commissionAmount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CommissionFormatted));
            }
        }

        public string Notes { get; set; }

        // Propiedades formateadas para el binding
        public string OrderDateFormatted => OrderDate.ToString("dd/MM/yyyy");
        public string SubtotalFormatted => Subtotal.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        public string CommissionRateFormatted => $"{CommissionRate:F2}%";
        public string CommissionFormatted => CommissionAmount.ToString("C2", new System.Globalization.CultureInfo("es-MX")); 

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}