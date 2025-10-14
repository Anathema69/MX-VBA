using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Controls
{
    public partial class SessionTimeoutBanner : UserControl
    {
        private readonly SessionTimeoutService _timeoutService;
        private Storyboard _slideDown;
        private Storyboard _slideUp;

        public event EventHandler OnExtendSession;
        public event EventHandler OnDismiss;

        public SessionTimeoutBanner()
        {
            InitializeComponent();
            _timeoutService = SessionTimeoutService.Instance;

            // Cargar animaciones
            _slideDown = (Storyboard)Resources["SlideDown"];
            _slideUp = (Storyboard)Resources["SlideUp"];

            // Suscribirse a actualizaciones del timer
            _timeoutService.OnTimerTick += UpdateCountdown;

            // Oculto por defecto
            Visibility = Visibility.Collapsed;
        }

        public void Show()
        {
            Visibility = Visibility.Visible;
            _slideDown.Begin(BannerBorder);

            System.Diagnostics.Debug.WriteLine("📢 Banner de timeout mostrado");
        }

        public void Hide()
        {
            _slideUp.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
            };
            _slideUp.Begin(BannerBorder);

            System.Diagnostics.Debug.WriteLine("📢 Banner de timeout ocultado");
        }

        private void UpdateCountdown(object sender, int remainingSeconds)
        {
            Dispatcher.Invoke(() =>
            {
                var minutes = remainingSeconds / 60;
                var seconds = remainingSeconds % 60;

                CountdownText.Text = $"{minutes}:{seconds:D2}";

                // Cambiar colores según urgencia
                if (remainingSeconds <= 10)
                {
                    // Rojo crítico en últimos 10 segundos
                    BannerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                    BannerBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    CountdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                    ExtendButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

                    // Parpadeo
                    if (remainingSeconds % 2 == 0)
                    {
                        CountdownText.FontWeight = FontWeights.ExtraBold;
                    }
                    else
                    {
                        CountdownText.FontWeight = FontWeights.Bold;
                    }
                }
                else if (remainingSeconds <= 20)
                {
                    // Naranja en últimos 20 segundos
                    BannerBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FED7AA"));
                    BannerBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316"));
                    CountdownText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA580C"));
                    ExtendButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F97316"));
                }
            });
        }

        private void ExtendButton_Click(object sender, RoutedEventArgs e)
        {
            // Resetear timer
            _timeoutService.ResetTimer();

            // Log
            var logger = JsonLoggerService.Instance;
            logger.LogInfo("SESSION", "USER_EXTENDED_SESSION_FROM_BANNER", new
            {
                action = "User clicked extend from banner",
                timestamp = DateTime.Now
            });

            System.Diagnostics.Debug.WriteLine("✅ Usuario extendió sesión desde banner");

            // Notificar y ocultar
            OnExtendSession?.Invoke(this, EventArgs.Empty);
            Hide();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("❌ Usuario cerró banner (sesión se cerrará automáticamente)");

            // Notificar y ocultar
            OnDismiss?.Invoke(this, EventArgs.Empty);
            Hide();
        }

        public void Cleanup()
        {
            _timeoutService.OnTimerTick -= UpdateCountdown;
        }
    }
}
