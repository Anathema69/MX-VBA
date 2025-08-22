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
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Detener el temporizador al cerrar la ventana

            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}