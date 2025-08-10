using SistemaGestionProyectos2.Services; // Ajusta el namespace si es necesario
using System.Windows;
using SistemaGestionProyectos2.Views;

namespace SistemaGestionProyectos2
{
    public partial class MainWindow : Window
    {
        private readonly SupabaseService _supabaseService;

        public MainWindow()
        {
            InitializeComponent();
            _supabaseService = SupabaseService.Instance;

            // Mostrar el usuario actual en la barra de estado
            if (_supabaseService.IsAuthenticated())
            {
                var user = _supabaseService.GetCurrentUser();
                UserText.Text = $"Usuario: {user?.Email ?? "Desconocido"}";
            }
        }

        private void NuevoProyecto_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funcionalidad de Nuevo Proyecto en desarrollo", "En construcción", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void CerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Está seguro que desea cerrar sesión?",
                                        "Confirmar",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _supabaseService.SignOut();

                // Volver al login
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }

        private void Salir_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("¿Está seguro que desea salir de la aplicación?",
                                        "Confirmar salida",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void VerProyectos_Click(object sender, RoutedEventArgs e)
        {
            // Cambiar a la pestaña de proyectos
            MainTabControl.SelectedItem = ProyectosTab;
        }

        private void VerGantt_Click(object sender, RoutedEventArgs e)
        {
            // Cambiar a la pestaña de Gantt
            MainTabControl.SelectedItem = GanttTab;
        }
    }
}