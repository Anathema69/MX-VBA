using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Views;

namespace SistemaGestionProyectos2
{
    public partial class App : Application
    {
        private JsonLoggerService _logger;
        private SessionTimeoutService _timeoutService;
        private SessionTimeoutWarningWindow _warningWindow;
        private DateTime _lastActivityNotification = DateTime.MinValue;

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

            // Inicializar servicio de timeout
            _timeoutService = SessionTimeoutService.Instance;
            _timeoutService.OnWarning += TimeoutService_OnWarning;
            _timeoutService.OnTimeout += TimeoutService_OnTimeout;

            // Capturar eventos globales de actividad (mouse y teclado)
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnGlobalKeyDown));
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseMoveEvent, new MouseEventHandler(OnGlobalMouseMove));
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(OnGlobalMouseDown));

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
            System.Diagnostics.Debug.WriteLine($"Log guardado en: {_logger.GetCurrentSessionFolder()}");

            base.OnStartup(e);
        }

        // Evento de teclado global
        private void OnGlobalKeyDown(object sender, KeyEventArgs e)
        {
            NotifyUserActivity();
        }

        // Evento de movimiento de mouse global
        private void OnGlobalMouseMove(object sender, MouseEventArgs e)
        {
            NotifyUserActivity();
        }

        // Evento de clic de mouse global
        private void OnGlobalMouseDown(object sender, MouseButtonEventArgs e)
        {
            NotifyUserActivity();
        }

        // Notificar actividad al servicio de timeout
        private void NotifyUserActivity()
        {
            // Evitar notificar demasiado frecuentemente (throttle a cada 5 segundos)
            var now = DateTime.Now;
            if ((now - _lastActivityNotification).TotalSeconds < 5)
                return;

            _lastActivityNotification = now;
            _timeoutService.ResetTimer();

            // OCULTAR BANNER SI ESTÁ VISIBLE (al detectar actividad, el usuario está de vuelta)
            HideAllBanners();
        }

        // Método para ocultar todos los banners visibles en todas las ventanas
        private void HideAllBanners()
        {
            foreach (Window window in Windows)
            {
                if (window is not LoginWindow && window.Content is Grid mainGrid)
                {
                    foreach (var child in mainGrid.Children)
                    {
                        if (child is Controls.SessionTimeoutBanner banner)
                        {
                            banner.Hide();
                            System.Diagnostics.Debug.WriteLine($"📢 Banner ocultado en ventana {window.GetType().Name} por actividad del usuario");
                            break;
                        }
                    }
                }
            }
        }

        // Evento de advertencia de timeout
        private void TimeoutService_OnWarning(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("⚠️ Advertencia de timeout - Mostrando banner en ventana activa");

                // Buscar la ventana activa y mostrar el banner ahí
                var activeWindow = GetActiveApplicationWindow();
                if (activeWindow != null)
                {
                    ShowBannerInWindow(activeWindow);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se encontró ventana activa, usando ventana modal de respaldo");

                    // Fallback: usar ventana modal si no hay ventana activa
                    if (_warningWindow != null && _warningWindow.IsLoaded)
                        return;

                    _warningWindow = new SessionTimeoutWarningWindow();
                    _warningWindow.ShowDialog();

                    if (_warningWindow.ShouldLogout)
                    {
                        ForceLogout("Usuario eligió cerrar sesión desde advertencia", "Sesión cerrada exitosamente.");
                    }

                    _warningWindow = null;
                }
            });
        }

        private Window GetActiveApplicationWindow()
        {
            // Buscar ventana activa de la aplicación
            foreach (Window window in Windows)
            {
                if (window is not LoginWindow && window.IsActive)
                {
                    return window;
                }
            }

            // Si no hay ventana activa, retornar cualquier ventana visible
            foreach (Window window in Windows)
            {
                if (window is not LoginWindow && window.IsVisible)
                {
                    return window;
                }
            }

            return null;
        }

        private void ShowBannerInWindow(Window window)
        {
            // Buscar si la ventana ya tiene un Grid como contenido principal
            if (window.Content is Grid mainGrid)
            {
                // Buscar si ya existe un banner
                Controls.SessionTimeoutBanner existingBanner = null;
                foreach (var child in mainGrid.Children)
                {
                    if (child is Controls.SessionTimeoutBanner banner)
                    {
                        existingBanner = banner;
                        break;
                    }
                }

                if (existingBanner != null)
                {
                    // Si ya existe, solo mostrarlo
                    existingBanner.Show();
                }
                else
                {
                    // Crear y agregar nuevo banner
                    var banner = new Controls.SessionTimeoutBanner();
                    banner.OnExtendSession += (s, args) =>
                    {
                        System.Diagnostics.Debug.WriteLine("✅ Sesión extendida desde banner");
                    };
                    banner.OnDismiss += (s, args) =>
                    {
                        System.Diagnostics.Debug.WriteLine("❌ Banner cerrado por usuario");
                    };

                    // Agregar el banner al Grid principal
                    Grid.SetRow(banner, 0);
                    Grid.SetColumnSpan(banner, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
                    Panel.SetZIndex(banner, 9999); // Asegurar que esté en el frente

                    mainGrid.Children.Add(banner);
                    banner.Show();

                    System.Diagnostics.Debug.WriteLine($"✅ Banner agregado a ventana: {window.GetType().Name}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Ventana {window.GetType().Name} no tiene Grid como contenido, usando modal");

                // Fallback: usar ventana modal
                if (_warningWindow != null && _warningWindow.IsLoaded)
                    return;

                _warningWindow = new SessionTimeoutWarningWindow();
                _warningWindow.Owner = window;
                _warningWindow.ShowDialog();

                if (_warningWindow.ShouldLogout)
                {
                    ForceLogout("Usuario eligió cerrar sesión desde advertencia", "Sesión cerrada exitosamente.");
                }

                _warningWindow = null;
            }
        }

        // Evento de timeout (sesión cerrada por inactividad)
        private void TimeoutService_OnTimeout(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔥🔥🔥 ========================================");
            System.Diagnostics.Debug.WriteLine("🔥🔥🔥 EVENTO TIMEOUT DISPARADO");
            System.Diagnostics.Debug.WriteLine("🔥🔥🔥 ========================================");

            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("🔥 Dentro de Dispatcher.Invoke");

                // Cerrar ventana de advertencia si está abierta
                if (_warningWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine("🔥 Cerrando ventana de advertencia");
                    _warningWindow.Close();
                    _warningWindow = null;
                }

                System.Diagnostics.Debug.WriteLine("🔥 Llamando a ForceLogout...");
                ForceLogout("Sesión cerrada por inactividad", "Tu sesión ha sido cerrada por inactividad.\n\nPor favor, inicia sesión nuevamente.");
                System.Diagnostics.Debug.WriteLine("🔥 ForceLogout completado");
            });
        }

        // Forzar logout y volver a login
        public void ForceLogout(string reason, string userMessage = null)
        {
            System.Diagnostics.Debug.WriteLine("🚪🚪🚪 ========================================");
            System.Diagnostics.Debug.WriteLine($"🚪🚪🚪 FORCE LOGOUT INICIADO - Razón: {reason}");
            System.Diagnostics.Debug.WriteLine("🚪🚪🚪 ========================================");

            _logger.LogWarning("SESSION", "FORCED_LOGOUT", new
            {
                reason,
                timestamp = DateTime.Now
            });

            System.Diagnostics.Debug.WriteLine("🚪 Deteniendo timeout service...");
            _timeoutService.Stop();
            System.Diagnostics.Debug.WriteLine($"🚪 Timeout service detenido (IsRunning: {_timeoutService.IsRunning})");

            // Contar ventanas antes de cerrar
            int totalWindows = Windows.Count;
            System.Diagnostics.Debug.WriteLine($"🚪 Total de ventanas abiertas: {totalWindows}");

            // Listar todas las ventanas
            int windowIndex = 0;
            foreach (Window window in Windows)
            {
                System.Diagnostics.Debug.WriteLine($"   [{windowIndex}] {window.GetType().Name} - IsActive: {window.IsActive}, IsVisible: {window.IsVisible}");
                windowIndex++;
            }

            // CREAR Y MOSTRAR LOGINWINDOW PRIMERO (para evitar que la app se cierre al cerrar todas las ventanas)
            System.Diagnostics.Debug.WriteLine("🚪 Creando nueva ventana de Login...");
            var loginWindow = new LoginWindow();
            System.Diagnostics.Debug.WriteLine("🚪 Mostrando ventana de Login...");
            loginWindow.Show();

            // Forzar actualización de la UI
            System.Diagnostics.Debug.WriteLine("🚪 Activando ventana de Login...");
            loginWindow.Activate();
            loginWindow.Focus();

            System.Diagnostics.Debug.WriteLine($"🚪 LoginWindow mostrada - IsVisible: {loginWindow.IsVisible}, IsActive: {loginWindow.IsActive}");
            System.Diagnostics.Debug.WriteLine($"🚪 Total de ventanas ANTES de cerrar las demás: {Windows.Count}");

            // AHORA cerrar todas las ventanas excepto Login
            System.Diagnostics.Debug.WriteLine("🚪 Cerrando todas las ventanas excepto Login...");
            var windowsToClose = new System.Collections.Generic.List<Window>();
            foreach (Window window in Windows)
            {
                if (window is not LoginWindow)
                {
                    windowsToClose.Add(window);
                }
            }

            System.Diagnostics.Debug.WriteLine($"🚪 Se cerrarán {windowsToClose.Count} ventanas");
            foreach (var window in windowsToClose)
            {
                System.Diagnostics.Debug.WriteLine($"🚪   Cerrando: {window.GetType().Name}");
                window.Close();
            }

            System.Diagnostics.Debug.WriteLine($"🚪 Ventanas restantes después del cierre: {Windows.Count}");

            // Mostrar mensaje si se proporcionó (con delay para permitir que la ventana se renderice)
            if (!string.IsNullOrEmpty(userMessage))
            {
                System.Diagnostics.Debug.WriteLine($"🚪 Programando mensaje al usuario (con delay)...");

                // Usar Dispatcher para mostrar el MessageBox DESPUÉS de que la ventana se haya renderizado
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"🚪 Mostrando mensaje al usuario: {userMessage}");
                    MessageBox.Show(
                        loginWindow,
                        userMessage,
                        "Sesión Cerrada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    System.Diagnostics.Debug.WriteLine("🚪 Usuario cerró el MessageBox");
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }

            System.Diagnostics.Debug.WriteLine("🚪🚪🚪 FORCE LOGOUT COMPLETADO");
            System.Diagnostics.Debug.WriteLine("🚪🚪🚪 ========================================");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Detener servicio de timeout
            _timeoutService?.Stop();

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
