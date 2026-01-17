using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SistemaGestionProyectos2.Controls
{
    public partial class ToastNotification : UserControl
    {
        private Storyboard _showAnimation;
        private Storyboard _hideAnimation;

        public enum ToastType
        {
            Success,
            Error,
            Warning,
            Info
        }

        public ToastNotification()
        {
            InitializeComponent();
            _showAnimation = (Storyboard)Resources["ShowAnimation"];
            _hideAnimation = (Storyboard)Resources["HideAnimation"];
        }

        public void Show(string title, string message = null, ToastType type = ToastType.Success, int durationMs = 3000)
        {
            TitleText.Text = title;

            if (!string.IsNullOrEmpty(message))
            {
                MessageText.Text = message;
                MessageText.Visibility = Visibility.Visible;
            }
            else
            {
                MessageText.Visibility = Visibility.Collapsed;
            }

            ApplyStyle(type);

            this.Visibility = Visibility.Visible;
            _showAnimation.Begin(this);

            if (durationMs > 0)
            {
                _ = AutoHideAsync(durationMs);
            }
        }

        private void ApplyStyle(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10B981
                    IconBorder.Background = new SolidColorBrush(Colors.White);
                    IconText.Text = "✓";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    TitleText.Foreground = Brushes.White;
                    MessageText.Foreground = Brushes.White;
                    CloseButton.Foreground = Brushes.White;
                    break;

                case ToastType.Error:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #EF4444
                    IconBorder.Background = new SolidColorBrush(Colors.White);
                    IconText.Text = "✕";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    TitleText.Foreground = Brushes.White;
                    MessageText.Foreground = Brushes.White;
                    CloseButton.Foreground = Brushes.White;
                    break;

                case ToastType.Warning:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // #F59E0B
                    IconBorder.Background = new SolidColorBrush(Colors.White);
                    IconText.Text = "!";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    TitleText.Foreground = Brushes.White;
                    MessageText.Foreground = Brushes.White;
                    CloseButton.Foreground = Brushes.White;
                    break;

                case ToastType.Info:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // #3B82F6
                    IconBorder.Background = new SolidColorBrush(Colors.White);
                    IconText.Text = "i";
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    TitleText.Foreground = Brushes.White;
                    MessageText.Foreground = Brushes.White;
                    CloseButton.Foreground = Brushes.White;
                    break;
            }
        }

        private async Task AutoHideAsync(int durationMs)
        {
            await Task.Delay(durationMs);
            Hide();
        }

        public void Hide()
        {
            _hideAnimation.Completed += (s, e) =>
            {
                this.Visibility = Visibility.Collapsed;
            };
            _hideAnimation.Begin(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
