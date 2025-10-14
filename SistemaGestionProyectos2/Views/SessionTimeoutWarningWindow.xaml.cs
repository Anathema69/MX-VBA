using System;
using System.Windows;
using System.Windows.Media;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    public partial class SessionTimeoutWarningWindow : Window
    {
        private readonly SessionTimeoutService _timeoutService;
        private bool _userResponded = false;

        public bool ShouldLogout { get; private set; } = false;

        public SessionTimeoutWarningWindow()
        {
            InitializeComponent();
            _timeoutService = SessionTimeoutService.Instance;

            // Suscribirse a actualizaciones del timer
            _timeoutService.OnTimerTick += UpdateCountdown;

            // Actualizar inmediatamente
            var config = _timeoutService.GetConfig();
            var remainingSeconds = (int)(config.WarningBeforeMinutes * 60);
            UpdateCountdownDisplay(remainingSeconds);

            Loaded += Window_Loaded;
            Closed += Window_Closed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Animación de entrada (opcional)
            this.Opacity = 0;
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            this.BeginAnimation(OpacityProperty, animation);
        }

        private void UpdateCountdown(object sender, int remainingSeconds)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateCountdownDisplay(remainingSeconds);
            });
        }

        private void UpdateCountdownDisplay(int remainingSeconds)
        {
            var minutes = remainingSeconds / 60;
            var seconds = remainingSeconds % 60;

            CountdownText.Text = $"{minutes}:{seconds:D2}";

            // Cambiar color según urgencia
            if (remainingSeconds <= 30)
            {
                // Rojo crítico en últimos 30 segundos
                CountdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

                // Parpadeo visual
                if (remainingSeconds % 2 == 0)
                {
                    CountdownText.FontWeight = FontWeights.ExtraBold;
                }
                else
                {
                    CountdownText.FontWeight = FontWeights.Bold;
                }
            }
            else if (remainingSeconds <= 60)
            {
                // Naranja en último minuto
                CountdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                CountdownText.FontWeight = FontWeights.Bold;
            }
            else
            {
                // Amarillo normal
                CountdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                CountdownText.FontWeight = FontWeights.Bold;
            }

            // Cerrar automáticamente si el tiempo se acaba
            if (remainingSeconds <= 0 && !_userResponded)
            {
                _userResponded = true; // Marcar como respondido para evitar loop
                ShouldLogout = true;

                System.Diagnostics.Debug.WriteLine("⏰ Tiempo agotado - Cerrando ventana automáticamente");

                // Desuscribirse antes de cerrar
                _timeoutService.OnTimerTick -= UpdateCountdown;

                // Cerrar directamente
                DialogResult = false;
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            _userResponded = true;
            ShouldLogout = false;

            // Resetear timer de inactividad
            _timeoutService.ResetTimer();

            // Log
            var logger = JsonLoggerService.Instance;
            logger.LogInfo("SESSION", "USER_CONTINUED_SESSION", new
            {
                action = "User clicked continue",
                timestamp = DateTime.Now
            });

            System.Diagnostics.Debug.WriteLine("✅ Usuario continuó trabajando - Timer reseteado");

            // Cerrar sin animación para evitar loop
            _timeoutService.OnTimerTick -= UpdateCountdown;
            DialogResult = true;
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _userResponded = true;
            ShouldLogout = true;

            // Log
            var logger = JsonLoggerService.Instance;
            logger.LogInfo("SESSION", "USER_LOGOUT_FROM_WARNING", new
            {
                action = "User clicked logout from warning",
                timestamp = DateTime.Now
            });

            System.Diagnostics.Debug.WriteLine("🚪 Usuario cerró sesión manualmente desde advertencia");

            // Cerrar sin animación para evitar loop
            _timeoutService.OnTimerTick -= UpdateCountdown;
            DialogResult = false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Desuscribirse del evento
            _timeoutService.OnTimerTick -= UpdateCountdown;

            // Si el usuario no respondió y la ventana se cerró, es por timeout
            if (!_userResponded)
            {
                ShouldLogout = true;
            }
        }
    }
}
