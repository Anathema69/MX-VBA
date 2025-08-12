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
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;

            // Valores por defecto para pruebas
            UsernameTextBox.Text = "admin";
            PasswordBox.Password = "admin123";
            RoleComboBox.SelectedIndex = 0; // Admin por defecto
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

        // Botón de Login
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Validación
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowStatus("⚠️", "Por favor complete todos los campos", "#FFA726", false);
                return;
            }

            // Mostrar loading
            ShowLoading("Verificando credenciales...");
            DisableControls();

            try
            {
                // Simular delay de autenticación
                await Task.Delay(1500);

                // Obtener el rol seleccionado
                string selectedRole = GetSelectedRole();

                // Por ahora validación simple (después conectar con Supabase)
                bool loginSuccess = false;
                string userFullName = "";

                // Validación temporal según rol
                switch (selectedRole)
                {
                    case "admin":
                        if (UsernameTextBox.Text == "admin" && PasswordBox.Password == "admin123")
                        {
                            loginSuccess = true;
                            userFullName = "Administrador General";
                        }
                        break;
                    case "coordinator":
                        if (UsernameTextBox.Text == "coordinador" && PasswordBox.Password == "ima2025")
                        {
                            loginSuccess = true;
                            userFullName = "Coordinador General";
                        }
                        break;
                    case "salesperson":
                        // Cualquier vendedor de la lista
                        if (PasswordBox.Password == "ima2025")
                        {
                            loginSuccess = true;
                            userFullName = UsernameTextBox.Text.ToUpper();
                        }
                        break;
                }

                if (loginSuccess)
                {
                    ShowStatus("✅", "Acceso autorizado", "#4CAF50", true);
                    await Task.Delay(800);

                    // Crear objeto de usuario para pasar al menú principal
                    var currentUser = new UserSession
                    {
                        Username = UsernameTextBox.Text,
                        FullName = userFullName,
                        Role = selectedRole,
                        LoginTime = DateTime.Now
                    };

                    // Abrir menú principal
                    MainMenuWindow mainMenu = new MainMenuWindow(currentUser);
                    mainMenu.Show();
                    this.Close();
                }
                else
                {
                    ShowStatus("❌", "Credenciales incorrectas", "#F44336", false);
                }

                /* CÓDIGO PARA SUPABASE (cuando esté listo):
                var user = await _supabaseService.AuthenticateUser(
                    UsernameTextBox.Text, 
                    PasswordBox.Password
                );

                if (user != null && user.Role == selectedRole)
                {
                    // Login exitoso
                    var currentUser = new UserSession
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName,
                        Role = user.Role,
                        LoginTime = DateTime.Now
                    };

                    MainMenuWindow mainMenu = new MainMenuWindow(currentUser);
                    mainMenu.Show();
                    this.Close();
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

        private string GetSelectedRole()
        {
            switch (RoleComboBox.SelectedIndex)
            {
                case 0: return "admin";
                case 1: return "coordinator";
                case 2: return "salesperson";
                default: return "salesperson";
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
            RoleComboBox.IsEnabled = false;
            LoginButton.IsEnabled = false;
        }

        private void EnableControls()
        {
            UsernameTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            RoleComboBox.IsEnabled = true;
            LoginButton.IsEnabled = true;
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

// UserSession ya está definido en Models/UserSession.cs