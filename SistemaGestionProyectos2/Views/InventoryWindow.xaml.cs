using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SistemaGestionProyectos2.Views
{
    public partial class InventoryWindow : Window
    {
        private List<CategoryCardItem> _allCategories = new();

        public InventoryWindow()
        {
            InitializeComponent();
            this.SourceInitialized += (s, e) => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            LoadMockData();
        }

        private void LoadMockData()
        {
            _allCategories = new List<CategoryCardItem>
            {
                new(1, "TORNILLERIA", "Tornillos, tuercas y arandelas", "#3B82F6", 42, 915, 3),
                new(2, "CABLEADO", "Cables electricos y de datos", "#10B981", 18, 340, 0),
                new(3, "CONECTORES", "Conectores industriales y terminales", "#8B5CF6", 25, 580, 7),
                new(4, "HERRAMIENTAS", "Herramientas manuales y electricas", "#F59E0B", 15, 127, 1),
                new(5, "SENSORES", "Sensores de proximidad, temperatura", "#EC4899", 8, 64, 2),
                new(6, "MOTORES", "Motores AC, DC y paso a paso", "#EF4444", 12, 48, 0),
            };

            RefreshView();
        }

        private void RefreshView(string? filter = null)
        {
            var items = _allCategories.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
                items = items.Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

            var list = items.ToList();
            CategoriesPanel.ItemsSource = list;

            int totalProducts = list.Sum(c => c.ProductsCount);
            int totalLow = list.Sum(c => c.LowStockCount);
            TotalProductsText.Text = totalProducts.ToString();
            LowStockTotalText.Text = totalLow.ToString();
            TotalCategoriesText.Text = list.Count.ToString();
            FooterCategoryCount.Text = $"{list.Count} categorias activas";
            FooterTimestamp.Text = $"Ultima actualizacion: {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        private void CategoryCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is CategoryCardItem cat)
            {
                var detail = new CategoryDetailWindow(cat);
                detail.Show();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => Close();

        private void NewCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NewCategoryDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var newCat = new CategoryCardItem(
                    _allCategories.Count + 1,
                    dialog.CategoryName.ToUpperInvariant(),
                    dialog.CategoryDescription,
                    dialog.CategoryColor,
                    0, 0, 0);

                _allCategories.Add(newCat);
                RefreshView(SearchBox.Text);
                ShowToast($"Categoria \"{newCat.Name}\" creada", ToastType.Success);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshView(SearchBox.Text);
        }

        #region Toast Notifications

        private void ShowToast(string message, ToastType type)
        {
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

        #endregion
    }

    public enum ToastType { Info, Success, Warning, Error }

    // ViewModel for category cards
    public class CategoryCardItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public int ProductsCount { get; set; }
        public int StockTotal { get; set; }
        public int LowStockCount { get; set; }

        public Brush ColorBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Color));

        // Badge
        public string BadgeText => LowStockCount > 0 ? $"{LowStockCount} por pedir" : "Stock OK";
        public string BadgeIcon => LowStockCount > 0 ? "\uE7BA" : "\uE73E";
        public Brush BadgeBg => LowStockCount > 0
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEB"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FFF4"));
        public Brush BadgeFg => LowStockCount > 0
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78"));

        public CategoryCardItem(int id, string name, string desc, string color, int products, int stock, int lowStock)
        {
            Id = id;
            Name = name;
            Description = desc;
            Color = color;
            ProductsCount = products;
            StockTotal = stock;
            LowStockCount = lowStock;
        }
    }
}
