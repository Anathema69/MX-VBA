using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewCategoryDialog : Window
    {
        private string _selectedColor = "#3B82F6";

        public string CategoryName => NameInput.Text.Trim();
        public string CategoryDescription => DescriptionInput.Text.Trim();
        public string CategoryColor => _selectedColor;

        public NewCategoryDialog()
        {
            InitializeComponent();
            NameInput.TextChanged += (_, _) => UpdatePreview();
            DescriptionInput.TextChanged += (_, _) => UpdatePreview();
        }

        private void UpdatePreview()
        {
            PreviewName.Text = string.IsNullOrWhiteSpace(NameInput.Text)
                ? "NUEVA CATEGORIA"
                : NameInput.Text.ToUpperInvariant();
            PreviewDesc.Text = string.IsNullOrWhiteSpace(DescriptionInput.Text)
                ? "Sin descripcion"
                : DescriptionInput.Text;
            PreviewColorBar.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_selectedColor));
        }

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string color)
            {
                _selectedColor = color;
                UpdatePreview();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameInput.Text))
            {
                NameInput.BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#EF4444"));
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Backdrop_Click(object sender, MouseButtonEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
