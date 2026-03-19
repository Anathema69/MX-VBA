using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SistemaGestionProyectos2.Views
{
    public partial class InventoryWindow : Window
    {
        private readonly UserSession _currentUser;
        private List<CategoryCardItem> _allCategories = new();
        private List<ProductRowItem> _allProducts = new();
        private CategoryCardItem? _selectedCategory;
        private ProductRowItem? _editingProduct; // null = new, non-null = editing
        private CancellationTokenSource _cts = new();

        private static readonly string _logPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "inventory.log");

        private static void Log(string msg)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
                File.AppendAllText(_logPath, line + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine($"[Inventory] {msg}");
            }
            catch { }
        }

        private static readonly string[] _colorPalette =
        {
            "#3B82F6", "#10B981", "#8B5CF6", "#F59E0B",
            "#EC4899", "#EF4444", "#06B6D4", "#84CC16"
        };

        public InventoryWindow(UserSession user)
        {
            Log($"=== InventoryWindow INIT === user={user?.FullName ?? "null"}, id={user?.Id}");
            _currentUser = user;
            InitializeComponent();
            this.SourceInitialized += (s, e) => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);

            DataChangedEvent.Subscribe(this,
                new[] { DataChangedEvent.Topics.Inventory },
                () => LoadDataAsync());

            this.Closed += (_, _) =>
            {
                _cts?.Cancel();
                _cts?.Dispose();
                DataChangedEvent.Unsubscribe(this);
            };

            LoadDataAsync();
        }

        // ================================================================
        // DATA LOADING
        // ================================================================

        private async void LoadDataAsync()
        {
            Log("LoadDataAsync: START");
            try
            {
                Log("LoadDataAsync: calling GetInventoryCategorySummary...");
                var summaries = await SupabaseService.Instance.GetInventoryCategorySummary();
                Log($"LoadDataAsync: got {summaries.Count} summaries");

                _allCategories = summaries.Select(s =>
                {
                    Log($"  Category: id={s.Id}, name={s.Name}, color={s.Color}, products={s.TotalProducts}, stock={s.TotalStock}, low={s.LowStockCount}");
                    return new CategoryCardItem(
                        s.Id, s.Name, s.Description ?? "", s.Color ?? "#3498DB",
                        s.TotalProducts, (int)s.TotalStock, s.LowStockCount, s.TotalValue
                    );
                }).ToList();

                Log("LoadDataAsync: calling RefreshSidebar...");
                RefreshSidebar();

                // Re-select previously selected category
                if (_selectedCategory != null)
                {
                    var reselect = _allCategories.FirstOrDefault(c => c.Id == _selectedCategory.Id);
                    if (reselect != null)
                        CategoriesListBox.SelectedItem = reselect;
                }
                Log("LoadDataAsync: DONE OK");
            }
            catch (Exception ex)
            {
                Log($"LoadDataAsync: ERROR - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                if (ex.InnerException != null)
                    Log($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                ShowToast($"Error al cargar: {ex.Message}", ToastType.Error);
            }
        }

        private void RefreshSidebar(string? filter = null)
        {
            if (CategoriesListBox == null) return; // UI not ready yet
            var items = _allCategories.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter))
                items = items.Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

            CategoriesListBox.ItemsSource = items.ToList();

            // Global KPIs (always from full list, not filtered)
            TotalProductsText.Text = _allCategories.Sum(c => c.ProductsCount).ToString();
            LowStockTotalText.Text = _allCategories.Sum(c => c.LowStockCount).ToString();
            TotalCategoriesText.Text = _allCategories.Count.ToString();
            TotalValueText.Text = $"${_allCategories.Sum(c => c.TotalValue):N0}";
        }

        // ================================================================
        // CATEGORY SELECTION → LOAD PRODUCTS
        // ================================================================

        private async void CategoriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoriesListBox.SelectedItem is not CategoryCardItem cat)
                return;

            Log($"CategorySelected: id={cat.Id}, name={cat.Name}");
            _selectedCategory = cat;
            HideInlineProductForm();

            // Update detail header
            DetailCatName.Text = cat.Name;
            DetailCatDesc.Text = cat.Description;
            DetailColorDot.Fill = cat.ColorBrush;
            UpdateDetailStats(cat);

            NoCategoryState.Visibility = Visibility.Collapsed;

            try
            {
                Log($"CategorySelected: loading products for category {cat.Id}...");
                var products = await SupabaseService.Instance.GetInventoryProductsByCategory(cat.Id);
                Log($"CategorySelected: got {products.Count} products from DB");

                _allProducts = new List<ProductRowItem>();
                foreach (var p in products)
                {
                    try
                    {
                        Log($"  Product: id={p.Id}, code={p.Code}, name={p.Name}, stock={p.StockCurrent}, min={p.StockMinimum}");
                        _allProducts.Add(new ProductRowItem(p));
                    }
                    catch (Exception pex)
                    {
                        Log($"  ERROR mapping product id={p.Id}: {pex.GetType().Name}: {pex.Message}");
                    }
                }

                Log($"CategorySelected: mapped {_allProducts.Count} products, calling filters...");
                PopulateLocationFilter();
                ApplyProductFilters();
                Log("CategorySelected: DONE OK");
            }
            catch (Exception ex)
            {
                Log($"CategorySelected: ERROR - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                if (ex.InnerException != null)
                    Log($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                ShowToast($"Error al cargar productos: {ex.Message}", ToastType.Error);
            }
        }

        private void UpdateDetailStats(CategoryCardItem cat)
        {
            if (DetailStatsPanel == null) return;
            DetailStatsPanel.Children.Clear();
            AddStatChip($"{cat.ProductsCount} productos", "#F3F4F6", "#6B7280");
            AddStatChip($"{cat.StockTotal:N0} unidades", "#F3F4F6", "#6B7280");

            if (cat.LowStockCount > 0)
                AddStatChip($"\u26A0 {cat.LowStockCount} por pedir", "#FFFBEB", "#D97706");
        }

        private void AddStatChip(string text, string bgHex, string fgHex)
        {
            var chip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 0)
            };
            chip.Child = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex))
            };
            DetailStatsPanel.Children.Add(chip);
        }

        // ================================================================
        // PRODUCT FILTERING
        // ================================================================

        private void PopulateLocationFilter()
        {
            if (LocationFilter == null) return; // UI not ready yet
            LocationFilter.Items.Clear();
            LocationFilter.Items.Add(new ComboBoxItem { Content = "Todas las ubicaciones", IsSelected = true });

            foreach (var loc in _allProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Location))
                .Select(p => p.Location).Distinct().OrderBy(l => l))
            {
                LocationFilter.Items.Add(new ComboBoxItem { Content = loc });
            }
            LocationFilter.SelectedIndex = 0;
        }

        private void ApplyProductFilters()
        {
            if (ProductsGrid == null) return; // UI not ready yet
            var items = _allProducts.AsEnumerable();

            var searchText = ProductSearchBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(searchText))
                items = items.Where(p =>
                    (p.Code ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (p.Name ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase));

            if (LowStockToggle?.IsChecked == true)
                items = items.Where(p => p.StockCurrent < p.StockMinimum);

            if (LocationFilter?.SelectedItem is ComboBoxItem sel &&
                sel.Content?.ToString() != "Todas las ubicaciones")
                items = items.Where(p => p.Location == sel.Content?.ToString());

            var filtered = items.ToList();
            ProductsGrid.ItemsSource = filtered;
            ProductsGrid.Visibility = filtered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyProductState.Visibility = filtered.Count == 0 && _selectedCategory != null
                ? Visibility.Visible : Visibility.Collapsed;

            ProductCountText.Text = $"Mostrando {filtered.Count} de {_allProducts.Count}";

            var lowCount = filtered.Count(p => p.StockCurrent < p.StockMinimum);
            FooterAlertText.Text = lowCount > 0 ? $"\u26A0 {lowCount} productos con stock bajo" : "";
            FooterCatValue.Text = $"${filtered.Sum(p => p.StockCurrent * p.UnitPrice):N2}";
        }

        private void ProductSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyProductFilters();
        private void FilterChanged(object sender, RoutedEventArgs e) => ApplyProductFilters();

        // ================================================================
        // INLINE CATEGORY CREATION
        // ================================================================

        private void NewCategory_Click(object sender, RoutedEventArgs e)
        {
            // Toggle form visibility
            if (InlineCategoryForm.Visibility == Visibility.Visible)
            {
                HideInlineCategoryForm();
                return;
            }

            NewCatNameBox.Text = "";
            NewCatDescBox.Text = "";
            InlineCategoryForm.Visibility = Visibility.Visible;
            NewCatNameBox.Focus();
        }

        private void CancelNewCategory_Click(object sender, RoutedEventArgs e)
        {
            HideInlineCategoryForm();
        }

        private async void SaveNewCategory_Click(object sender, RoutedEventArgs e)
        {
            var name = NewCatNameBox.Text?.Trim();
            Log($"SaveCategory: name='{name}', editing={_isEditingCategory}");
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowToast("El nombre es obligatorio", ToastType.Warning);
                NewCatNameBox.Focus();
                return;
            }

            try
            {
                if (_isEditingCategory && _selectedCategory != null)
                {
                    // UPDATE existing
                    var cat = new InventoryCategoryDb
                    {
                        Id = _selectedCategory.Id,
                        Name = name.ToUpperInvariant(),
                        Description = NewCatDescBox.Text?.Trim(),
                        Color = _selectedCategory.Color,
                        DisplayOrder = _selectedCategory.Id,
                        UpdatedBy = _currentUser?.Id
                    };
                    await SupabaseService.Instance.UpdateInventoryCategory(cat);
                    ShowToast($"Categoría \"{cat.Name}\" actualizada", ToastType.Success);
                }
                else
                {
                    // CREATE new
                    var autoColor = _colorPalette[_allCategories.Count % _colorPalette.Length];
                    var category = new InventoryCategoryDb
                    {
                        Name = name.ToUpperInvariant(),
                        Description = NewCatDescBox.Text?.Trim(),
                        Color = autoColor,
                        DisplayOrder = _allCategories.Count + 1,
                        CreatedBy = _currentUser?.Id
                    };
                    await SupabaseService.Instance.CreateInventoryCategory(category);
                    ShowToast($"Categoría \"{category.Name}\" creada", ToastType.Success);
                }

                HideInlineCategoryForm();
                LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Error: {ex.Message}", ToastType.Error);
            }
        }

        private void HideInlineCategoryForm()
        {
            InlineCategoryForm.Visibility = Visibility.Collapsed;
            _isEditingCategory = false;
            NewCatNameBox.Text = "";
            NewCatDescBox.Text = "";
        }

        // ================================================================
        // INLINE PRODUCT CREATION / EDITING
        // ================================================================

        private void NewProduct_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null)
            {
                ShowToast("Selecciona una categoría primero", ToastType.Warning);
                return;
            }

            if (InlineProductForm.Visibility == Visibility.Visible && _editingProduct == null)
            {
                HideInlineProductForm();
                return;
            }

            _editingProduct = null;
            ClearProductForm();
            InlineProductForm.Visibility = Visibility.Visible;
            NewProdCodeBox.Focus();
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProductRowItem product)
            {
                _editingProduct = product;

                // Fill form with existing data
                NewProdCodeBox.Text = product.Code;
                NewProdNameBox.Text = product.Name;
                NewProdDescBox.Text = product.Description;
                NewProdStockBox.Text = product.StockCurrent.ToString("F0");
                NewProdMinBox.Text = product.StockMinimum.ToString("F0");
                NewProdUnitBox.Text = product.Unit;
                NewProdPriceBox.Text = product.UnitPrice.ToString("F2");
                NewProdLocBox.Text = product.Location;

                InlineProductForm.Visibility = Visibility.Visible;
                NewProdNameBox.Focus();
            }
        }

        private void CancelNewProduct_Click(object sender, RoutedEventArgs e)
        {
            HideInlineProductForm();
        }

        private async void SaveNewProduct_Click(object sender, RoutedEventArgs e)
        {
            var code = NewProdCodeBox.Text?.Trim();
            var name = NewProdNameBox.Text?.Trim();
            Log($"SaveNewProduct: code='{code}', name='{name}', editing={_editingProduct != null}");

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                ShowToast("Código y nombre son obligatorios", ToastType.Warning);
                return;
            }

            if (!decimal.TryParse(NewProdStockBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
                stock = 0;
            if (!decimal.TryParse(NewProdMinBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var min))
                min = 0;
            if (!decimal.TryParse(NewProdPriceBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                price = 0;

            try
            {
                if (_editingProduct == null)
                {
                    // CREATE
                    var product = new InventoryProductDb
                    {
                        CategoryId = _selectedCategory!.Id,
                        Code = code,
                        Name = name,
                        Description = NewProdDescBox.Text?.Trim(),
                        StockCurrent = stock,
                        StockMinimum = min,
                        Unit = NewProdUnitBox.Text?.Trim() ?? "pza",
                        UnitPrice = price,
                        Location = NewProdLocBox.Text?.Trim(),
                        CreatedBy = _currentUser?.Id
                    };

                    await SupabaseService.Instance.CreateInventoryProduct(product);
                    ShowToast($"Producto \"{name}\" creado", ToastType.Success);
                }
                else
                {
                    // UPDATE
                    _editingProduct.Code = code;
                    _editingProduct.Name = name;
                    _editingProduct.Description = NewProdDescBox.Text?.Trim() ?? "";
                    _editingProduct.StockCurrent = stock;
                    _editingProduct.StockMinimum = min;
                    _editingProduct.Unit = NewProdUnitBox.Text?.Trim() ?? "pza";
                    _editingProduct.UnitPrice = price;
                    _editingProduct.Location = NewProdLocBox.Text?.Trim() ?? "";

                    await SupabaseService.Instance.UpdateInventoryProduct(_editingProduct.Id, new InventoryProductDb
                    {
                        Code = code,
                        Name = name,
                        Description = NewProdDescBox.Text?.Trim(),
                        StockCurrent = stock,
                        StockMinimum = min,
                        Unit = NewProdUnitBox.Text?.Trim() ?? "pza",
                        UnitPrice = price,
                        Location = NewProdLocBox.Text?.Trim(),
                        UpdatedBy = _currentUser?.Id
                    });
                    ShowToast($"Producto \"{name}\" actualizado", ToastType.Success);
                }

                HideInlineProductForm();
                // Reload to reflect changes
                LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowToast($"Error: {ex.Message}", ToastType.Error);
            }
        }

        private async void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProductRowItem product)
            {
                var result = MessageBox.Show(
                    $"¿Eliminar \"{product.Name}\"?\nEsta acción no se puede deshacer.",
                    "Confirmar eliminación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await SupabaseService.Instance.DeleteInventoryProduct(product.Id);
                        ShowToast($"\"{product.Name}\" eliminado", ToastType.Success);
                        LoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"Error: {ex.Message}", ToastType.Error);
                    }
                }
            }
        }

        private void HideInlineProductForm()
        {
            InlineProductForm.Visibility = Visibility.Collapsed;
            _editingProduct = null;
            ClearProductForm();
        }

        private void ClearProductForm()
        {
            NewProdCodeBox.Text = "";
            NewProdNameBox.Text = "";
            NewProdDescBox.Text = "";
            NewProdStockBox.Text = "0";
            NewProdMinBox.Text = "0";
            NewProdUnitBox.Text = "pza";
            NewProdPriceBox.Text = "0.00";
            NewProdLocBox.Text = "";
        }

        // ================================================================
        // OTHER ACTIONS
        // ================================================================

        private void BackButton_Click(object sender, RoutedEventArgs e) => Close();

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Limpiar búsquedas al refrescar
            if (SearchBox != null) SearchBox.Text = "";
            if (ProductSearchBox != null) ProductSearchBox.Text = "";
            if (LowStockToggle != null) LowStockToggle.IsChecked = false;
            LoadDataAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshSidebar(SearchBox.Text);
        }

        /// <summary>
        /// Enter = guardar, Escape = cancelar en formularios inline
        /// </summary>
        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (InlineCategoryForm?.Visibility == Visibility.Visible)
                {
                    SaveNewCategory_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (InlineProductForm?.Visibility == Visibility.Visible)
                {
                    SaveNewProduct_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (InlineCategoryForm?.Visibility == Visibility.Visible)
                {
                    HideInlineCategoryForm();
                    e.Handled = true;
                }
                else if (InlineProductForm?.Visibility == Visibility.Visible)
                {
                    HideInlineProductForm();
                    e.Handled = true;
                }
            }
        }

        private bool _isEditingCategory = false;

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null) return;

            _isEditingCategory = true;
            NewCatNameBox.Text = _selectedCategory.Name;
            NewCatDescBox.Text = _selectedCategory.Description;
            InlineCategoryForm.Visibility = Visibility.Visible;
            NewCatNameBox.Focus();
        }

        // ================================================================
        // TOAST NOTIFICATIONS
        // ================================================================

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

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var slideIn = new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

            toast.BeginAnimation(OpacityProperty, fadeIn);
            ((TranslateTransform)toast.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);

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
    }

    public enum ToastType { Info, Success, Warning, Error }

    // ================================================================
    // VIEW MODELS
    // ================================================================

    public class CategoryCardItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public int ProductsCount { get; set; }
        public int StockTotal { get; set; }
        public int LowStockCount { get; set; }
        public decimal TotalValue { get; set; }

        public Brush ColorBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Color));
        public Visibility LowStockBadgeVisibility => LowStockCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public Brush BadgeBg => LowStockCount > 0
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEB"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
        public Brush BadgeFg => LowStockCount > 0
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));

        public double HealthPercent => ProductsCount > 0
            ? ((ProductsCount - LowStockCount) / (double)ProductsCount) * 100
            : 100;

        public CategoryCardItem(int id, string name, string desc, string color,
            int products, int stock, int lowStock, decimal totalValue = 0m)
        {
            Id = id; Name = name; Description = desc; Color = color;
            ProductsCount = products; StockTotal = stock; LowStockCount = lowStock; TotalValue = totalValue;
        }
    }

    public class ProductRowItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal StockCurrent { get; set; }
        public decimal StockMinimum { get; set; }
        public string Unit { get; set; } = "pza";
        public decimal UnitPrice { get; set; }
        public string Location { get; set; } = "";

        public bool IsLowStock => StockCurrent < StockMinimum;

        public Brush StockForeground
        {
            get
            {
                if (StockMinimum <= 0) return _okBrush;
                var ratio = (double)(StockCurrent / StockMinimum);
                return ratio >= 1.5 ? _okBrush : ratio >= 0.8 ? _warnBrush : _dangerBrush;
            }
        }

        public string StockWarningIcon => IsLowStock ? "\uE7BA" : "";
        public Visibility StockWarningVisibility => IsLowStock ? Visibility.Visible : Visibility.Collapsed;
        public Brush StockBarBrush => StockForeground;
        public double StockBarWidth
        {
            get
            {
                if (StockMinimum <= 0) return 40;
                return Math.Max(4, Math.Min((double)(StockCurrent / StockMinimum), 1.0) * 40);
            }
        }

        private static readonly Brush _okBrush;
        private static readonly Brush _warnBrush;
        private static readonly Brush _dangerBrush;

        static ProductRowItem()
        {
            _okBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            _okBrush.Freeze();
            _warnBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
            _warnBrush.Freeze();
            _dangerBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            _dangerBrush.Freeze();
        }

        /// <summary>
        /// Constructor from InventoryProductDb.
        /// </summary>
        public ProductRowItem(InventoryProductDb p)
        {
            Id = p.Id;
            Code = p.Code ?? "";
            Name = p.Name ?? "";
            Description = p.Description ?? "";
            StockCurrent = p.StockCurrent;
            StockMinimum = p.StockMinimum;
            Unit = p.Unit ?? "pza";
            UnitPrice = p.UnitPrice;
            Location = p.Location ?? "";
        }

        public ProductRowItem() { }
    }
}
