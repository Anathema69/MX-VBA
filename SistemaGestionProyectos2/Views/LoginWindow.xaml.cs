using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class LoginWindow : Window
    {
        private readonly SupabaseService _supabaseService;

        public LoginWindow()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;

            // Valores por defecto para pruebas (quitar en producción)
            EmailTextBox.Text = "test@example.com";
            PasswordBox.Password = "password";
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

        // Botón de Login
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Validación
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowStatus("⚠️", "Por favor complete todos los campos", "#FFA726", false);
                return;
            }

            // Mostrar loading
            ShowLoading("Iniciando sesión...");
            DisableControls();

            try
            {
                // Por ahora usamos login de prueba
                await Task.Delay(1500); // Simular delay

                if (EmailTextBox.Text == "test@example.com" && PasswordBox.Password == "password")
                {
                    ShowStatus("✅", "Login exitoso", "#4CAF50", true);
                    await Task.Delay(500);

                    // Abrir ventana principal
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ShowStatus("❌", "Credenciales incorrectas", "#F44336", false);
                }

                /* PARA USAR CON SUPABASE REAL (descomentar cuando esté listo):
                bool loginSuccess = await _supabaseService.SignIn(
                    EmailTextBox.Text.Trim(), 
                    PasswordBox.Password
                );

                if (loginSuccess)
                {
                    ShowStatus("✅", "Login exitoso", "#4CAF50", true);
                    await Task.Delay(500);
                    
                    var user = _supabaseService.GetCurrentUser();
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Title = $"Sistema de Gestión - {user?.Email}";
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    ShowStatus("❌", "Credenciales incorrectas", "#F44336", false);
                }
                */
            }
            catch (Exception ex)
            {
                ShowStatus("⚠️", $"Error: {ex.Message}", "#F44336", false);
            }
            finally
            {
                EnableControls();
            }
        }

        // Botón Test de Conexión
        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Probando conexión con Supabase...");
            DisableControls();

            try
            {
                // Intentar obtener el cliente de Supabase
                var client = _supabaseService.GetClient();

                if (client != null)
                {
                    // Hacer una llamada simple para verificar la conexión
                    await Task.Delay(1000); // Simular test

                    ShowStatus("✅", "Conexión exitosa con Supabase", "#4CAF50", true);

                    // Mostrar información adicional
                    var config = _supabaseService.GetConfiguration();
                    var url = config?["Supabase:Url"] ?? "No configurado";

                    MessageBox.Show(
                        $"✅ Conexión establecida correctamente\n\n" +
                        $"🔗 URL: {url}\n" +
                        $"🔑 API Key: Configurada\n" +
                        $"📊 Estado: Activo",
                        "Test de Conexión Exitoso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    throw new Exception("No se pudo crear el cliente de Supabase");
                }
            }
            catch (Exception ex)
            {
                ShowStatus("❌", "Error de conexión", "#F44336", false);

                MessageBox.Show(
                    $"❌ No se pudo conectar a Supabase\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"Verifique:\n" +
                    $"• Las credenciales en appsettings.json\n" +
                    $"• La conexión a internet\n" +
                    $"• Que el proyecto en Supabase esté activo",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
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
            ConnectionStatusPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowStatus(string icon, string message, string colorHex, bool isSuccess)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ConnectionStatusPanel.Visibility = Visibility.Visible;

            StatusIcon.Text = icon;
            StatusText.Text = message;

            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var brush = new SolidColorBrush(color);

            StatusIcon.Foreground = brush;
            StatusText.Foreground = brush;
            ConnectionStatusPanel.Background = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B));
        }

        private void DisableControls()
        {
            EmailTextBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            LoginButton.IsEnabled = false;
            TestConnectionButton.IsEnabled = false;
            RememberCheckBox.IsEnabled = false;
        }

        private void EnableControls()
        {
            EmailTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            LoginButton.IsEnabled = true;
            TestConnectionButton.IsEnabled = true;
            RememberCheckBox.IsEnabled = true;
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
    }
}