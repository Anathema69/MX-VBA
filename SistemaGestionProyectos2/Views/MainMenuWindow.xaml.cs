using System;
using System.Windows;
using System.Windows.Threading;
using SistemaGestionProyectos2.Models;

namespace SistemaGestionProyectos2.Views
{
    public partial class MainMenuWindow : Window
    {
        private UserSession _currentUser;
        private DispatcherTimer _timer;

        // Constructor que recibe UserSession
        public MainMenuWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;

            InitializeUI();
            StartClock();
            ConfigurePermissions();
        }

        

        private void InitializeUI()
        {
            // Configurar información del usuario
            UserInfoText.Text = $"Usuario: {_currentUser.FullName}";
            RoleText.Text = $"Rol: {GetRoleDisplayName(_currentUser.Role)}";

            // Título de la ventana
            this.Title = $"IMA Mecatrónica - {_currentUser.FullName}";
        }

        private void ConfigurePermissions()
        {
            // Configurar permisos según el rol
            switch (_currentUser.Role)
            {
                case "admin":
                    // Admin tiene acceso a todo
                    OrdersModuleButton.IsEnabled = true;
                    VendorPortalButton.IsEnabled = true;
                    break;

                    // SOLO EL ADMIN TENDRÁ ACCESO AL MÓDULO DE ÓRDENES
                    // COORDINADOR IRÁN DIRECTO AL MÓDULO DE ÓRDENES QUE SE MANEJARÁ DESDE EL LOGIN
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

        private void StartClock()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            _timer.Start();
        }

        // MÉTODO IMPORTANTE: Click en el módulo de órdenes
        private void OrdersModule_Click(object sender, RoutedEventArgs e)
        {
            // Verificar permisos una vez más
            if (_currentUser.Role == "salesperson")
            {
                MessageBox.Show(
                    "No tiene permisos para acceder al módulo de órdenes.",
                    "Acceso Denegado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Abriendo módulo de órdenes...";

            try
            {
                // Abrir ventana de órdenes
                OrdersManagementWindow ordersWindow = new OrdersManagementWindow(_currentUser);
                ordersWindow.Show();

                // Opcional: Cerrar menú principal o dejarlo abierto
                // this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el módulo de órdenes:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // MÉTODO IMPORTANTE: Click en logout
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "¿Está seguro que desea cerrar sesión?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _timer?.Stop();

                // Volver al login
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();

                // Asegurar cerrar esta ventana, porque esta es la principal
                this.Close();

                // en algunos casos a pesar de haber confirmado cerrar la sesión cuándo se vuelve de la grilla de órdenes la venta del main no se cierra
                // si la venta de login está abierta, forzar el cierre de esta ventana
                if (Application.Current.Windows.Count > 1)
                {
                    foreach (var window in Application.Current.Windows)
                    {
                        if (window is MainMenuWindow mainMenu && mainMenu != this)
                        {
                            mainMenu.Close();
                        }
                    }
                }

            }


        }

        protected override void OnClosed(EventArgs e)
        {
            // Detener el temporizador al cerrar la ventana

            _timer?.Stop();
            base.OnClosed(e);
        }

        // MÉTODO IMPORTANTE: Click en el portal del vendedor
        private void VendorPortal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Abriendo Portal del Vendedor...";

                // Determinar qué ventana abrir según el rol
                if (_currentUser.Role == "admin")
                {
                    // Admin ve la ventana completa con edición
                    var adminPortal = new VendorPortalAdminWindow(_currentUser);
                    adminPortal.Show();
                }
                else if (_currentUser.Role == "salesperson")
                {
                    // Vendedor ve solo sus comisiones
                    // Por ahora usar la misma ventana, luego crearemos la específica
                    MessageBox.Show(
                        "Portal del vendedor en desarrollo. Por favor contacte al administrador.",
                        "En desarrollo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;

                    // Cuando esté lista:
                    // var vendorPortal = new VendorPortalWindow(_currentUser);
                    // vendorPortal.Show();
                }
                else
                {
                    MessageBox.Show(
                        "No tiene permisos para acceder al Portal del Vendedor.",
                        "Acceso Denegado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Portal del Vendedor abierto";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el Portal del Vendedor:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error al abrir portal";
            }
        }
    }
}