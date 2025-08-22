using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class OrdersManagementWindow : Window
    {
        private UserSession _currentUser;
        private ObservableCollection<OrderViewModel> _orders;
        private CollectionViewSource _ordersViewSource;
        private readonly SupabaseService _supabaseService;
        private List<ClientDb> _clients;
        private List<VendorDb> _vendors;
        private List<OrderStatusDb> _orderStatuses;
        public bool IsAdmin => _currentUser?.Role == "admin";

        public OrdersManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _orders = new ObservableCollection<OrderViewModel>();
            _supabaseService = SupabaseService.Instance;

            // IMPORTANTE: Establecer el DataContext para los bindings
            this.DataContext = this;

            InitializeUI();
            ConfigurePermissions();

            // Cargar datos iniciales
            _ = LoadInitialDataAsync();
        }

        

        private void InitializeUI()
        {
            // Configurar información del usuario
            UserStatusText.Text = $"Usuario: {_currentUser.FullName} ({GetRoleDisplayName(_currentUser.Role)})";

            // ⭐ IMPORTANTE: AGREGAR ESTA LÍNEA PARA QUE FUNCIONE EL BINDING
            this.Tag = _currentUser.Role;

            // Configurar el DataGrid
            _ordersViewSource = new CollectionViewSource { Source = _orders };
            OrdersDataGrid.ItemsSource = _ordersViewSource.View;

            // Título de la ventana
            this.Title = $"IMA Mecatrónica - Manejo de Órdenes - {_currentUser.FullName}";

            
        }

        private void ConfigurePermissions()
        {
            // Configurar visibilidad y permisos según el rol
            switch (_currentUser.Role)
            {
                case "admin":
                    
                    NewOrderButton.IsEnabled = true;
                    SubtotalColumn.Visibility = Visibility.Visible;
                    TotalColumn.Visibility = Visibility.Visible;
                    InvoicedColumn.Visibility = Visibility.Visible;
                    break;

                case "coordinator":
                    // Coordinador NO puede crear nuevas órdenes
                    NewOrderButton.IsEnabled = false;
                    NewOrderButton.Visibility = Visibility.Collapsed;

                    // Como ya no existe el botón de crear para el coordinador, 
                    // debemos mover el botón de refresh a la izquierda
                    RefreshButton.Width = 90;
                    NewOrderButton.ToolTip = "Solo el administrador puede crear órdenes";

                    // NO puede ver campos financieros
                    SubtotalColumn.Visibility = Visibility.Collapsed;
                    TotalColumn.Visibility = Visibility.Collapsed;
                    InvoicedColumn.Visibility = Visibility.Collapsed;
                    break;

                case "salesperson":
                    // Los vendedores no deberían poder acceder aquí
                    MessageBox.Show(
                        "No tiene permisos para acceder a este módulo.",
                        "Acceso Denegado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    this.Close();
                    break;
            }
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                StatusText.Text = "Cargando datos...";

                // Cargar clientes, vendedores y estados en paralelo
                var clientsTask = _supabaseService.GetClients();
                var vendorsTask = _supabaseService.GetVendors();
                var statusesTask = _supabaseService.GetOrderStatuses();

                await Task.WhenAll(clientsTask, vendorsTask, statusesTask);

                _clients = await clientsTask;
                _vendors = await vendorsTask;
                _orderStatuses = await statusesTask;

                System.Diagnostics.Debug.WriteLine($"✅ Datos cargados: {_clients.Count} clientes, {_vendors.Count} vendedores, {_orderStatuses.Count} estados");

                // Ahora cargar las órdenes
                await LoadOrders();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error cargando datos";
                MessageBox.Show(
                    $"Error al cargar datos iniciales:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }



        private async Task LoadOrders()
        {
            try
            {
                StatusText.Text = "Cargando órdenes...";
                _orders.Clear();

                // Cargar primero las 100 órdenes más recientes para velocidad
                var ordersFromDb = await _supabaseService.GetOrders(limit: 100, offset: 0);

                if (ordersFromDb != null && ordersFromDb.Count > 0)
                {
                    // Obtener los IDs de las órdenes para buscar sus facturas
                    var orderIds = ordersFromDb.Select(o => o.Id).ToList();

                    // Obtener totales facturados para todas las órdenes
                    var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(orderIds);

                    foreach (var order in ordersFromDb)
                    {
                        // Obtener nombre del cliente
                        var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);

                        // Obtener nombre del vendedor desde t_vendor
                        var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);

                        // Obtener nombre del estado
                        var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                        // Usar el método de extensión para convertir a ViewModel
                        var viewModel = order.ToViewModel(
                            clientName: client?.Name,
                            vendorName: vendor?.VendorName,
                            statusName: status?.Name
                        );

                        // Agregar el monto facturado
                        if (invoicedTotals.ContainsKey(order.Id))
                        {
                            viewModel.InvoicedAmount = invoicedTotals[order.Id];
                        }
                        else
                        {
                            viewModel.InvoicedAmount = 0;
                        }

                        _orders.Add(viewModel);
                    }

                    StatusText.Text = $"{_orders.Count} órdenes más recientes cargadas";

                    System.Diagnostics.Debug.WriteLine($"✅ {_orders.Count} órdenes cargadas correctamente");

                    // Cargar el resto en segundo plano si hay más de 100
                    if (ordersFromDb.Count == 100)
                    {
                        _ = LoadRemainingOrdersAsync();
                    }
                }
                else
                {
                    StatusText.Text = "No se encontraron órdenes";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error cargando órdenes";
                MessageBox.Show(
                    $"Error al cargar órdenes:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }
        }

        private async Task LoadRemainingOrdersAsync()
        {
            try
            {
                int offset = 100;
                int batchSize = 100;
                bool hasMore = true;

                while (hasMore)
                {
                    var moreOrders = await _supabaseService.GetOrders(limit: batchSize, offset: offset);

                    if (moreOrders != null && moreOrders.Count > 0)
                    {
                        // Obtener totales facturados para este batch
                        var orderIds = moreOrders.Select(o => o.Id).ToList();
                        var invoicedTotals = await _supabaseService.GetInvoicedTotalsByOrders(orderIds);

                        // Agregar al UI en el thread principal
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var order in moreOrders)
                            {
                                var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);
                                var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                                var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                                var viewModel = order.ToViewModel(
                                    clientName: client?.Name,
                                    vendorName: vendor?.VendorName,
                                    statusName: status?.Name
                                );

                                // Agregar el monto facturado
                                if (invoicedTotals.ContainsKey(order.Id))
                                {
                                    viewModel.InvoicedAmount = invoicedTotals[order.Id];
                                }

                                _orders.Add(viewModel);
                            }

                            StatusText.Text = $"{_orders.Count} órdenes cargadas";
                        });

                        offset += batchSize;
                        hasMore = moreOrders.Count == batchSize;
                    }
                    else
                    {
                        hasMore = false;
                    }

                    // Pequeña pausa para no saturar
                    await Task.Delay(100);
                }

                System.Diagnostics.Debug.WriteLine($"✅ Carga completa: {_orders.Count} órdenes totales");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando órdenes adicionales: {ex.Message}");
            }
        }

        

        private string GetRoleDisplayName(string role)
        {
            switch (role)
            {
                case "admin": return "Administrador";
                case "coordinator": return "Coordinador";
                case "salesperson": return "Vendedor";
                default: return role;
            }
        }

        // Event Handlers
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void NewOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "admin")
            {
                MessageBox.Show(
                    "Solo el administrador puede crear nuevas órdenes.",
                    "Permiso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Pasar el usuario actual a la ventana de nueva orden
                var newOrderWindow = new NewOrderWindow(_currentUser);
                newOrderWindow.Owner = this;

                if (newOrderWindow.ShowDialog() == true)
                {
                    // Recargar órdenes después de crear una nueva
                    _ = LoadOrders();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el formulario:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Actualizando...";

            // Deshabilitar controles durante la actualización
            RefreshButton.IsEnabled = false;
            NewOrderButton.IsEnabled = false;

            try
            {
                await LoadOrders();
            }
            finally
            {
                RefreshButton.IsEnabled = true;

                // Restaurar permisos del botón Nueva Orden según el rol
                if (_currentUser.Role == "admin")
                {
                    NewOrderButton.IsEnabled = true;
                }
            }
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.Trim();

            // Si no hay texto o es muy corto, cargar todas las órdenes
            if (string.IsNullOrWhiteSpace(searchText))
            {
                await LoadOrders();
                return;
            }

            // Aplicar filtro local en lugar de buscar en Supabase
            if (_ordersViewSource?.View != null)
            {
                _ordersViewSource.View.Filter = item =>
                {
                    var order = item as OrderViewModel;
                    if (order == null) return false;

                    var searchLower = searchText.ToLower();
                    return order.OrderNumber.ToLower().Contains(searchLower) ||
                           order.ClientName.ToLower().Contains(searchLower) ||
                           order.Description.ToLower().Contains(searchLower) ||
                           order.VendorName.ToLower().Contains(searchLower);
                };

                var count = _ordersViewSource.View.Cast<object>().Count();
                StatusText.Text = $"{count} órdenes encontradas";
            }
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ordersViewSource?.View == null) return;

            var selectedItem = (ComboBoxItem)StatusFilter.SelectedItem;
            var filterText = selectedItem?.Content?.ToString();

            _ordersViewSource.View.Filter = item =>
            {
                if (filterText == "Todos")
                    return true;

                var order = item as OrderViewModel;
                return order?.Status == filterText;
            };

            UpdateStatusBar();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var order = button?.Tag as OrderViewModel;

            if (order == null) return;

            try
            {
                // Abrir ventana de edición con los permisos del usuario actual
                var editWindow = new EditOrderWindow(order, _currentUser);
                editWindow.Owner = this;

                if (editWindow.ShowDialog() == true)
                {
                    // Recargar órdenes después de editar
                    _ = LoadOrders();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el editor:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.Role != "admin")
            {
                MessageBox.Show(
                    "Solo el administrador puede eliminar órdenes.",
                    "Permiso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var order = button?.Tag as OrderViewModel;

            if (order == null) return;

            var result = MessageBox.Show(
                $"¿Está seguro que desea eliminar la orden {order.OrderNumber}?\n\n" +
                "Esta acción no se puede deshacer.",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Por ahora, solo remover de la lista local
                    // TODO: Implementar eliminación en Supabase cuando sea necesario

                    _orders.Remove(order);
                    UpdateStatusBar();

                    MessageBox.Show(
                        "Orden marcada para eliminación.\n" +
                        "(Nota: La eliminación real está deshabilitada por seguridad)",
                        "Información",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error al eliminar la orden:\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStatusBar()
        {
            var visibleCount = _ordersViewSource?.View?.Cast<object>().Count() ?? 0;
            StatusText.Text = $"{visibleCount} órdenes visibles de {_orders.Count} total";
        }

        private async void InvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Verificar permisos - Solo Admin puede gestionar facturas
            if (_currentUser.Role != "admin")
            {
                MessageBox.Show(
                    "Solo el administrador puede gestionar facturas.",
                    "Acceso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var button = sender as Button;
            var order = button?.Tag as OrderViewModel;

            if (order == null) return;

            try
            {
                // Verificar que la orden esté en estado correcto para facturar
                bool canInvoice = await _supabaseService.CanCreateInvoice(order.Id);

                if (!canInvoice)
                {
                    // Obtener el estado actual
                    var currentStatus = order.Status;

                    string message = currentStatus switch
                    {
                        "CREADA" => "La orden debe estar EN PROCESO para poder facturar.\n" +
                                   "Cambie el estado de la orden primero.",
                        
                        "CANCELADA" => "Esta orden está cancelada.\n" +
                                     "No se pueden gestionar facturas.",
                        _ => $"No se pueden gestionar facturas en el estado: {currentStatus}"
                    };

                    MessageBox.Show(
                        message,
                        "Facturación No Disponible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Abrir ventana de gestión de facturas
                var invoiceWindow = new InvoiceManagementWindow(order.Id, _currentUser);
                invoiceWindow.ShowDialog();

                // Recargar órdenes para actualizar estados y montos facturados
                await LoadOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir gestión de facturas:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }
        }


        
    }
}