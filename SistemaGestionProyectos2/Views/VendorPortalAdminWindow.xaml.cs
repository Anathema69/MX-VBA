using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.ViewModels;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorPortalAdminWindow : Window
    {
        private readonly UserSession _currentUser;
        private readonly SupabaseService _supabaseService;
        private ObservableCollection<VendorCommissionViewModel> _commissions;
        private ObservableCollection<VendorCommissionViewModel> _filteredCommissions;
        private List<VendorTableDb> _vendors;
        private bool _hasUnsavedChanges = false;

        public VendorPortalAdminWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _supabaseService = SupabaseService.Instance;
            _commissions = new ObservableCollection<VendorCommissionViewModel>();
            _filteredCommissions = new ObservableCollection<VendorCommissionViewModel>();
            _vendors = new List<VendorTableDb>(); // Inicializar la lista de vendors

            InitializeUI();
            _ = LoadDataAsync();
        }

        private void InitializeUI()
        {
            UserNameText.Text = _currentUser.FullName;
            Title = $"Portal del Vendedor - {_currentUser.FullName}";
            CommissionsDataGrid.ItemsSource = _filteredCommissions;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StatusText.Text = "Cargando datos...";

                // Cargar vendedores
                await LoadVendors();

                // Cargar órdenes con comisiones
                await LoadCommissions();

                StatusText.Text = "Datos cargados correctamente";
                LastUpdateText.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al cargar datos: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error al cargar datos";
            }
        }

        private async Task LoadVendors()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                var response = await supabaseClient
                    .From<VendorTableDb>()
                    .Select("*")
                    .Get();

                _vendors = response?.Models ?? new List<VendorTableDb>();

                // Llenar el combo de filtros
                VendorFilterCombo.Items.Clear();
                VendorFilterCombo.Items.Add(new ComboBoxItem { Content = "Todos los vendedores", IsSelected = true });

                foreach (var vendor in _vendors.OrderBy(v => v.VendorName))
                {
                    VendorFilterCombo.Items.Add(new ComboBoxItem { Content = vendor.VendorName, Tag = vendor.Id });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando vendedores: {ex.Message}");
            }
        }

        private async Task LoadCommissions()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Cargar órdenes con subtotal > 0
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Filter("f_salesubtotal", Postgrest.Constants.Operator.GreaterThan, "0")
                    .Order("f_podate", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var orders = ordersResponse?.Models ?? new List<OrderDb>();

                // Cargar clientes
                var clientsResponse = await supabaseClient
                    .From<ClientDb>()
                    .Select("*")
                    .Get();
                var clients = clientsResponse?.Models?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();

                _commissions.Clear();

                foreach (var order in orders)
                {
                    var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                    var clientName = order.ClientId.HasValue && clients.ContainsKey(order.ClientId.Value)
                        ? clients[order.ClientId.Value]
                        : "Sin cliente";

                    var commission = new VendorCommissionViewModel
                    {
                        OrderId = order.Id,
                        OrderNumber = order.Po ?? "N/A",
                        VendorName = vendor?.VendorName ?? "Sin vendedor",
                        CompanyName = clientName,
                        Description = order.Description ?? "",
                        CommissionRate = order.CommissionRate ?? 0,
                        Subtotal = order.SaleSubtotal ?? 0,
                        OrderDate = order.PoDate,
                        IsEditable = true
                    };

                    _commissions.Add(commission);
                }

                // Actualizar vista filtrada
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
            // Verificar que las colecciones estén inicializadas
            if (_filteredCommissions == null)
                _filteredCommissions = new ObservableCollection<VendorCommissionViewModel>();

            if (_commissions == null)
                return;

            var searchText = SearchBox?.Text?.ToLower() ?? "";
            var selectedVendor = (VendorFilterCombo?.SelectedItem as ComboBoxItem)?.Tag as int?;

            _filteredCommissions.Clear();

            var filtered = _commissions.AsEnumerable();

            // Filtro por vendedor
            if (selectedVendor.HasValue)
            {
                filtered = filtered.Where(c =>
                    _vendors.FirstOrDefault(v => v.VendorName == c.VendorName)?.Id == selectedVendor.Value);
            }

            // Filtro por búsqueda
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(c =>
                    c.OrderNumber.ToLower().Contains(searchText) ||
                    c.VendorName.ToLower().Contains(searchText) ||
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
            var totalCommissions = _filteredCommissions.Sum(c => c.Commission);
            TotalCommissionsText.Text = totalCommissions.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasUnsavedChanges)
            {
                MessageBox.Show("No hay cambios por guardar.", "Información",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "¿Está seguro de guardar los cambios en las comisiones?",
                "Confirmar Guardado",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                SaveButton.IsEnabled = false;
                StatusText.Text = "Guardando cambios...";

                var supabaseClient = _supabaseService.GetClient();
                int updatedCount = 0;

                foreach (var commission in _commissions)
                {
                    // Actualizar solo las órdenes que cambiaron
                    var orderToUpdate = await supabaseClient
                        .From<OrderDb>()
                        .Select("*")
                        .Where(o => o.Id == commission.OrderId)
                        .Single();

                    if (orderToUpdate != null && orderToUpdate.CommissionRate != commission.CommissionRate)
                    {
                        orderToUpdate.CommissionRate = commission.CommissionRate;
                        await supabaseClient
                            .From<OrderDb>()
                            .Update(orderToUpdate);
                        updatedCount++;
                    }
                }

                _hasUnsavedChanges = false;
                StatusText.Text = $"Se actualizaron {updatedCount} órdenes correctamente";
                MessageBox.Show($"Se guardaron los cambios en {updatedCount} órdenes.",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error al guardar cambios";
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Hay cambios sin guardar. ¿Desea continuar sin guardar?",
                    "Cambios sin guardar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            _ = LoadDataAsync();
        }

        private void VendorFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CommissionRate_LostFocus(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
            UpdateSummary();
        }

        private void CommissionRate_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Permitir solo números y punto decimal
            var textBox = sender as TextBox;
            var fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            var regex = new Regex(@"^\d{0,3}(\.\d{0,2})?$");
            e.Handled = !regex.IsMatch(fullText);
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funcionalidad de exportación a Excel en desarrollo.",
                "En desarrollo", MessageBoxButton.OK, MessageBoxImage.Information);
            // TODO: Implementar exportación a Excel
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "Hay cambios sin guardar. ¿Desea salir sin guardar?",
                    "Cambios sin guardar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            this.Close();
        }
    }
}