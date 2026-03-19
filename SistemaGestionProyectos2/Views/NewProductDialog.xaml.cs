using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaGestionProyectos2.Views
{
    public partial class NewProductDialog : Window
    {
        private readonly ProductRowItem? _editProduct;

        // Propiedades publicas para leer los valores del formulario
        public string ProductCode => CodeInput.Text.Trim();
        public string ProductName => NameInput.Text.Trim();
        public string ProductDescription => DescriptionInput.Text.Trim();
        public decimal ProductStock => decimal.TryParse(StockInput.Text, out var s) ? s : 0;
        public decimal ProductMinimum => decimal.TryParse(MinimumInput.Text, out var m) ? m : 0;
        public string ProductUnit => (UnitCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "pza";
        public decimal ProductPrice => decimal.TryParse(PriceInput.Text, out var p) ? p : 0;
        public string ProductLocation => LocationInput.Text.Trim();
        public string ProductNotes => NotesInput.Text.Trim();

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
            StockInput.Text = p.StockCurrent.ToString("F0");
            MinimumInput.Text = p.StockMinimum.ToString("F0");
            PriceInput.Text = p.UnitPrice.ToString("F2");
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
