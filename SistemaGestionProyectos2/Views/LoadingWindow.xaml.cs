using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SistemaGestionProyectos2.Views
{
    public partial class LoadingWindow : Window
    {
        private DispatcherTimer _timer;
        private int _dotsCount = 0;

        public LoadingWindow()
        {
            InitializeComponent();
            StartDotsAnimation();
        }

        // Métodos para actualizar el estado de carga
        public void UpdateStatus(string title, string message, string details = null)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingTitle.Text = title;
                LoadingMessage.Text = message;

                if (!string.IsNullOrEmpty(details))
                {
                    LoadingDetails.Text = details;
                    LoadingDetails.Visibility = Visibility.Visible;
                }
                else
                {
                    LoadingDetails.Visibility = Visibility.Collapsed;
                }
            });
        }

        public void ShowProgress(bool show, double value = 0)
        {
            Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    LoadingProgress.Visibility = Visibility.Visible;
                    LoadingProgress.IsIndeterminate = value == 0;
                    if (value > 0)
                    {
                        LoadingProgress.Value = value;
                    }
                }
                else
                {
                    LoadingProgress.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void StartDotsAnimation()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += (s, e) =>
            {
                _dotsCount = (_dotsCount + 1) % 4;
                string dots = new string('.', _dotsCount);

                if (LoadingMessage.Text.Contains("..."))
                {
                    var baseText = LoadingMessage.Text.Replace("...", "").Replace("..", "").Replace(".", "");
                    LoadingMessage.Text = baseText + dots;
                }
            };
            _timer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }

        // Método helper para cerrar con fade out
        public async Task CloseWithFade()
        {
            var fadeOutDuration = TimeSpan.FromMilliseconds(300);
            var steps = 10;
            var stepDuration = fadeOutDuration.TotalMilliseconds / steps;

            for (int i = steps; i >= 0; i--)
            {
                this.Opacity = (double)i / steps;
                await Task.Delay((int)stepDuration);
            }

            this.Close();
        }
    }
}