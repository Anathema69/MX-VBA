using System;
using System.Windows;
using System.Windows.Threading;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2
{
    public partial class App : Application
    {
        private JsonLoggerService _logger;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Inicializar el logger
            _logger = JsonLoggerService.Instance;
            _logger.LogInfo("SYSTEM", "APPLICATION_START", new
            {
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                environment = Environment.OSVersion.ToString(),
                dotnetVersion = Environment.Version.ToString()
            });

            // Capturar TODOS los errores
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;

                _logger.LogError("SYSTEM", "UNHANDLED_EXCEPTION", new
                {
                    message = ex.Message,
                    stackTrace = ex.StackTrace,
                    source = ex.Source,
                    isTerminating = args.IsTerminating
                });

                MessageBox.Show(
                    $"Error crítico en la aplicación:\n\n{ex.Message}\n\nDetalles guardados en el log.",
                    "Error Fatal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: {ex}");
            };

            this.DispatcherUnhandledException += (s, args) =>
            {
                _logger.LogError("UI", "DISPATCHER_EXCEPTION", new
                {
                    message = args.Exception.Message,
                    stackTrace = args.Exception.StackTrace,
                    source = args.Exception.Source
                });

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
            System.Diagnostics.Debug.WriteLine($"Log guardado en: {_logger.GetCurrentLogPath()}");

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cerrar sesión de log
            _logger.LogInfo("SYSTEM", "APPLICATION_EXIT", new
            {
                exitCode = e.ApplicationExitCode
            });

            _logger.CloseSessionAsync().Wait();

            base.OnExit(e);
        }
    }
}