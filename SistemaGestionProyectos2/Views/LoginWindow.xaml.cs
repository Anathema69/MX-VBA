using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Models;

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
            var result = MessageBox.Show(
                "¿Está seguro que desea salir del sistema?",
                "IMA Mecatrónica",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        // Botón de Login - AHORA CON SUPABASE
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Validación
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowStatus("⚠️", "Por favor complete todos los campos", "#FFA726", false);
                return;
            }

            ShowLoading("Verificando credenciales...");
            DisableControls();

            try
            {
                string username = UsernameTextBox.Text.Trim();
                string password = PasswordBox.Password;

                // AUTENTICACIÓN CON SUPABASE
                var (success, user, message) = await _supabaseService.AuthenticateUser(username, password);

                if (success && user != null)
                {
                    ShowStatus("✅", "Acceso autorizado", "#4CAF50", true);
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

                    // Log para debug
                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso: {user.FullName} ({user.Role})");

                    // Crear ventana de carga
                    var loadingWindow = new LoadingWindow();
                    loadingWindow.Show();
                    this.Hide();

                    // Simular carga
                    loadingWindow.UpdateStatus("Preparando Sistema", $"Bienvenido {user.FullName}");
                    await Task.Delay(800);

                    loadingWindow.UpdateStatus("Cargando Módulos", "Configurando permisos...");
                    await Task.Delay(600);

                    // Abrir menú principal
                    MainMenuWindow mainMenu = new MainMenuWindow(currentUser);
                    mainMenu.Show();

                    await loadingWindow.CloseWithFade();
                    this.Close();
                }
                else
                {
                    ShowStatus("❌", message ?? "Credenciales incorrectas", "#F44336", false);

                    // Mostrar mensaje más visible
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

                MessageBox.Show(
                    "No se pudo conectar con el servidor.\n\n" +
                    "Posibles causas:\n" +
                    "• Sin conexión a internet\n" +
                    "• Servidor no disponible\n" +
                    "• Error en configuración\n\n" +
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
        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DisableControls();
                ShowLoading("Probando conexión con Supabase...");

                // Intentar obtener clientes como prueba
                var clients = await _supabaseService.GetClients();

                if (clients != null)
                {
                    ShowStatus("✅", $"Conexión exitosa - {clients.Count} clientes en BD", "#4CAF50", true);
                }
                else
                {
                    ShowStatus("⚠️", "Conexión establecida pero sin datos", "#FFA726", false);
                }

                await Task.Delay(2000);
                LoadingPanel.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowStatus("❌", $"Error: {ex.Message}", "#F44336", false);
            }
            finally
            {
                EnableControls();
            }
        }
    }
}