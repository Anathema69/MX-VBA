using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class LoginWindow : Window
    {
        private readonly SupabaseService _supabaseService;

        public LoginWindow()
        {
            try
            {
                InitializeComponent();
                _supabaseService = SupabaseService.Instance;

                // Quitar valores por defecto en producción
                UsernameTextBox.Text = "";
                PasswordBox.Password = "";

                // Enfocar el campo de usuario
                UsernameTextBox.Focus();

                // Cerrar cualquier otra ventana abierta
                foreach (Window window in Application.Current.Windows)
                {
                    if (window != this)
                        window.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error inicializando conexión con base de datos:\n{ex.Message}\n\n" +
                    "Verifique su conexión a internet y configuración.",
                    "Error de Inicialización",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Mover ventana
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // Minimizar ventana
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Cerrar aplicación
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var logger = JsonLoggerService.Instance;

            // Validación
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowStatus("⚠️", "Por favor complete todos los campos", "#FFA726", false);
                logger.LogWarning("AUTH", "LOGIN_VALIDATION_FAILED", new { reason = "Empty fields" });
                return;
            }

            ShowLoading("Verificando credenciales...");
            DisableControls();

            try
            {
                string username = UsernameTextBox.Text.Trim();
                string password = PasswordBox.Password;

                logger.LogInfo("AUTH", "LOGIN_ATTEMPT", new { username });

                // AUTENTICACIÓN CON SUPABASE
                var (success, user, message) = await _supabaseService.AuthenticateUser(username, password);

                if (success && user != null)
                {
                    ShowStatus("✅", "Acceso autorizado", "#4CAF50", true);

                    // Log de login exitoso
                    logger.LogLogin(username, true, user.Id.ToString(), user.Role);

                    await Task.Delay(500);

                    // Crear sesión de usuario
                    var currentUser = new UserSession
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName,
                        Role = user.Role,
                        LoginTime = DateTime.Now
                    };

                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso: {user.FullName} ({user.Role})");

                    // INICIAR MONITOREO DE TIMEOUT DE SESIÓN
                    var timeoutService = Services.SessionTimeoutService.Instance;
                    timeoutService.Start();

                    System.Diagnostics.Debug.WriteLine($"🔐 Timeout service iniciado (IsRunning: {timeoutService.IsRunning})");

                    // Crear ventana de carga
                    var loadingWindow = new LoadingWindow();
                    loadingWindow.Show();
                    this.Hide();

                    // Simular carga
                    loadingWindow.UpdateStatus("Preparando Sistema", $"Bienvenido {user.FullName}");
                    await Task.Delay(800);

                    loadingWindow.UpdateStatus("Cargando Módulos", "Configurando permisos...");
                    await Task.Delay(600);

                    
                    // SI ES admin

                    if (user.Role == "admin")
                    {
                        // Admin abre el menú principal
                        MainMenuWindow mainMenu = new MainMenuWindow(currentUser);
                        mainMenu.Show();
                        
                    }
                    else if (user.Role == "coordinator")
                    {
                        // Coordinador va directo al módulo de órdenes
                        OrdersManagementWindow ordersWindow = new OrdersManagementWindow(currentUser);
                        ordersWindow.Show();

                    }
                    // si es vendedor
                    else if (user.Role == "salesperson")
                    {
                        // Vendedor abre el portal del vendedor
                        VendorDashboard vendorPortal = new VendorDashboard(currentUser);
                        vendorPortal.Show();

                    }
                    else
                    {
                        // Rol desconocido
                        MessageBox.Show(
                            $"Su rol '{user.Role}' no tiene acceso a ninguna sección.\nContacte al administrador.",
                            "Rol Desconocido",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        logger.LogWarning("AUTH", "LOGIN_UNKNOWN_ROLE", new { username, role = user.Role }, user.Id.ToString());
                        PasswordBox.Clear();
                        PasswordBox.Focus();
                        this.Show();
                        await loadingWindow.CloseWithFade();
                        return;
                    }

                    await loadingWindow.CloseWithFade();
                    this.Close();

                }
                else
                {
                    ShowStatus("❌", message ?? "Credenciales incorrectas", "#F44336", false);

                    // Log de login fallido
                    logger.LogLogin(username, false, null, null);

                    MessageBox.Show(
                        message ?? "Usuario o contraseña incorrectos.\n\nPor favor verifique sus credenciales.",
                        "Error de Autenticación",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Limpiar contraseña
                    PasswordBox.Clear();
                    PasswordBox.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowStatus("⚠️", "Error de conexión", "#F44336", false);

                logger.LogError("AUTH", "LOGIN_ERROR", new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });

                MessageBox.Show(
                    "No se pudo conectar con el servidor.\n\n" +
                    $"Detalles: {ex.Message}",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            }
            finally
            {
                EnableControls();
            }
        }

        // Helpers para UI
        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingPanel.Visibility = Visibility.Visible;
            StatusPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowStatus(string icon, string message, string colorHex, bool isSuccess)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            StatusPanel.Visibility = Visibility.Visible;

            StatusIcon.Text = icon;
            StatusText.Text = message;

            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var brush = new SolidColorBrush(color);

            StatusIcon.Foreground = brush;
            StatusText.Foreground = brush;
            StatusPanel.Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B));
        }

        private void DisableControls()
        {
            UsernameTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            LoginButton.IsEnabled = false;
            TestConnectionButton.IsEnabled = false;
        }

        private void EnableControls()
        {
            UsernameTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            LoginButton.IsEnabled = true;
            TestConnectionButton.IsEnabled = true;
        }

        // Permitir login con Enter
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Return && LoginButton.IsEnabled)
            {
                LoginButton_Click(this, new RoutedEventArgs());
            }
            base.OnKeyDown(e);
        }

        // Botón de test de conexión
        // Versión MINIMALISTA del método TestConnectionButton_Click
        // Reemplazar el método TestConnectionButton_Click en LoginWindow.xaml.cs

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DisableControls();
                ShowLoading("Probando conexión con base de datos...");

                // Intentar conexión simple con Supabase
                bool isConnected = await _supabaseService.TestConnection();

                if (isConnected)
                {
                    // Intentar obtener el conteo de clientes como prueba adicional
                    try
                    {
                        var clients = await _supabaseService.GetClients();

                        ShowStatus("✅", "Conexión exitosa", "#4CAF50", true);

                        await Task.Delay(500); // Breve pausa para que se vea el status

                        // Mostrar ventana simple de confirmación
                        MessageBox.Show(
                            $"✅ Conexión establecida correctamente\n\n" +
                            $"Base de datos: Supabase\n" +
                            $"Estado: Operativa\n" +
                            $"Clientes registrados: {clients?.Count ?? 0}\n" +
                            $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                            "Test de Conexión Exitoso",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        // Conexión OK pero error al obtener datos
                        ShowStatus("⚠️", "Conexión parcial", "#FFA726", false);

                        MessageBox.Show(
                            $"⚠️ Conexión establecida pero con advertencias\n\n" +
                            $"La conexión a la base de datos funciona, pero hubo un problema al obtener datos de prueba.\n\n" +
                            $"Detalles: {ex.Message}",
                            "Test de Conexión - Advertencia",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    ShowStatus("❌", "Sin conexión", "#F44336", false);

                    MessageBox.Show(
                        "❌ No se pudo establecer conexión\n\n" +
                        "Posibles causas:\n" +
                        "• Sin conexión a internet\n" +
                        "• Servidor no disponible\n" +
                        "• Credenciales incorrectas en configuración\n\n" +
                        "Verifique su conexión e intente nuevamente.",
                        "Test de Conexión Fallido",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                // Limpiar el estado después de 2 segundos
                await Task.Delay(2000);
                LoadingPanel.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowStatus("❌", "Error en test", "#F44336", false);

                MessageBox.Show(
                    $"❌ Error durante el test de conexión\n\n" +
                    $"Mensaje: {ex.Message}\n\n" +
                    "Por favor, verifique:\n" +
                    "• El archivo appsettings.json existe\n" +
                    "• Las credenciales de Supabase están configuradas\n" +
                    "• Tiene conexión a internet",
                    "Error en Test",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Error en test de conexión: {ex}");
            }
            finally
            {
                EnableControls();
            }
        }

    }
}