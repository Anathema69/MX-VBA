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
                // Buscar la ventana activa y mostrar el banner ahí
                var activeWindow = GetActiveApplicationWindow();
                if (activeWindow != null)
                {
                    ShowBannerInWindow(activeWindow);
                }
                else
                {
                    // Fallback: usar ventana modal si no hay ventana activa
                    if (_warningWindow != null && _warningWindow.IsLoaded)
                        return;

                    _warningWindow = new SessionTimeoutWarningWindow();
                    _warningWindow.ShowDialog();

                    if (_warningWindow.ShouldLogout)
                    {
                        ForceLogout("Usuario eligió cerrar sesión desde advertencia");
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

                    // Agregar el banner al Grid principal
                    Grid.SetRow(banner, 0);
                    Grid.SetColumnSpan(banner, mainGrid.ColumnDefinitions.Count > 0 ? mainGrid.ColumnDefinitions.Count : 1);
                    Panel.SetZIndex(banner, 9999); // Asegurar que esté en el frente

                    mainGrid.Children.Add(banner);
                    banner.Show();
                }
            }
            else
            {
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
            Dispatcher.Invoke(() =>
            {
                // Cerrar ventana de advertencia si está abierta
                _warningWindow?.Close();
                _warningWindow = null;

                ForceLogout("Sesión cerrada por inactividad", "Tu sesión ha sido cerrada por inactividad.\n\nPor favor, inicia sesión nuevamente.");
            });
        }

        // Forzar logout y volver a login
        public void ForceLogout(string reason, string userMessage = null)
        {
            _logger.LogWarning("SESSION", "FORCED_LOGOUT", new
            {
                reason,
                timestamp = DateTime.Now
            });

            _timeoutService.Stop();

            // CREAR Y MOSTRAR LOGINWINDOW PRIMERO (para evitar que la app se cierre al cerrar todas las ventanas)
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            loginWindow.Activate();
            loginWindow.Focus();

            // AHORA cerrar todas las ventanas excepto Login
            var windowsToClose = new System.Collections.Generic.List<Window>();
            foreach (Window window in Windows)
            {
                if (window is not LoginWindow)
                {
                    windowsToClose.Add(window);
                }
            }

            foreach (var window in windowsToClose)
            {
                window.Close();
            }

            // Mostrar mensaje si se proporcionó (con delay para permitir que la ventana se renderice)
            if (!string.IsNullOrEmpty(userMessage))
            {
                // Usar Dispatcher para mostrar el MessageBox DESPUÉS de que la ventana se haya renderizado
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(
                        loginWindow,
                        userMessage,
                        "Sesión Cerrada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        /// <summary>
        /// Verifica si hay actualizaciones disponibles para la aplicación
        /// </summary>
        public async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                // Obtener versión actual desde AssemblyInfo
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var currentVersion = $"{version.Major}.{version.Minor}.{version.Build}";

                _logger.LogInfo("UPDATE", "CHECK_INIT", new
                {
                    currentVersion
                });

                // Obtener cliente de Supabase desde SupabaseService
                var supabaseClient = SistemaGestionProyectos2.Services.SupabaseService.Instance.GetClient();
                if (supabaseClient == null)
                {
                    _logger.LogWarning("UPDATE", "NO_SUPABASE_CLIENT", new
                    {
                        message = "Cliente de Supabase no disponible"
                    });
                    return;
                }

                // Crear servicio de actualización
                var updateService = new SistemaGestionProyectos2.Services.Updates.UpdateService(supabaseClient, currentVersion);

                // Verificar actualizaciones
                var (available, newVersion, message) = await updateService.CheckForUpdate();

                if (available && newVersion != null)
                {
                    _logger.LogInfo("UPDATE", "SHOWING_UPDATE_WINDOW", new
                    {
                        newVersion = newVersion.Version,
                        mandatory = newVersion.IsMandatory
                    });

                    // Mostrar ventana de actualización en el thread de UI
                    Dispatcher.Invoke(() =>
                    {
                        var updateWindow = new UpdateAvailableWindow(updateService, newVersion);
                        updateWindow.ShowDialog();

                        if (updateWindow.UpdatePostponed)
                        {
                            _logger.LogInfo("UPDATE", "USER_POSTPONED", new
                            {
                                version = newVersion.Version
                            });
                        }
                    });
                }
                else
                {
                    _logger.LogInfo("UPDATE", "NO_UPDATE_NEEDED", new
                    {
                        currentVersion,
                        message
                    });
                }
            }
            catch (Exception ex)
            {
                // No es crítico si falla la verificación de actualizaciones
                _logger.LogError("UPDATE", "CHECK_FAILED", new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });

                System.Diagnostics.Debug.WriteLine($"Error verificando actualizaciones: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Detener servicio de timeout
                _timeoutService?.Stop();

                // Cerrar sesión de log de forma sincrónica sin bloquear
                _logger.LogInfo("SYSTEM", "APPLICATION_EXIT", new
                {
                    exitCode = e.ApplicationExitCode
                });

                // Intentar cerrar sesión con timeout de 2 segundos máximo
                var closeTask = _logger.CloseSessionAsync();
                if (!closeTask.Wait(TimeSpan.FromSeconds(2)))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Timeout cerrando logger - forzando cierre");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en OnExit: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}
