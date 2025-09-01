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
        private Dictionary<int, decimal> _originalCommissionRates;

        

        public VendorPortalAdminWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _supabaseService = SupabaseService.Instance;
            _commissions = new ObservableCollection<VendorCommissionViewModel>();
            _filteredCommissions = new ObservableCollection<VendorCommissionViewModel>();
            _vendors = new List<VendorTableDb>();
            _originalCommissionRates = new Dictionary<int, decimal>();

            InitializeUI();
            _ = LoadDataAsync();

            // Suscribir al evento de cierre para el botón Back
            BackButton.Click += BackButton_Click;
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

                // Llenar el combo de filtros con autocompletado
                VendorFilterCombo.Items.Clear();
                VendorFilterCombo.Items.Add("Todos los vendedores");

                foreach (var vendor in _vendors.OrderBy(v => v.VendorName))
                {
                    VendorFilterCombo.Items.Add(vendor.VendorName);
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

                    // Guardar el valor original para detectar cambios
                    _originalCommissionRates[commission.OrderId] = commission.CommissionRate;
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
            var selectedVendorText = VendorFilterCombo?.Text ?? "Todos los vendedores";

            _filteredCommissions.Clear();

            var filtered = _commissions.AsEnumerable();

            // Filtro por vendedor
            if (!string.IsNullOrWhiteSpace(selectedVendorText) &&
                selectedVendorText != "Todos los vendedores")
            {
                filtered = filtered.Where(c =>
                    c.VendorName.ToLower().Contains(selectedVendorText.ToLower()));
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

            await SaveChangesAsync();
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                SaveButton.IsEnabled = false;
                StatusText.Text = "Guardando cambios...";

                var supabaseClient = _supabaseService.GetClient();
                int updatedCount = 0;

                foreach (var commission in _commissions)
                {
                    // Verificar si realmente cambió el valor
                    if (_originalCommissionRates.ContainsKey(commission.OrderId) &&
                        Math.Abs(_originalCommissionRates[commission.OrderId] - commission.CommissionRate) > 0.01m)
                    {
                        var orderToUpdate = await supabaseClient
                            .From<OrderDb>()
                            .Select("*")
                            .Where(o => o.Id == commission.OrderId)
                            .Single();

                        if (orderToUpdate != null)
                        {
                            orderToUpdate.CommissionRate = commission.CommissionRate;
                            await supabaseClient
                                .From<OrderDb>()
                                .Update(orderToUpdate);
                            updatedCount++;

                            // Actualizar el valor original después de guardar
                            _originalCommissionRates[commission.OrderId] = commission.CommissionRate;
                        }
                    }
                }

                _hasUnsavedChanges = false;
                UpdateChangesIndicator();
                StatusText.Text = updatedCount > 0
                    ? $"Se actualizaron {updatedCount} órdenes correctamente"
                    : "No había cambios que guardar";

                
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

            _hasUnsavedChanges = false;
            UpdateChangesIndicator();
            _ = LoadDataAsync();
        }

        private void VendorFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Aplicar filtro cuando cambia la selección del combo, se espera que después de elegir en el bombox los resultados aparezcan de inmediato
            ApplyFilters();

            // En caso los resultados no aparezcan de inmediato, forzar la actualización del filtro después de un pequeño delay
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                new Action(() => ApplyFilters()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);



        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
            
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

        // Nuevos métodos para el manejo de edición
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var item = e.Row.Item as VendorCommissionViewModel;
                if (item != null)
                {
                    var textBox = e.EditingElement as TextBox;
                    if (textBox != null && decimal.TryParse(textBox.Text, out decimal newValue))
                    {
                        // Validar rango
                        if (newValue < 0) newValue = 0;
                        if (newValue > 100) newValue = 100;

                        // Verificar si realmente cambió comparando con el valor original
                        if (_originalCommissionRates.ContainsKey(item.OrderId))
                        {
                            bool hasChanged = Math.Abs(_originalCommissionRates[item.OrderId] - newValue) > 0.01m;

                            if (hasChanged)
                            {
                                _hasUnsavedChanges = true;
                                UpdateChangesIndicator();
                            }
                        }

                        // Actualizar valor en el ViewModel
                        item.CommissionRate = newValue;
                    }
                }
            }
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var dataGrid = sender as DataGrid;
                if (dataGrid != null)
                {
                    dataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    dataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                    // Guardar automáticamente sin confirmación cuando se presiona Enter
                    if (_hasUnsavedChanges)
                    {
                        _ = SaveChangesAsync();
                    }

                    e.Handled = true;
                }
            }
        }

        private void UpdateChangesIndicator()
        {
            if (_hasUnsavedChanges)
            {
                ChangesIndicator.Text = "Cambios pendientes";
                ChangesIndicator.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 152, 0)); // Naranja
            }
            else
            {
                ChangesIndicator.Text = "Sin cambios";
                ChangesIndicator.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.Gray);
            }
        }

        private void VendorFilterCombo_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                comboBox.IsDropDownOpen = true;

                // Filtrar items basado en el texto ingresado
                var searchText = comboBox.Text + e.Text;

                // Aplicar filtro después de un pequeño delay
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => ApplyFilters()),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void VendorFilterCombo_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void VendorFilterCombo_KeyUp(object sender, KeyEventArgs e)
        {
            // Aplicar filtro cuando se suelta una tecla
            if (e.Key != Key.Tab && e.Key != Key.Enter)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => ApplyFilters()),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void VendorManagement_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Módulo de Gestión de Vendedores\nEn desarrollo...",
                "Próximamente",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // TODO: Abrir ventana de gestión de vendedores cuando esté lista
            // var vendorManagement = new VendorManagementWindow(_currentUser);
            // vendorManagement.ShowDialog();
        }
    }
}