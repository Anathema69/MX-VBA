using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewProductDialog : Window
    {
        private readonly ProductRowItem? _editProduct;

        public NewProductDialog(string categoryName, ProductRowItem? product = null)
        {
            InitializeComponent();
            _editProduct = product;

            DialogSubtitle.Text = $"Categoria: {categoryName}";

            if (product != null)
            {
                DialogTitle.Text = "Editar Producto";
                SaveButtonText.Text = "Actualizar Producto";
                PopulateFields(product);
            }
        }

        private void PopulateFields(ProductRowItem p)
        {
            CodeInput.Text = p.Code;
            NameInput.Text = p.Name;
            StockInput.Text = p.Stock.ToString();
            MinimumInput.Text = p.Minimum.ToString();
            PriceInput.Text = p.Price.ToString("F2");
            LocationInput.Text = p.Location;

            // Select matching unit
            foreach (ComboBoxItem item in UnitCombo.Items)
            {
                if (item.Content?.ToString() == p.Unit)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(CodeInput.Text) || string.IsNullOrWhiteSpace(NameInput.Text))
            {
                // Highlight empty required fields with red border
                if (string.IsNullOrWhiteSpace(CodeInput.Text))
                    CodeInput.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
                if (string.IsNullOrWhiteSpace(NameInput.Text))
                    NameInput.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
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
