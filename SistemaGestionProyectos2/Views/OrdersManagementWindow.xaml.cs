using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SistemaGestionProyectos2.Models;

namespace SistemaGestionProyectos2.Views
{
    public partial class OrdersManagementWindow : Window
    {
        private UserSession _currentUser;
        private ObservableCollection<OrderViewModel> _orders;
        private CollectionViewSource _ordersViewSource;

        // Constructor que recibe el usuario actual
        public OrdersManagementWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            _orders = new ObservableCollection<OrderViewModel>();

            InitializeUI();
            ConfigurePermissions();
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

        private void LoadOrders()
        {
            // Cargar órdenes de prueba (después se conectará con Supabase)
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

            // Aquí abriremos el formulario de nueva orden (Fase 3)
            MessageBox.Show(
                "Formulario de Nueva Orden - En desarrollo\n\nAquí se abrirá el formulario para crear una nueva orden.",
                "Nueva Orden",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Actualizando...";
            LoadOrders();
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

            // Verificar permisos para editar
            string message = "";
            if (_currentUser.Role == "coordinator")
            {
                message = $"Editando Orden: {order.OrderNumber}\n\n" +
                         "Como Coordinador, puede editar:\n" +
                         "• Fecha Promesa\n" +
                         "• % Avance\n" +
                         "• Estatus";
            }
            else if (_currentUser.Role == "admin")
            {
                message = $"Editando Orden: {order.OrderNumber}\n\n" +
                         "Como Administrador, puede editar todos los campos.";
            }

            // Por ahora mostrar mensaje (en Fase 3 se abrirá formulario de edición)
            MessageBox.Show(
                message + "\n\n(Formulario de edición en desarrollo)",
                "Editar Orden",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
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
                $"¿Está seguro que desea eliminar la orden {order.OrderNumber}?",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _orders.Remove(order);
                UpdateStatusBar();

                // Aquí se eliminaría de la base de datos
                MessageBox.Show(
                    "Orden eliminada correctamente.",
                    "Éxito",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}