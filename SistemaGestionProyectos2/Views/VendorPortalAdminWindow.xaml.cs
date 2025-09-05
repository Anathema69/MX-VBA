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

                // Cargar órdenes COMPLETADAS
                var ordersResponse = await supabaseClient
                    .From<OrderDb>()
                    .Select("*")
                    .Filter("f_orderstat", Postgrest.Constants.Operator.Equals, 4)
                    .Get();

                var orders = ordersResponse?.Models ?? new List<OrderDb>();

                // Cargar estados de pago
                var paymentsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Select("*")
                    .Get();

                var payments = paymentsResponse?.Models?.ToDictionary(p => p.OrderId)
                    ?? new Dictionary<int, VendorCommissionPaymentDb>();

                _commissions.Clear();

                foreach (var order in orders)
                {
                    var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                    if (vendor == null || vendor.VendorName.ToLower() == "sin_vendedor")
                        continue;

                    // Determinar estado de pago
                    var paymentStatus = "Por Pagar";
                    if (payments.ContainsKey(order.Id))
                    {
                        paymentStatus = payments[order.Id].PaymentStatus == "paid" ? "Pagado" : "Por Pagar";
                    }

                    var commission = new VendorCommissionViewModel
                    {
                        OrderId = order.Id,
                        OrderNumber = order.Po ?? "N/A",
                        VendorName = vendor.VendorName,
                        CompanyName = "Cliente",
                        Description = order.Description ?? "",
                        CommissionRate = order.CommissionRate ?? 10,
                        Subtotal = order.SaleSubtotal ?? 0,
                        PaymentStatus = paymentStatus
                    };

                    _commissions.Add(commission);
                }

                ApplyFilters();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void ApplyFilters()
        {
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

            // ORDENAR: Primero "Por Pagar", luego "Pagado"
            filtered = filtered.OrderBy(c => c.PaymentStatus == "Por Pagar" ? 0 : 1)
                              .ThenByDescending(c => c.OrderDate);

            foreach (var commission in filtered)
            {
                _filteredCommissions.Add(commission);
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            OrderCountText.Text = _filteredCommissions.Count.ToString();

            // Total de comisiones PAGADAS (verde)
            var paidCommissions = _filteredCommissions
                .Where(c => c.PaymentStatus == "Pagado")
                .Sum(c => c.Commission);
            PaidCommissionsText.Text = paidCommissions.ToString("C2", new System.Globalization.CultureInfo("es-MX"));

            // Total pendiente de pago (rojo)
            var pendingPayments = _filteredCommissions
                .Where(c => c.PaymentStatus == "Por Pagar")
                .Sum(c => c.Commission);
            PendingPaymentText.Text = pendingPayments.ToString("C2", new System.Globalization.CultureInfo("es-MX"));
        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasUnsavedChanges)
            {
                return;
            }

           

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
                    if (_originalCommissionRates.ContainsKey(commission.OrderId) &&
                        Math.Abs(_originalCommissionRates[commission.OrderId] - commission.CommissionRate) > 0.01m)
                    {
                        // Actualizar directamente usando SQL o un método más directo
                        var response = await supabaseClient
                            .From<OrderDb>()
                            .Where(o => o.Id == commission.OrderId)
                            .Set(o => o.CommissionRate, commission.CommissionRate)
                            .Update();

                        if (response?.Models?.Count > 0)
                        {
                            updatedCount++;
                            _originalCommissionRates[commission.OrderId] = commission.CommissionRate;

                            // También actualizar el registro en t_vendor_commission_payment si existe
                            var paymentUpdate = await supabaseClient
                                .From<VendorCommissionPaymentDb>()
                                .Where(p => p.OrderId == commission.OrderId)
                                .Set(p => p.CommissionRate, commission.CommissionRate)
                                .Set(p => p.CommissionAmount, commission.Subtotal * (commission.CommissionRate / 100))
                                .Update();
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
        private void PaymentStatus_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.DataContext is VendorCommissionViewModel commission)
            {
                // Solo cambiar visualmente, sin guardar en BD
                commission.PaymentStatus = commission.PaymentStatus == "Pagado" ? "Por Pagar" : "Pagado";

                // Actualizar visual
                CommissionsDataGrid.Items.Refresh();
                UpdateSummary();

                StatusText.Text = $"Estado cambiado (no guardado): {commission.OrderNumber}";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
            
        }

       
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            
            this.Close();

            // Si MAIN MENU ESTÁ ABIERTO, ACTIVARLO Y SI HAY OTRA VENTANA ABIERTA, CERRARLA
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainMenuWindow mainMenu)
                {
                    mainMenu.Activate();
                }
                else if (window != this)
                {
                    window.Close();
                }
            }


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
            var vendorManagement = new VendorManagementWindow(_currentUser);
            vendorManagement.ShowDialog();

            // Recargar datos por si se agregaron vendedores
            _ = LoadDataAsync();
        }

        // Método para cambiar el estado de pago al hacer clic en el botón
        private async void TogglePaymentStatus_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as VendorCommissionViewModel;

            if (commission == null) return;

            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Obtener el registro actual de pago de comisión
                var paymentResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(p => p.OrderId == commission.OrderId)
                    .Single();

                if (paymentResponse != null)
                {
                    // Cambiar estado
                    var newStatus = paymentResponse.PaymentStatus == "paid" ? "pending" : "paid";

                    // Si se marca como pagado, agregar fecha de pago
                    if (newStatus == "paid")
                    {
                        paymentResponse.PaymentDate = DateTime.Now;
                    }
                    else
                    {
                        paymentResponse.PaymentDate = null;
                    }

                    paymentResponse.PaymentStatus = newStatus;

                    // Actualizar en BD
                    await supabaseClient
                        .From<VendorCommissionPaymentDb>()
                        .Where(p => p.Id == paymentResponse.Id)
                        .Set(p => p.PaymentStatus, newStatus)
                        .Set(p => p.PaymentDate, paymentResponse.PaymentDate)
                        .Update();

                    // Actualizar UI
                    commission.PaymentStatus = newStatus == "paid" ? "Pagado" : "Por Pagar";

                    StatusText.Text = $"Estado de pago actualizado para orden {commission.OrderNumber}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cambiar estado de pago: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}