using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class OrdersManagementWindow : Window
    {
        private UserSession _currentUser;
        private ObservableCollection<OrderViewModel> _orders;
        private CollectionViewSource _ordersViewSource;
        private readonly SupabaseService _supabaseService;

        // Constructor que recibe el usuario actual
        public OrdersManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _orders = new ObservableCollection<OrderViewModel>();
            //_supabaseService = SupabaseService.Instance;

            InitializeUI();
            ConfigurePermissions();

            // Test de conexión
            //TestSupabaseConnection();

            LoadOrders();
        }

        private void InitializeUI()
        {
            // Configurar información del usuario
            UserStatusText.Text = $"Usuario: {_currentUser.FullName} ({GetRoleDisplayName(_currentUser.Role)})";

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
                    // Admin puede ver y editar todo
                    NewOrderButton.IsEnabled = true;
                    SubtotalColumn.Visibility = Visibility.Visible;
                    TotalColumn.Visibility = Visibility.Visible;
                    OrderPercentageColumn.Visibility = Visibility.Visible;

                    // Admin puede eliminar órdenes (opcional)
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

        private void EnableDeleteButtons(bool enable)
        {
            // Esta función habilitará los botones de eliminar en el DataGrid
            // Se aplicará cuando se carguen los datos
        }

        private async void LoadOrders()
        {
            try
            {
                StatusText.Text = "Cargando órdenes...";

                // Limpiar la colección actual
                _orders.Clear();

                // Por ahora, cargar directamente datos de ejemplo
                LoadSampleOrders();

                /* CÓDIGO PARA SUPABASE (comentado por ahora)
                try 
                {
                    // Verificar si tenemos servicio de Supabase
                    if (_supabaseService != null)
                    {
                        var ordersFromDb = await _supabaseService.GetOrders();

                        if (ordersFromDb != null && ordersFromDb.Count > 0)
                        {
                            foreach (var order in ordersFromDb)
                            {
                                _orders.Add(new OrderViewModel
                                {
                                    Id = order.Id,
                                    OrderNumber = order.OrderNumber,
                                    OrderDate = order.OrderDate,
                                    ClientName = order.ClientName,
                                    Description = order.Description,
                                    VendorName = order.VendorName,
                                    PromiseDate = order.PromiseDate,
                                    ProgressPercentage = order.ProgressPercentage,
                                    OrderPercentage = order.OrderPercentage,
                                    Subtotal = order.Subtotal,
                                    Total = order.Total,
                                    Status = order.Status,
                                    Invoiced = order.Invoiced,
                                    LastInvoiceDate = order.LastInvoiceDate
                                });
                            }

                            UpdateStatusBar();
                            StatusText.Text = $"{_orders.Count} órdenes cargadas desde el servidor";
                            return; // Si cargó de BD, salir
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cargando de Supabase: {ex.Message}");
                }

                // Si llegamos aquí, cargar datos de ejemplo
                LoadSampleOrders();
                */
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error al cargar órdenes";
                MessageBox.Show(
                    $"Error al cargar órdenes:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LoadSampleOrders()
        {
            _orders.Clear();

            // Datos de ejemplo
            var sampleOrders = new List<OrderViewModel>
            {
                new OrderViewModel
                {
                    Id = 1,
                    OrderNumber = "051124",
                    OrderDate = new DateTime(2024, 11, 1),
                    ClientName = "Ventas Industriales",
                    Description = "Rodillo",
                    VendorName = "MARIO GARZA",
                    PromiseDate = new DateTime(2025, 8, 12),
                    ProgressPercentage = 45,
                    OrderPercentage = 30,
                    Subtotal = 40353.60m,
                    Total = 40353.60m * 1.16m,
                    Status = "EN PROCESO"
                },
                new OrderViewModel
                {
                    Id = 2,
                    OrderNumber = "2450045194",
                    OrderDate = new DateTime(2024, 12, 1),
                    ClientName = "BorgWarner",
                    Description = "Gauge para tubo",
                    VendorName = "CYNTHIA GARCÍA",
                    PromiseDate = new DateTime(2025, 8, 12),
                    ProgressPercentage = 75,
                    OrderPercentage = 60,
                    Subtotal = 5568.00m,
                    Total = 5568.00m * 1.16m,
                    Status = "EN PROCESO"
                },
                new OrderViewModel
                {
                    Id = 3,
                    OrderNumber = "G000130110",
                    OrderDate = new DateTime(2025, 1, 1),
                    ClientName = "Lennox",
                    Description = "Engrane tapa brazo, plato aluminio",
                    VendorName = "JEHU ARREDONDO",
                    PromiseDate = new DateTime(2025, 9, 15),
                    ProgressPercentage = 20,
                    OrderPercentage = 10,
                    Subtotal = 11623.20m,
                    Total = 11623.20m * 1.16m,
                    Status = "EN PROCESO"
                }
            };

            foreach (var order in sampleOrders)
            {
                _orders.Add(order);
            }

            UpdateStatusBar();
            StatusText.Text = $"{_orders.Count} órdenes de ejemplo cargadas (modo offline)";
        }

        private void UpdateStatusBar()
        {
            StatusText.Text = $"{_orders.Count} órdenes cargadas";
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
                // Abrir formulario de nueva orden
                var newOrderWindow = new NewOrderWindow();
                newOrderWindow.Owner = this;

                if (newOrderWindow.ShowDialog() == true)
                {
                    // Si se guardó exitosamente, actualizar la lista
                    MessageBox.Show(
                        "La orden se agregó a la lista (modo offline).",
                        "Información",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Opcional: agregar la orden a la lista local
                    // _orders.Add(new OrderViewModel { ... });
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
                // Simular actualización
                await Task.Delay(500);
                LoadOrders(); // Esto ahora carga datos de ejemplo
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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ordersViewSource?.View == null) return;

            var searchText = SearchBox.Text.ToLower();

            _ordersViewSource.View.Filter = item =>
            {
                if (string.IsNullOrWhiteSpace(searchText))
                    return true;

                var order = item as OrderViewModel;
                if (order == null) return false;

                return order.OrderNumber.ToLower().Contains(searchText) ||
                       order.ClientName.ToLower().Contains(searchText) ||
                       order.Description.ToLower().Contains(searchText);
            };

            UpdateStatusBar();
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
                    // La orden ya fue actualizada en el objeto
                    // Refrescar el DataGrid para mostrar los cambios
                    OrdersDataGrid.Items.Refresh();

                    // Actualizar status bar
                    StatusText.Text = $"Orden {order.OrderNumber} actualizada exitosamente";

                    // Opcional: Si estuviéramos conectados a Supabase, aquí se guardaría
                    // await _supabaseService.UpdateOrder(order);
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
                    // TODO: Implementar eliminación en Supabase
                    // await _supabaseService.DeleteOrder(order.Id);

                    _orders.Remove(order);
                    UpdateStatusBar();

                    MessageBox.Show(
                        "Orden eliminada correctamente.",
                        "Éxito",
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

        private async void TestSupabaseConnection()
        {
            try
            {
                var supabase = SupabaseService.Instance;

                // Intento simple de query
                var client = supabase.GetClient();
                if (client != null)
                {
                    // Query directa usando el modelo OrderDb
                    var result = await client
                        .From<OrderDb>()  // Usar el modelo definido
                        .Select("*")
                        .Limit(10)
                        .Get();

                    MessageBox.Show($"Conexión exitosa. Registros encontrados: {result?.Models?.Count ?? 0}",
                        "Test Supabase",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Mostrar algunos detalles de la primera orden si existe
                    if (result?.Models?.Count > 0)
                    {
                        var firstOrder = result.Models.First();
                        MessageBox.Show(
                            $"Primera orden:\n" +
                            $"ID: {firstOrder.Id}\n" +
                            $"Número: {firstOrder.OrderNumber}\n" +
                            $"Fecha: {firstOrder.OrderDate}",
                            "Datos de Prueba",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Cliente de Supabase no inicializado",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de conexión:\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}