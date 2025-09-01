using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorPortalWindow : Window
    {
        private readonly UserSession _currentUser;
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<VendorCommissionViewModel> _commissions;
        private ObservableCollection<VendorCommissionViewModel> _filteredCommissions;
        private int? _vendorId;

        public VendorPortalWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _supabaseService = SupabaseService.Instance;
            _commissions = new ObservableCollection<VendorCommissionViewModel>();
            _filteredCommissions = new ObservableCollection<VendorCommissionViewModel>();

            InitializeUI();
            _ = LoadDataAsync();
        }

        private void InitializeUI()
        {
            VendorNameText.Text = _currentUser.FullName;
            Title = $"Portal del Vendedor - {_currentUser.FullName}";
            CommissionsDataGrid.ItemsSource = _filteredCommissions;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StatusText.Text = "Cargando comisiones...";

                // Primero obtener el ID del vendedor basado en el usuario actual
                await GetVendorId();

                if (_vendorId == null)
                {
                    MessageBox.Show(
                        "No se encontró información de vendedor para su usuario.\nContacte al administrador.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    StatusText.Text = "Sin información de vendedor";
                    return;
                }

                // Cargar las comisiones
                await LoadCommissions();

                StatusText.Text = "Comisiones cargadas";
                LastUpdateText.Text = $"Actualizado: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cargar comisiones: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
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

                // Cargar solo las órdenes del vendedor actual con subtotal > 0
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Where(o => o.SalesmanId == _vendorId.Value)
                    .Filter("f_salesubtotal", Postgrest.Constants.Operator.GreaterThan, "0")
                    .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var orders = ordersResponse?.Models ?? new List<OrderDb>();

                // Cargar clientes para obtener nombres
                var clientsResponse = await supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();
                var clients = clientsResponse?.Models?.ToDictionary(c => c.Id, c => c.Name)
                    ?? new Dictionary<int, string>();

                _commissions.Clear();

                foreach (var order in orders)
                {
                    // Mostrar todas las órdenes, incluso sin comisión asignada
                    // Ya no filtramos por CommissionRate > 0

                    var clientName = order.ClientId.HasValue && clients.ContainsKey(order.ClientId.Value)
                        ? clients[order.ClientId.Value]
                        : "Sin cliente";

                    var commission = new VendorCommissionViewModel
                    {
                        OrderId = order.Id,
                        OrderNumber = order.Po ?? "N/A",
                        CompanyName = clientName,
                        Description = order.Description ?? "",
                        CommissionRate = order.CommissionRate ?? 0,
                        Subtotal = order.SaleSubtotal ?? 0,
                        OrderDate = order.PoDate,
                        IsEditable = false // Solo lectura para vendedores
                    };

                    _commissions.Add(commission);
                }

                ApplyFilters();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando comisiones: {ex.Message}");
            }
        }

        private void ApplyFilters()
        {
            if (_filteredCommissions == null)
                _filteredCommissions = new ObservableCollection<VendorCommissionViewModel>();

            if (_commissions == null)
                return;

            var searchText = SearchBox?.Text?.ToLower() ?? "";

            _filteredCommissions.Clear();

            var filtered = _commissions.AsEnumerable();

            // Filtro por búsqueda
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(c =>
                    c.OrderNumber.ToLower().Contains(searchText) ||
                    c.CompanyName.ToLower().Contains(searchText) ||
                    c.Description.ToLower().Contains(searchText));
            }

            foreach (var commission in filtered)
            {
                _filteredCommissions.Add(commission);
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            OrderCountText.Text = _filteredCommissions.Count.ToString();
            var total = _filteredCommissions.Sum(c => c.Commission);
            TotalCommissionText.Text = total.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        }

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
}