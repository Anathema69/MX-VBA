using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Models;

namespace SistemaGestionProyectos2.Views
{
    public partial class LoginWindow : Window
    {
        private readonly SupabaseService _supabaseService;

        // Diccionario temporal de usuarios con sus roles (después se obtendrá de la BD)
        private readonly Dictionary<string, (string password, string fullName, string role)> _users = new()
        {
            // Administradores
            { "admin", ("admin123", "Administrador General", "admin") },
            
            // Coordinadores
            { "coordinador", ("ima2025", "Coordinador General", "coordinator") },
            { "coord1", ("ima2025", "Coordinador de Producción", "coordinator") },
            
            // Vendedores
            { "mgarza", ("ima2025", "MARIO GARZA", "salesperson") }
        };

        public LoginWindow()
        {
            try
            {
                InitializeComponent();


                // _supabaseService = SupabaseService.Instance;
                // Valores por defecto para pruebas
                

                // Enfocar el campo de usuario
                UsernameTextBox.Focus();
                UsernameTextBox.SelectAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en LoginWindow:\n{ex.Message}", "Error");
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

            ShowLoading("Verificando credenciales...");
            DisableControls();

            try
            {
                await Task.Delay(1500); // Simular carga

                // USAR SOLO MODO OFFLINE POR AHORA
                string username = UsernameTextBox.Text.ToLower().Trim();
                string password = PasswordBox.Password;

                if (_users.ContainsKey(username) && _users[username].password == password)
                {
                    var userData = _users[username];
                    ShowStatus("✅", "Acceso autorizado (Modo Offline)", "#4CAF50", true);
                    await Task.Delay(500);

                    var currentUser = new UserSession
                    {
                        Id = 1,
                        Username = username,
                        FullName = userData.fullName,
                        Role = userData.role,
                        LoginTime = DateTime.Now
                    };

                    // Crear ventana de carga
                    var loadingWindow = new LoadingWindow();
                    loadingWindow.Show();

                    this.Hide();

                    loadingWindow.UpdateStatus("Preparando Sistema", "Modo Offline...");
                    await Task.Delay(800);

                    MainMenuWindow mainMenu = new MainMenuWindow(currentUser);
                    mainMenu.Show();

                    await loadingWindow.CloseWithFade();
                    this.Close();
                }
                else
                {
                    ShowStatus("❌", "Credenciales incorrectas", "#F44336", false);
                }
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

        // Modo offline como fallback
        private async Task LoginOfflineMode()
        {
            string username = UsernameTextBox.Text.ToLower().Trim();
            string password = PasswordBox.Password;

            if (_users.ContainsKey(username) && _users[username].password == password)
            {
                var userData = _users[username];
                ShowStatus("🔌", "Modo Offline", "#FF9800", true);
                await Task.Delay(500);

                var currentUser = new UserSession
                {
                    Username = username,
                    FullName = userData.fullName + " (Offline)",
                    Role = userData.role,
                    LoginTime = DateTime.Now
                };

                MainMenuWindow mainMenu = new MainMenuWindow(currentUser);
                mainMenu.Show();
                this.Close();
            }
            else
            {
                ShowStatus("❌", "Credenciales incorrectas", "#F44336", false);
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
        }

        private void EnableControls()
        {
            UsernameTextBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
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