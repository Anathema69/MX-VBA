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

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            InitializeUI();
            StartClock();
            ConfigurePermissions();
        }

        private void MaximizeWithTaskbar()
        {
            // Obtener el área de trabajo (sin incluir la barra de tareas)
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
        }



        private void InitializeUI()
        {
            // Configurar información del usuario
            UserInfoText.Text = $"Usuario: {_currentUser.FullName}";
            RoleText.Text = $"Rol: {GetRoleDisplayName(_currentUser.Role)}";

            // Título de la ventana
            this.Title = $"IMA Mecatrónica - {_currentUser.FullName}";

            // Título del departamento centrado
            DepartmentTitle.Text = GetDepartmentTitle(_currentUser.Role);

            // Versión dinámica desde el assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private string GetDepartmentTitle(string role)
        {
            return role switch
            {
                "direccion" => "DIRECCIÓN",
                "administracion" => "ADMINISTRACIÓN",
                "ventas" => "VENTAS",
                "proyectos" => "PROYECTOS",
                "coordinacion" => "COORDINACIÓN",
                _ => "SISTEMA"
            };
        }

        private void ConfigurePermissions()
        {
            // Configurar permisos según el rol
            // Roles v2.0: direccion, administracion, proyectos, coordinacion, ventas
            switch (_currentUser.Role)
            {
                case "direccion":
                    // Direccion tiene acceso completo a todos los módulos
                    OrdersModuleButton.IsEnabled = true;
                    VendorPortalButton.IsEnabled = true;
                    VendorPortalButton.Visibility = Visibility.Visible;
                    CalendarButton.Visibility = Visibility.Visible;
                    UserPortalButton.Visibility = Visibility.Visible;
                    break;

                case "administracion":
                    // Administracion accede a órdenes y calendario, pero no a vendedores ni usuarios
                    OrdersModuleButton.IsEnabled = true;
                    VendorPortalButton.Visibility = Visibility.Collapsed;
                    CalendarButton.Visibility = Visibility.Visible;
                    UserPortalButton.Visibility = Visibility.Collapsed;
                    break;

                case "coordinacion":
                case "proyectos":
                    // Coordinación y Proyectos acceden a órdenes solamente
                    OrdersModuleButton.IsEnabled = true;
                    VendorPortalButton.Visibility = Visibility.Collapsed;
                    CalendarButton.Visibility = Visibility.Collapsed;
                    UserPortalButton.Visibility = Visibility.Collapsed;
                    break;

                case "ventas":
                    // Ventas accede a órdenes solamente
                    OrdersModuleButton.IsEnabled = true;
                    VendorPortalButton.Visibility = Visibility.Collapsed;
                    CalendarButton.Visibility = Visibility.Collapsed;
                    UserPortalButton.Visibility = Visibility.Collapsed;
                    break;

                default:
                    // Rol desconocido - mostrar advertencia y dar acceso mínimo a órdenes
                    System.Diagnostics.Debug.WriteLine($"[WARNING] Rol no reconocido: '{_currentUser.Role}'");
                    OrdersModuleButton.IsEnabled = true;
                    VendorPortalButton.Visibility = Visibility.Collapsed;
                    CalendarButton.Visibility = Visibility.Collapsed;
                    UserPortalButton.Visibility = Visibility.Collapsed;

                    // Notificar al usuario del problema
                    MessageBox.Show(
                        $"Su rol '{_currentUser.Role}' no está configurado en el sistema.\n\n" +
                        "Se le ha asignado acceso básico al módulo de órdenes.\n" +
                        "Contacte al administrador para configurar sus permisos correctamente.",
                        "Rol No Reconocido",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;
            }
        }

        private string GetRoleDisplayName(string role)
        {
            switch (role)
            {
                // Roles v2.0
                case "direccion": return "Dirección";
                case "administracion": return "Administración";
                case "proyectos": return "Proyectos";
                case "coordinacion": return "Coordinación";
                case "ventas": return "Ventas";
                // Legacy (por compatibilidad)
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
            // Solo ventas no tiene acceso a órdenes
            if (_currentUser.Role == "ventas")
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
            _timer?.Stop();

            // Usar el método centralizado de App para cerrar sesión
            // Esto cierra TODAS las ventanas excepto la de login
            var app = (App)Application.Current;
            app.ForceLogout("Usuario cerró sesión manualmente");
        }

        protected override void OnClosed(EventArgs e)
        {
            // Detener el temporizador al cerrar la ventana

            _timer?.Stop();
            base.OnClosed(e);
        }

        // MÉTODO: Click en el Test Runner
        /*
         * Comentado porque solo el admin tendrá acceso a los tests y se hará desde el portal del vendedor
         * 
         *
        private void TestRunner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Abriendo Test Runner...";

                var testWindow = new TestRunnerWindow();
                testWindow.Show();

                StatusText.Text = "Test Runner abierto";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error abriendo Test Runner: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error abriendo Test Runner";
            }
        }

        */

        // MÉTODO IMPORTANTE: Click en el portal del vendedor
        private void VendorPortal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Abriendo Portal del Vendedor...";

                // Determinar qué ventana abrir según el rol
                // direccion tiene acceso completo al portal de vendedores
                // administracion NO tiene acceso (según requerimiento)
                // ventas ve solo sus comisiones
                if (_currentUser.Role == "direccion")
                {
                    // Dirección ve la ventana completa con edición
                    var vendorWindow = new VendorCommissionsWindow(_currentUser);
                    vendorWindow.Show();
                }
                else if (_currentUser.Role == "ventas")
                {
                    // Vendedor ve solo sus comisiones
                    // Por ahora usar la misma ventana, luego crearemos la específica
                    MessageBox.Show(
                        "Portal del vendedor en desarrollo. Por favor contacte al administrador.",
                        "En desarrollo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                else
                {
                    // administracion, coordinacion, proyectos no tienen acceso
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

        // MÉTODO IMPORTANTE: Click en el portal de proveedores
        private void OpenExpensePortal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Abriendo Cuentas por Pagar...";

                // Verificar permisos - direccion y administracion tienen acceso
                if (_currentUser.Role != "direccion" && _currentUser.Role != "administracion")
                {
                    MessageBox.Show(
                        "No tiene permisos para acceder al Portal de Proveedores.",
                        "Acceso Denegado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Abrir la nueva vista de Cuentas por Pagar (pivoteada por proveedor)
                var supplierPendingWindow = new SupplierPendingView(_currentUser);
                supplierPendingWindow.ShowDialog();

                StatusText.Text = "Cuentas por Pagar cerrado";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir Cuentas por Pagar:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error al abrir portal";
            }
        }

        // MÉTODO IMPORTANTE: Click en el botón de ingresos pendientes
        private void PendingIncomesButton_Click(object sender, RoutedEventArgs e)
        {
            // direccion y administracion tienen acceso
            if (_currentUser.Role != "direccion" && _currentUser.Role != "administracion")
            {
                MessageBox.Show("Solo Dirección y Administración pueden acceder a este módulo.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pendingIncomesWindow = new PendingIncomesView(_currentUser);
            pendingIncomesWindow.ShowDialog();
        }

        private void OpenPayroll_Click(object sender, RoutedEventArgs e) {
            // Verificar permisos - direccion y administracion tienen acceso
            if (_currentUser.Role != "direccion" && _currentUser.Role != "administracion") {
                MessageBox.Show("Solo Dirección y Administración pueden acceder a este módulo.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var payrollWindow = new PayrollManagementView(_currentUser);
            payrollWindow.ShowDialog();
        }

        // función para abrir la ventana de balance 'BalanceWindow.xaml' con 'Balance_Click'
        private void Balance_Click(object sender, RoutedEventArgs e) {
            // Verificar permisos - direccion y administracion tienen acceso
            if (_currentUser.Role != "direccion" && _currentUser.Role != "administracion") {
                MessageBox.Show("Solo Dirección y Administración pueden acceder a este módulo.",
                    "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var balanceWindow = new BalanceWindowPro(_currentUser);
            balanceWindow.ShowDialog();
        }

        // Calendario - disponible para direccion y administracion
        private void Calendar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Abriendo Calendario de Personal...";
                var calendarWindow = new CalendarView(_currentUser);
                calendarWindow.ShowDialog();
                StatusText.Text = "Sistema listo";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir el Calendario:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error al abrir calendario";
            }
        }

        // Portal de Usuarios - solo para direccion
        private void UserPortal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Abriendo Gestión de Usuarios...";
                var userWindow = new UserManagementWindow(_currentUser);
                userWindow.ShowDialog();
                StatusText.Text = "Sistema listo";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir gestión de usuarios: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Error al abrir módulo";
            }
        }

    }
}