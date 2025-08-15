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

            // Debug para verificar el rol
            System.Diagnostics.Debug.WriteLine($"🔍 Usuario actual: {_currentUser.FullName}, Rol: {_currentUser.Role}");
            System.Diagnostics.Debug.WriteLine($"🔍 Window.Tag establecido a: {this.Tag}");
        }

        private void ConfigurePermissions()
        {
            // Configurar visibilidad y permisos según el rol
            switch (_currentUser.Role)
            {
                case "admin":
                    // Admin puede ver y editar todo
                    NewOrderButton.IsEnabled = true;
                    SubtotalColumn.Visibility = Visibility.Visible;
                    TotalColumn.Visibility = Visibility.Visible;
                    OrderPercentageColumn.Visibility = Visibility.Visible;
                    EnableDeleteButtons(true);
                    break;

                case "coordinator":
                    // Coordinador NO puede crear nuevas órdenes
                    NewOrderButton.IsEnabled = false;
                    NewOrderButton.ToolTip = "Solo el administrador puede crear órdenes";

                    // NO puede ver campos financieros
                    SubtotalColumn.Visibility = Visibility.Collapsed;
                    TotalColumn.Visibility = Visibility.Collapsed;
                    OrderPercentageColumn.Visibility = Visibility.Collapsed;

                    // NO puede eliminar
                    EnableDeleteButtons(false);
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

        // Actualizar el método LoadOrders en OrdersManagementWindow.xaml.cs

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
                    foreach (var order in ordersFromDb)
                    {
                        // Obtener nombre del cliente
                        var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);

                        // Obtener nombre del vendedor desde t_vendor (usando f_salesman)
                        var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);

                        // Obtener nombre del estado
                        var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                        // Usar el método de extensión para convertir a ViewModel
                        var viewModel = order.ToViewModel(
                            clientName: client?.Name,
                            vendorName: vendor?.VendorName,
                            statusName: status?.Name
                        );

                        _orders.Add(viewModel);
                    }

                    StatusText.Text = $"{_orders.Count} órdenes más recientes cargadas";

                    // AGREGAR ESTA LÍNEA AL FINAL
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ConfigureButtonsVisibility();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);

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
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontraron órdenes en la BD");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error al cargar órdenes";
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando órdenes: {ex.Message}");

                MessageBox.Show(
                    $"Error al cargar órdenes:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
                        // Agregar al UI en el thread principal
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var order in moreOrders)
                            {
                                var client = _clients?.FirstOrDefault(c => c.Id == order.ClientId);
                                var vendor = _vendors?.FirstOrDefault(v => v.Id == order.SalesmanId);
                                var status = _orderStatuses?.FirstOrDefault(s => s.Id == order.OrderStatus);

                                // Usar el método de extensión
                                var viewModel = order.ToViewModel(
                                    clientName: client?.Name,
                                    vendorName: vendor?.VendorName,
                                    statusName: status?.Name
                                );

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

        private void EnableDeleteButtons(bool enable)
        {
            // Esta función habilitará los botones de eliminar en el DataGrid
            // Se aplicará cuando se carguen los datos
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

        private void InvoiceButton_Click(object sender, RoutedEventArgs e)
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
                // Abrir ventana de gestión de facturas
                var invoiceWindow = new InvoiceManagementWindow(order.Id, _currentUser);
                invoiceWindow.Owner = this;
                invoiceWindow.ShowDialog();

                // Opcional: Recargar órdenes para actualizar cualquier cambio
                // _ = LoadOrders();
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

        // Agregar este método en OrdersManagementWindow.xaml.cs

        // Método para configurar la visibilidad de los botones después de cargar el DataGrid
        private void ConfigureButtonsVisibility()
        {
            // Si no es admin, ocultar el botón de facturas en todas las filas
            if (_currentUser.Role != "admin")
            {
                // Ocultar la columna completa de facturas es más eficiente
                foreach (var column in OrdersDataGrid.Columns)
                {
                    if (column is DataGridTemplateColumn templateColumn &&
                        templateColumn.Header?.ToString() == "ACCIONES")
                    {
                        // Necesitamos modificar el template
                        OrdersDataGrid.UpdateLayout();

                        // Iterar por todas las filas
                        foreach (var item in OrdersDataGrid.Items)
                        {
                            var row = OrdersDataGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                            if (row != null)
                            {
                                // Buscar el botón de facturas en la fila
                                var presenter = GetVisualChild<DataGridCellsPresenter>(row);
                                if (presenter != null)
                                {
                                    // Obtener la celda de acciones (última columna)
                                    var cell = presenter.ItemContainerGenerator.ContainerFromIndex(
                                        OrdersDataGrid.Columns.Count - 1) as DataGridCell;

                                    if (cell != null)
                                    {
                                        // Buscar el StackPanel dentro de la celda
                                        var stackPanel = GetVisualChild<StackPanel>(cell);
                                        if (stackPanel != null && stackPanel.Children.Count > 1)
                                        {
                                            // El segundo botón es el de facturas (índice 1)
                                            if (stackPanel.Children[1] is Button invoiceButton)
                                            {
                                                invoiceButton.Visibility = Visibility.Collapsed;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Métodos helper para buscar elementos visuales
        private T GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                var v = VisualTreeHelper.GetChild(parent, i);
                child = v as T ?? GetVisualChild<T>(v);
                if (child != null)
                    break;
            }
            return child;
        }
    }
}