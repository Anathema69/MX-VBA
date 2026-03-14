using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SistemaGestionProyectos2.Views
{
    public partial class CategoryDetailWindow : Window
    {
        private readonly CategoryCardItem _category;
        private List<ProductRowItem> _allProducts = new();
        private ProductRowItem? _pendingDeleteProduct;
        private bool _isInitialized;

        private static readonly string LogPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "inventory_debug.log");

        public CategoryDetailWindow(CategoryCardItem category)
        {
            _category = category;
            InitializeComponent();
            this.SourceInitialized += (s, e) => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            SetupHeader();
            LoadMockProducts();
            _isInitialized = true;
        }

        private static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                File.AppendAllText(LogPath, line + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine($"[Inventory] {message}");
            }
            catch { /* logging should never crash the app */ }
        }

        private void SetupHeader()
        {
            Log($"SetupHeader: category={_category.Name}, color={_category.Color}, products={_category.ProductsCount}");
            CategoryNameText.Text = _category.Name;
            CategoryCountText.Text = $"{_category.ProductsCount} productos";
            CategoryColorBar.Background = _category.ColorBrush;
        }

        private void LoadMockProducts()
        {
            Log("LoadMockProducts: creating 8 mock items");
            _allProducts = new List<ProductRowItem>
            {
                new("TOR-001", "Tornillo M3x10",    150,  50, "pza", 0.50m, "A-1"),
                new("TOR-002", "Tornillo M4x15",     20,  30, "pza", 0.75m, "A-1"),
                new("TOR-003", "Tornillo M5x20",    200, 100, "pza", 1.00m, "A-2"),
                new("TOR-004", "Tuerca M3",           45,  50, "pza", 0.30m, "A-1"),
                new("TOR-005", "Arandela M3",        500, 100, "pza", 0.10m, "A-3"),
                new("TOR-006", "Tornillo Allen M6",   80,  20, "pza", 1.50m, "A-2"),
                new("TOR-007", "Perno M8x30",         10,  25, "pza", 2.00m, "B-1"),
                new("TOR-008", "Rondana plana M4",   300,  50, "pza", 0.15m, "A-1"),
            };

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            try
            {
                // Guard: controls may not exist yet during InitializeComponent
                if (ProductsGrid == null)
                {
                    Log("RefreshGrid: SKIP - ProductsGrid is null (too early)");
                    return;
                }

                Log($"RefreshGrid: start, products={_allProducts.Count}");

                var items = _allProducts.AsEnumerable();

                // Text filter
                var searchText = ProductSearchBox?.Text;
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    Log($"RefreshGrid: text filter='{searchText}'");
                    items = items.Where(p =>
                        p.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                }

                // Low stock filter
                if (LowStockToggle?.IsChecked == true)
                {
                    Log("RefreshGrid: low stock filter ON");
                    items = items.Where(p => p.IsLowStock);
                }

                // Location filter
                if (LocationFilter?.SelectedIndex > 0)
                {
                    var loc = (LocationFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (!string.IsNullOrEmpty(loc))
                    {
                        Log($"RefreshGrid: location filter='{loc}'");
                        items = items.Where(p => p.Location == loc);
                    }
                }

                var list = items.ToList();
                Log($"RefreshGrid: filtered count={list.Count}, setting ItemsSource...");

                ProductsGrid.ItemsSource = null; // clear first to avoid stale bindings
                ProductsGrid.ItemsSource = list;

                Log("RefreshGrid: ItemsSource set OK");

                int lowCount = list.Count(p => p.IsLowStock);
                decimal totalValue = list.Sum(p => p.Stock * p.Price);

                if (LowStockCountText != null)
                    LowStockCountText.Text = $"{lowCount} productos con stock bajo";
                if (TotalValueText != null)
                    TotalValueText.Text = $"Valor total de inventario: ${totalValue:N2}";
                if (ShowingCountText != null)
                    ShowingCountText.Text = $"Mostrando {list.Count} de {_allProducts.Count}";
                if (CategoryCountText != null)
                    CategoryCountText.Text = $"{_allProducts.Count} productos";

                Log("RefreshGrid: complete OK");
            }
            catch (Exception ex)
            {
                Log($"RefreshGrid: ERROR - {ex.GetType().Name}: {ex.Message}");
                Log($"RefreshGrid: StackTrace:\n{ex.StackTrace}");
                if (ex.InnerException != null)
                    Log($"RefreshGrid: Inner - {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                ShowToast($"Error: {ex.Message}", ToastType.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => Close();

        private void NewProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new NewProductDialog(_category.Name);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    ShowToast("Producto creado exitosamente", ToastType.Success);
                    RefreshGrid();
                }
            }
            catch (Exception ex) { Log($"NewProduct_Click ERROR: {ex}"); }
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is ProductRowItem product)
                {
                    var dialog = new NewProductDialog(_category.Name, product);
                    dialog.Owner = this;
                    if (dialog.ShowDialog() == true)
                    {
                        ShowToast($"Producto \"{product.Name}\" actualizado", ToastType.Success);
                        RefreshGrid();
                    }
                }
            }
            catch (Exception ex) { Log($"EditProduct_Click ERROR: {ex}"); }
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is ProductRowItem product)
                {
                    _pendingDeleteProduct = product;
                    DeleteConfirmText.Text = $"\"{product.Name}\" ({product.Code}) sera eliminado permanentemente.";
                    DeleteOverlay.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex) { Log($"DeleteProduct_Click ERROR: {ex}"); }
        }

        private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingDeleteProduct != null)
            {
                var name = _pendingDeleteProduct.Name;
                _allProducts.Remove(_pendingDeleteProduct);
                _pendingDeleteProduct = null;
                DeleteOverlay.Visibility = Visibility.Collapsed;
                RefreshGrid();
                ShowToast($"Producto \"{name}\" eliminado", ToastType.Warning);
            }
        }

        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            _pendingDeleteProduct = null;
            DeleteOverlay.Visibility = Visibility.Collapsed;
        }

        private void CancelDelete_Backdrop(object sender, MouseButtonEventArgs e)
        {
            _pendingDeleteProduct = null;
            DeleteOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitialized) RefreshGrid();
        }

        private void LowStockToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) RefreshGrid();
        }

        private void LocationFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized) RefreshGrid();
        }

        #region Toast Notifications

        private void ShowToast(string message, ToastType type)
        {
            try
            {
                if (ToastContainer == null) return;

                string bgColor, fgColor, icon;
                switch (type)
                {
                    case ToastType.Success:
                        bgColor = "#F0FFF4"; fgColor = "#22543D"; icon = "\uE73E"; break;
                    case ToastType.Warning:
                        bgColor = "#FFFBEB"; fgColor = "#92400E"; icon = "\uE7BA"; break;
                    case ToastType.Error:
                        bgColor = "#FEF2F2"; fgColor = "#991B1B"; icon = "\uEA39"; break;
                    default:
                        bgColor = "#EFF6FF"; fgColor = "#1E3A8A"; icon = "\uE946"; break;
                }

                var toast = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 8),
                    MinWidth = 280,
                    IsHitTestVisible = true,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#1E293B"),
                        BlurRadius = 16, ShadowDepth = 4, Opacity = 0.12, Direction = 270
                    },
                    Opacity = 0,
                    RenderTransform = new TranslateTransform(40, 0)
                };

                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(new TextBlock
                {
                    Text = icon,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgColor)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = message,
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgColor)),
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center
                });

                toast.Child = panel;
                ToastContainer.Children.Add(toast);

                // Animate in
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var slideIn = new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(300))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

                toast.BeginAnimation(OpacityProperty, fadeIn);
                ((TranslateTransform)toast.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);

                // Auto-dismiss after 3s
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                    fadeOut.Completed += (_, _) => ToastContainer.Children.Remove(toast);
                    toast.BeginAnimation(OpacityProperty, fadeOut);
                };
                timer.Start();
            }
            catch (Exception ex) { Log($"ShowToast ERROR: {ex.Message}"); }
        }

        #endregion
    }

    // ViewModel for product table rows
    public class ProductRowItem
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int Stock { get; set; }
        public int Minimum { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public string Location { get; set; }

        public bool IsLowStock => Stock < Minimum;
        public bool IsHighStock => Stock > Minimum * 2;

        public string PriceFormatted => $"${Price:N2}";

        // Cache brushes to avoid creating new instances on every binding evaluation
        private static readonly Brush LowStockBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        private static readonly Brush HighStockBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78"));
        private static readonly Brush NormalStockBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));

        static ProductRowItem()
        {
            LowStockBrush.Freeze();
            HighStockBrush.Freeze();
            NormalStockBrush.Freeze();
        }

        public Brush StockColor => IsLowStock ? LowStockBrush : IsHighStock ? HighStockBrush : NormalStockBrush;
        public FontWeight StockWeight => IsLowStock ? FontWeights.Bold : FontWeights.Normal;

        public ProductRowItem(string code, string name, int stock, int min, string unit, decimal price, string location)
        {
            Code = code;
            Name = name;
            Stock = stock;
            Minimum = min;
            Unit = unit;
            Price = price;
            Location = location;
        }
    }
}
