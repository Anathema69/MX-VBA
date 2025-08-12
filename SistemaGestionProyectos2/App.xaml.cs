using System;
using System.Windows;
using System.Windows.Threading;

namespace SistemaGestionProyectos2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Capturar TODOS los errores
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                MessageBox.Show(
                    $"Error crítico en la aplicación:\n\n{ex.Message}\n\nDetalles:\n{ex.StackTrace}",
                    "Error Fatal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Log to debug
                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: {ex}");
            };

            this.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Error en la interfaz:\n\n{args.Exception.Message}",
                    "Error UI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"UI ERROR: {args.Exception}");
                args.Handled = true;
            };

            // Log de inicio
            System.Diagnostics.Debug.WriteLine("=== APLICACIÓN INICIANDO ===");

            base.OnStartup(e);
        }
    }
}