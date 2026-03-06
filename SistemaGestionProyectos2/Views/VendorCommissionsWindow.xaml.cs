using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Storage;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorCommissionsWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<VendorSummaryViewModel> _vendors;
        private ObservableCollection<CommissionDetailViewModel> _vendorCommissions;
        private VendorSummaryViewModel _selectedVendor;
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");
        private CancellationTokenSource _cts = new();

        // Cache pre-cargado: ordenes y clientes para evitar queries al seleccionar vendedor
        private List<VendorCommissionPaymentDb> _allCommissionsCache;
        private Dictionary<int, OrderDb> _ordersCache;
        private Dictionary<int, ClientDb> _clientsCache;

        public VendorCommissionsWindow(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _vendors = new ObservableCollection<VendorSummaryViewModel>();
            _vendorCommissions = new ObservableCollection<CommissionDetailViewModel>();

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();
            this.SourceInitialized += (s, e) => MaximizeWithTaskbar();

            InitializeUI();
            _ = SafeLoadAsync(() => LoadVendorsWithCommissions());
        }

        private void MaximizeWithTaskbar()
        {
            // Usar helper multi-monitor (detecta el monitor actual, no solo el primario)
            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
        }

        private void InitializeUI()
        {
            Title = $"Gestión de Comisiones - {_currentUser.FullName}";
            VendorsListBox.ItemsSource = _vendors;
            CommissionsItemsControl.ItemsSource = _vendorCommissions;
        }

        private async Task LoadVendorsWithCommissions()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var supabaseClient = _supabaseService.GetClient();

                // 1. Cargar comisiones + vendedores + ordenes + clientes EN PARALELO
                var commissionsTask = supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(c => c.PaymentStatus == "draft" || c.PaymentStatus == "pending")
                    .Select("*")
                    .Order("f_vendor", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var ordersTask = supabaseClient
                    .From<OrderDb>()
                    .Select("f_order,f_po,f_podate,f_client,f_description,f_salesubtotal")
                    .Get();

                var clientsTask = supabaseClient
                    .From<ClientDb>()
                    .Select("f_client,f_name")
                    .Get();

                await Task.WhenAll(commissionsTask, ordersTask, clientsTask);

                var allCommissions = commissionsTask.Result?.Models ?? new List<VendorCommissionPaymentDb>();
                _allCommissionsCache = allCommissions;
                _ordersCache = (ordersTask.Result?.Models ?? new List<OrderDb>()).ToDictionary(o => o.Id);
                _clientsCache = (clientsTask.Result?.Models ?? new List<ClientDb>()).ToDictionary(c => c.Id);

                System.Diagnostics.Debug.WriteLine($"⏱️ [VendorCommissions] Queries paralelas: {sw.ElapsedMilliseconds}ms ({allCommissions.Count} comisiones, {_ordersCache.Count} ordenes, {_clientsCache.Count} clientes)");

                if (allCommissions.Count == 0)
                {
                    _vendors.Clear();
                    _vendorCommissions.Clear();
                    UpdateTotalPending(0);

                    EmptyStatePanel.Visibility = Visibility.Visible;
                    CommissionsDetailPanel.Visibility = Visibility.Collapsed;

                    var emptyPanel = EmptyStatePanel.Children[0] as StackPanel;
                    if (emptyPanel != null)
                    {
                        var textBlocks = emptyPanel.Children.OfType<TextBlock>().ToList();
                        if (textBlocks.Count >= 2)
                        {
                            textBlocks[1].Text = "No hay comisiones";
                            textBlocks[2].Text = "No se encontraron comisiones para ningún vendedor";
                        }
                    }
                    return;
                }

                // 2. Agrupar por vendedor
                var vendorGroups = allCommissions.GroupBy(c => c.VendorId);

                // 3. Cargar información de vendedores
                var vendorIds = vendorGroups.Select(g => g.Key).ToList();
                var vendorsResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Filter("f_vendor", Postgrest.Constants.Operator.In, vendorIds)
                    .Get();

                var vendors = vendorsResponse?.Models?.ToDictionary(v => v.Id) ?? new Dictionary<int, VendorTableDb>();
                System.Diagnostics.Debug.WriteLine($"⏱️ [VendorCommissions] Query vendedores: {sw.ElapsedMilliseconds}ms");

                // 4. Construir ViewModels de vendedores
                decimal totalPendingGlobal = 0;
                var tempVendors = new List<VendorSummaryViewModel>();

                foreach (var group in vendorGroups)
                {
                    var vendor = vendors.ContainsKey(group.Key) ? vendors[group.Key] : null;
                    if (vendor == null) continue;

                    var vendorCommissions = group.ToList();

                    // Separar por estado
                    var pendingCommissions = vendorCommissions.Where(c => c.PaymentStatus == "pending").ToList();
                    var draftCommissions = vendorCommissions.Where(c => c.PaymentStatus == "draft").ToList();

                    // Calcular totales
                    decimal totalPending = pendingCommissions.Sum(c => c.CommissionAmount);
                    decimal totalDraft = draftCommissions.Sum(c => c.CommissionAmount);
                    decimal totalAll = totalPending + totalDraft;

                    totalPendingGlobal += totalPending;

                    var vendorVm = new VendorSummaryViewModel
                    {
                        VendorId = vendor.Id,
                        VendorName = vendor.VendorName ?? "Sin nombre",
                        Initials = GetInitials(vendor.VendorName),
                        TotalCount = vendorCommissions.Count,  // Total de comisiones
                        PendingCount = pendingCommissions.Count,  // Mantener para lógica interna
                        DraftCount = draftCommissions.Count,  // Mantener para lógica interna
                        TotalPending = totalPending,
                        TotalDraft = totalDraft,
                        TotalAll = totalAll,
                        TotalPendingFormatted = totalPending.ToString("C", _cultureMX),
                        TotalDraftFormatted = totalDraft.ToString("C", _cultureMX),
                        TotalAllFormatted = totalAll.ToString("C", _cultureMX),
                        AvatarColor1 = GetRandomColor(vendor.Id),
                        AvatarColor2 = GetRandomColor(vendor.Id + 100),
                        HasPendingPayments = pendingCommissions.Count > 0
                    };

                    tempVendors.Add(vendorVm);
                }

                // 5. Ordenar: primero los que tienen comisiones pendientes, luego por monto total
                var sortedVendors = tempVendors
                    .OrderByDescending(v => v.HasPendingPayments)
                    .ThenByDescending(v => v.TotalPending)
                    .ThenByDescending(v => v.TotalAll)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"⏱️ [VendorCommissions] Procesamiento: {sw.ElapsedMilliseconds}ms");

                // Batch update: swap ItemsSource to avoid N individual Add notifications
                VendorsListBox.ItemsSource = null;
                _vendors = new ObservableCollection<VendorSummaryViewModel>(sortedVendors);
                VendorsListBox.ItemsSource = _vendors;

                UpdateTotalPending(totalPendingGlobal);

                System.Diagnostics.Debug.WriteLine($"⏱️ [VendorCommissions] Render vendedores: {sw.ElapsedMilliseconds}ms");

                // Seleccionar el primer vendedor automáticamente
                if (_vendors.Count > 0)
                {
                    VendorsListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar vendedores: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void VendorsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VendorsListBox.SelectedItem is VendorSummaryViewModel vendor)
            {
                _selectedVendor = vendor;
                await LoadVendorCommissions(vendor.VendorId);

                EmptyStatePanel.Visibility = Visibility.Collapsed;
                CommissionsDetailPanel.Visibility = Visibility.Visible;

                SelectedVendorInitials.Text = vendor.Initials;
                SelectedVendorName.Text = vendor.VendorName;

                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 1);
                gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(vendor.AvatarColor1), 0));
                gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(vendor.AvatarColor2), 1));
                SelectedVendorAvatar.Background = gradientBrush;
            }
        }

        private async Task LoadVendorCommissions(int vendorId)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Usar cache pre-cargado (0 queries de red)
                var commissions = _allCommissionsCache?
                    .Where(c => c.VendorId == vendorId)
                    .OrderByDescending(c => c.OrderId)
                    .ToList() ?? new List<VendorCommissionPaymentDb>();

                System.Diagnostics.Debug.WriteLine($"⏱️ [LoadCommissions] Filtrado local: {sw.ElapsedMilliseconds}ms ({commissions.Count} items, 0 queries)");

                if (commissions.Count == 0)
                {
                    _vendorCommissions.Clear();
                    UpdateVendorSummary(0, 0, 0);
                    return;
                }

                var orders = _ordersCache ?? new Dictionary<int, OrderDb>();
                var clients = _clientsCache ?? new Dictionary<int, ClientDb>();

                // Load all files for these commissions
                var commissionIds = commissions.Select(c => c.Id).ToList();

                var supabaseClient2 = _supabaseService.GetClient();
                var allFilesResponse = await supabaseClient2
                    .From<OrderFileDb>()
                    .Filter("commission_id", Postgrest.Constants.Operator.In, commissionIds)
                    .Order("created_at", Postgrest.Constants.Ordering.Descending)
                    .Get();
                var allFiles = allFilesResponse?.Models ?? new List<OrderFileDb>();
                var filesByCommission = allFiles
                    .Where(f => f.CommissionId.HasValue)
                    .GroupBy(f => f.CommissionId.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // 4. Construir ViewModels de comisiones
                decimal totalPending = 0;
                decimal totalDraft = 0;
                decimal totalAll = 0;
                int pendingCount = 0;
                int draftCount = 0;

                var commissionViewModels = new List<CommissionDetailViewModel>();

                foreach (var commission in commissions)
                {
                    OrderDb order = orders.ContainsKey(commission.OrderId) ? orders[commission.OrderId] : null;
                    ClientDb client = null;

                    if (order?.ClientId.HasValue == true && clients.ContainsKey(order.ClientId.Value))
                    {
                        client = clients[order.ClientId.Value];
                    }

                    var commissionFiles = filesByCommission.ContainsKey(commission.Id)
                        ? filesByCommission[commission.Id] : new List<OrderFileDb>();

                    var filesVm = new ObservableCollection<FileItemViewModel>(
                        commissionFiles.Select(f =>
                        {
                            var ext = Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "";
                            var colorHex = ExtColors.ContainsKey(ext) ? ExtColors[ext] : "#F4F4F5";
                            var isImage = StorageService.IsImageFile(f.FileName);
                            return new FileItemViewModel
                            {
                                FileId = f.Id, FileName = f.FileName, StoragePath = f.StoragePath,
                                FileSize = f.FileSize, FileSizeFormatted = StorageService.FormatFileSize(f.FileSize),
                                ContentType = f.ContentType, CreatedAt = f.CreatedAt,
                                IsImage = isImage, CanDelete = false,
                                FileIcon = isImage ? "🖼" : "📄",
                                FileExt = ext.TrimStart('.').ToUpperInvariant(),
                                PlaceholderColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                                HasThumbnail = false
                            };
                        }));

                    var commissionVm = new CommissionDetailViewModel
                    {
                        CommissionPaymentId = commission.Id,
                        OrderId = commission.OrderId,
                        VendorId = commission.VendorId,
                        OrderNumber = order?.Po ?? $"ORD-{commission.OrderId}",
                        OrderDescription = order?.Description ?? "",
                        OrderDate = order?.PoDate ?? DateTime.Now,
                        ClientName = client?.Name ?? "Sin Cliente",
                        Subtotal = order?.SaleSubtotal ?? 0,
                        SubtotalFormatted = (order?.SaleSubtotal ?? 0).ToString("C", _cultureMX),
                        CommissionRate = commission.CommissionRate,
                        CommissionAmount = commission.CommissionAmount,
                        CommissionAmountFormatted = commission.CommissionAmount.ToString("C", _cultureMX),
                        PaymentStatus = commission.PaymentStatus,
                        FileCount = filesVm.Count,
                        HasFiles = filesVm.Count > 0,
                        IsFilesExpanded = false,
                        Files = filesVm
                    };

                    commissionViewModels.Add(commissionVm);

                    if (commission.PaymentStatus == "pending")
                    {
                        totalPending += commission.CommissionAmount;
                        pendingCount++;
                    }
                    else if (commission.PaymentStatus == "draft")
                    {
                        totalDraft += commission.CommissionAmount;
                        draftCount++;
                    }
                }

                // 5. Ordenar: primero las pending, luego las draft, y dentro de cada grupo por fecha
                var sortedCommissions = commissionViewModels
                    .OrderBy(c => c.PaymentStatus == "pending" ? 0 : 1)
                    .ThenByDescending(c => c.OrderDate)
                    .ToList();

                // Batch update
                CommissionsItemsControl.ItemsSource = null;
                _vendorCommissions = new ObservableCollection<CommissionDetailViewModel>(sortedCommissions);
                CommissionsItemsControl.ItemsSource = _vendorCommissions;

                System.Diagnostics.Debug.WriteLine($"⏱️ [LoadCommissions] Total (local): {sw.ElapsedMilliseconds}ms ({sortedCommissions.Count} items)");
                totalAll = totalPending + totalDraft;

                // 6. Actualizar resúmenes
                VendorTotalPendingText.Text = totalPending.ToString("C", _cultureMX);
                VendorCommissionsCountText.Text = $"{pendingCount + draftCount}";
                VendorTotalAllText.Text = totalAll.ToString("C", _cultureMX);

                // Mostrar/ocultar botón de pagar todas (solo si hay pendientes)
                PayAllButton.Visibility = pendingCount > 1 ? Visibility.Visible : Visibility.Collapsed;

                // Load thumbnails in background
                _ = LoadCommissionThumbnailsAsync(sortedCommissions);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar comisiones: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateVendorSummary(decimal totalPending, int count, decimal totalAll)
        {
            VendorTotalPendingText.Text = totalPending.ToString("C", _cultureMX);
            VendorCommissionsCountText.Text = count.ToString();
            VendorTotalAllText.Text = totalAll.ToString("C", _cultureMX);
        }

        private void UpdateTotalPending(decimal total)
        {
            TotalPendingText.Text = total.ToString("C", _cultureMX);
        }

        // Resto de métodos sin cambios (PayCommission_Click, MarkCommissionAsPaid, etc.)
        private async void PayCommission_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as CommissionDetailViewModel;

            if (commission != null && commission.PaymentStatus == "pending")
            {
                await MarkCommissionAsPaid(commission.CommissionPaymentId, button);
            }
        }

        private async void PayAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedVendor == null || _vendorCommissions.Count == 0) return;

            // Solo pagar las que están en pending
            var pendingCommissions = _vendorCommissions.Where(c => c.PaymentStatus == "pending").ToList();
            if (pendingCommissions.Count > 0)
            {
                await MarkAllCommissionsAsPaid(pendingCommissions);
            }
        }

        private void ToggleFiles_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as CommissionDetailViewModel;
            if (commission != null)
                commission.IsFilesExpanded = !commission.IsFilesExpanded;
        }

        private void PreviewFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var fileVm = button?.Tag as FileItemViewModel;
            if (fileVm == null) return;

            // Find parent commission
            var parent = _vendorCommissions.FirstOrDefault(c => c.Files.Contains(fileVm));
            var fileList = parent?.Files?.ToList() ?? new List<FileItemViewModel> { fileVm };
            int idx = fileList.IndexOf(fileVm);
            ShowAdminPreviewModal(fileList, idx);
        }

        private async void DownloadFileInline_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var fileVm = button?.Tag as FileItemViewModel;
            if (fileVm == null) return;

            var saveDialog = new SaveFileDialog { FileName = fileVm.FileName };
            if (saveDialog.ShowDialog() != true) return;

            try
            {
                var bytes = await _supabaseService.DownloadOrderFile(fileVm.StoragePath);
                File.WriteAllBytes(saveDialog.FileName, bytes);
                ShowTemporaryNotification("Archivo descargado");
            }
            catch { ShowTemporaryNotification("Error al descargar"); }
        }

        private static readonly Dictionary<string, string> ExtColors = new()
        {
            { ".pdf", "#FEF3C7" }, { ".doc", "#DBEAFE" }, { ".docx", "#DBEAFE" },
            { ".xls", "#D1FAE5" }, { ".xlsx", "#D1FAE5" }, { ".jpg", "#F3E8FF" },
            { ".jpeg", "#F3E8FF" }, { ".png", "#FEE2E2" }, { ".gif", "#DBEAFE" }
        };

        private void ShowAdminPreviewModal(List<FileItemViewModel> fileList, int startIndex)
        {
            if (fileList == null || fileList.Count == 0) return;
            int currentIndex = Math.Max(0, Math.Min(startIndex, fileList.Count - 1));

            var modal = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(204, 24, 24, 27)),
                WindowState = WindowState.Maximized,
                Topmost = true
            };

            var mainGrid = new Grid();
            mainGrid.MouseLeftButtonDown += (s, ev) => modal.Close();

            var contentPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 800
            };
            contentPanel.MouseLeftButtonDown += (s, ev) => ev.Handled = true;

            var filenameText = new TextBlock { Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            var counterText = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(179, 255, 255, 255)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            var previewContainer = new Border { MinHeight = 300 };

            Action updatePreview = () =>
            {
                var file = fileList[currentIndex];
                filenameText.Text = file.FileName;
                counterText.Text = $"{currentIndex + 1} / {fileList.Count}";

                if (file.IsImage && file.ThumbnailSource != null)
                {
                    previewContainer.Child = new Border
                    {
                        CornerRadius = new CornerRadius(12), ClipToBounds = true,
                        Child = new Image { Source = file.ThumbnailSource, MaxHeight = 600, Stretch = Stretch.Uniform }
                    };
                }
                else
                {
                    var ph = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    ph.Children.Add(new TextBlock { Text = file.FileIcon, FontSize = 64, HorizontalAlignment = HorizontalAlignment.Center });
                    ph.Children.Add(new TextBlock { Text = "Vista previa del documento", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });
                    ph.Children.Add(new TextBlock { Text = $"{file.FileName} — {file.FileSizeFormatted}", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
                    previewContainer.Child = new Border { Background = new SolidColorBrush(Color.FromRgb(244, 244, 245)), CornerRadius = new CornerRadius(12), Padding = new Thickness(120, 80, 120, 80), Child = ph };
                }
            };

            // Header
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };
            headerLeft.Children.Add(filenameText);
            headerLeft.Children.Add(counterText);
            Grid.SetColumn(headerLeft, 0);
            headerGrid.Children.Add(headerLeft);

            // Download button in modal
            var dlBtn = new Button { Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(0) };
            var dlTemplate = new ControlTemplate(typeof(Button));
            var dlBorder = new FrameworkElementFactory(typeof(Border));
            dlBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)));
            dlBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            dlBorder.SetValue(Border.PaddingProperty, new Thickness(16, 8, 16, 8));
            dlBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)));
            dlBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var dlTb = new FrameworkElementFactory(typeof(TextBlock));
            dlTb.SetValue(TextBlock.TextProperty, "⬇ Descargar");
            dlTb.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            dlTb.SetValue(TextBlock.FontSizeProperty, 13.0);
            dlTb.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            dlBorder.AppendChild(dlTb);
            dlTemplate.VisualTree = dlBorder;
            dlBtn.Template = dlTemplate;
            dlBtn.Click += async (s, ev) =>
            {
                var file = fileList[currentIndex];
                var saveDialog = new SaveFileDialog { FileName = file.FileName };
                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        var bytes = await _supabaseService.DownloadOrderFile(file.StoragePath);
                        File.WriteAllBytes(saveDialog.FileName, bytes);
                        ShowTemporaryNotification("Archivo descargado");
                    }
                    catch { ShowTemporaryNotification("Error al descargar"); }
                }
            };
            Grid.SetColumn(dlBtn, 1);
            headerGrid.Children.Add(dlBtn);

            contentPanel.Children.Add(headerGrid);
            contentPanel.Children.Add(previewContainer);
            mainGrid.Children.Add(contentPanel);

            // Navigation arrows
            if (fileList.Count > 1)
            {
                var leftArrow = CreateNavButton("←", HorizontalAlignment.Left);
                leftArrow.Click += (s, ev) => { ev.Handled = true; currentIndex = (currentIndex - 1 + fileList.Count) % fileList.Count; updatePreview(); };
                mainGrid.Children.Add(leftArrow);

                var rightArrow = CreateNavButton("→", HorizontalAlignment.Right);
                rightArrow.Click += (s, ev) => { ev.Handled = true; currentIndex = (currentIndex + 1) % fileList.Count; updatePreview(); };
                mainGrid.Children.Add(rightArrow);
            }

            // Close
            var closeBtn = new Button { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 20, 20, 0), Cursor = System.Windows.Input.Cursors.Hand };
            var closeTemplate = new ControlTemplate(typeof(Button));
            var closeBorder = new FrameworkElementFactory(typeof(Border));
            closeBorder.SetValue(Border.WidthProperty, 36.0);
            closeBorder.SetValue(Border.HeightProperty, 36.0);
            closeBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            closeBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)));
            var closeTb = new FrameworkElementFactory(typeof(TextBlock));
            closeTb.SetValue(TextBlock.TextProperty, "✕");
            closeTb.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            closeTb.SetValue(TextBlock.FontSizeProperty, 18.0);
            closeTb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            closeTb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            closeBorder.AppendChild(closeTb);
            closeTemplate.VisualTree = closeBorder;
            closeBtn.Template = closeTemplate;
            closeBtn.Click += (s, ev) => modal.Close();
            mainGrid.Children.Add(closeBtn);

            // Keyboard nav
            modal.KeyDown += (s, ev) =>
            {
                if (ev.Key == System.Windows.Input.Key.Left) { currentIndex = (currentIndex - 1 + fileList.Count) % fileList.Count; updatePreview(); }
                else if (ev.Key == System.Windows.Input.Key.Right) { currentIndex = (currentIndex + 1) % fileList.Count; updatePreview(); }
                else if (ev.Key == System.Windows.Input.Key.Escape) modal.Close();
            };

            updatePreview();
            modal.Content = mainGrid;
            modal.ShowDialog();
        }

        private Button CreateNavButton(string text, HorizontalAlignment alignment)
        {
            var btn = new Button
            {
                HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(30, 0, 30, 0), Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(0)
            };
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, 48.0);
            border.SetValue(Border.HeightProperty, 48.0);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(24));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)));
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetValue(TextBlock.TextProperty, text);
            tb.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            tb.SetValue(TextBlock.FontSizeProperty, 22.0);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(tb);
            template.VisualTree = border;
            btn.Template = template;
            btn.MouseLeftButtonDown += (s, ev) => ev.Handled = true;
            return btn;
        }


        private async Task MarkCommissionAsPaid(int commissionPaymentId, Button button = null)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Guardar el vendedor actual
                var currentVendorId = _selectedVendor?.VendorId;

                var update = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Where(x => x.Id == commissionPaymentId)
                    .Set(x => x.PaymentStatus, "paid")
                    .Set(x => x.PaymentDate, DateTime.Now)
                    .Set(x => x.UpdatedBy, _currentUser.Id)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                if (update != null)
                {
                    if (button != null)
                    {
                        ShowSuccessAnimation(button);
                    }

                    await Task.Delay(1500);

                    // Recargar solo las comisiones del vendedor actual
                    if (_selectedVendor != null)
                    {
                        await LoadVendorCommissions(_selectedVendor.VendorId);
                    }

                    // Recargar la lista pero mantener la selección
                    await LoadVendorsWithCommissionsKeepingSelection(currentVendorId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al marcar como pagada: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Nuevo método para recargar vendedores manteniendo la selección sin salir del vendedor actual
        private async Task LoadVendorsWithCommissionsKeepingSelection(int? vendorIdToSelect)
        {
            // Llamar al método original de carga
            await LoadVendorsWithCommissions();

            // Restaurar la selección si había un vendedor seleccionado
            if (vendorIdToSelect.HasValue)
            {
                var vendorToSelect = _vendors.FirstOrDefault(v => v.VendorId == vendorIdToSelect.Value);
                if (vendorToSelect != null)
                {
                    VendorsListBox.SelectedItem = vendorToSelect;
                }
            }
        }

        private async Task MarkAllCommissionsAsPaid(List<CommissionDetailViewModel> pendingCommissions)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                var paymentDate = DateTime.Now;
                var currentVendorId = _selectedVendor?.VendorId;  // Guardar vendedor actual
                int successCount = 0;

                foreach (var commission in pendingCommissions)
                {
                    var update = await supabaseClient
                        .From<VendorCommissionPaymentDb>()
                        .Where(x => x.Id == commission.CommissionPaymentId)
                        .Set(x => x.PaymentStatus, "paid")
                        .Set(x => x.PaymentDate, paymentDate)
                        .Set(x => x.UpdatedBy, _currentUser.Id)
                        .Set(x => x.UpdatedAt, paymentDate)
                        .Update();

                    if (update != null) successCount++;
                }

                ShowTemporaryNotification($"✓ Se pagaron {successCount} comisiones correctamente");

                await Task.Delay(1500);
                await LoadVendorsWithCommissionsKeepingSelection(currentVendorId);  // Mantener selección
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al marcar como pagadas: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Métodos de utilidad sin cambios
        private void ShowSuccessAnimation(Button button)
        {
            button.IsEnabled = false;
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78"));
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "✓", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 8, 0), Foreground = Brushes.White },
                    new TextBlock { Text = "Pagado", FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }
                }
            };

            var fadeAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                BeginTime = TimeSpan.FromMilliseconds(500)
            };
            button.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        private void ShowTemporaryNotification(string message)
        {
            var notification = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#48BB78")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 50, 0, 0),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14
                }
            };

            if (this.Content is Grid mainGrid)
            {
                mainGrid.Children.Add(notification);
                Grid.SetColumnSpan(notification, 2);

                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
                notification.BeginAnimation(OpacityProperty, fadeIn);

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, e) =>
                {
                    var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
                    fadeOut.Completed += (s2, e2) => mainGrid.Children.Remove(notification);
                    notification.BeginAnimation(OpacityProperty, fadeOut);
                    timer.Stop();
                };
                timer.Start();
            }
        }

        // Eventos para edición de tasa - permite editar si NO está pagada
        private void CommissionRate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                var commission = textBox.Tag as CommissionDetailViewModel;
                // Permitir editar si el estado NO es "paid"
                if (commission != null && commission.PaymentStatus != "paid")
                {
                    commission.IsEditingRate = true;
                    textBox.IsReadOnly = false;
                    // Mostrar solo el número para edición fácil (sin %)
                    textBox.Text = commission.CommissionRate.ToString("F2");
                    textBox.Focus();
                    textBox.SelectAll(); // Seleccionar todo para reemplazar fácilmente
                }
            }
        }

        // Resto de eventos sin cambios...
        private async void CommissionRate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox != null && !textBox.IsReadOnly)
                {
                    await SaveCommissionRate(textBox);
                    Keyboard.ClearFocus();
                }
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    var commission = textBox.Tag as CommissionDetailViewModel;
                    if (commission != null)
                    {
                        textBox.Text = $"{commission.CommissionRate:F2}%";
                        commission.IsEditingRate = false;
                        textBox.IsReadOnly = true;
                        Keyboard.ClearFocus();
                    }
                }
            }
        }

        private async void CommissionRate_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsReadOnly)
            {
                await SaveCommissionRate(textBox);
            }
        }

        private void CommissionRate_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !regex.IsMatch(newText);
        }

        private async Task SaveCommissionRate(TextBox textBox)
        {
            var commission = textBox.Tag as CommissionDetailViewModel;
            // Permitir guardar si el estado NO es "paid"
            if (commission != null && commission.PaymentStatus != "paid")
            {
                string cleanText = textBox.Text.Replace("%", "").Trim();
                if (decimal.TryParse(cleanText, out decimal newRate) && newRate >= 0 && newRate <= 100)
                {
                    if (newRate != commission.CommissionRate)
                    {
                        // Guardar valores anteriores para posible rollback
                        decimal oldRate = commission.CommissionRate;
                        decimal oldAmount = commission.CommissionAmount;
                        decimal newCommissionAmount = Math.Round((commission.Subtotal * newRate) / 100, 2);

                        // ═══════════════════════════════════════════════════════════
                        // OPTIMISTIC UI: Actualizar UI inmediatamente (sin esperar BD)
                        // ═══════════════════════════════════════════════════════════
                        commission.CommissionRate = newRate;
                        commission.CommissionAmount = newCommissionAmount;
                        commission.CommissionAmountFormatted = newCommissionAmount.ToString("C", _cultureMX);
                        textBox.Text = $"{newRate:F2}%";
                        commission.IsEditingRate = false;
                        textBox.IsReadOnly = true;

                        // Feedback visual inmediato
                        textBox.Background = new SolidColorBrush(Color.FromArgb(50, 76, 175, 80));
                        ShowTemporaryNotification($"✓ Tasa actualizada: {oldRate:F2}% → {newRate:F2}%");

                        // Actualizar totales del vendedor en UI
                        UpdateVendorTotalsInUI(oldAmount, newCommissionAmount);

                        // ═══════════════════════════════════════════════════════════
                        // BACKGROUND: Guardar en BD en paralelo (sin bloquear UI)
                        // ═══════════════════════════════════════════════════════════
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var supabaseClient = _supabaseService.GetClient();
                                var now = DateTime.Now;

                                // Ejecutar las 3 operaciones en PARALELO
                                var historyTask = supabaseClient
                                    .From<CommissionRateHistoryDb>()
                                    .Insert(new CommissionRateHistoryDb
                                    {
                                        OrderId = commission.OrderId,
                                        VendorId = commission.VendorId,
                                        CommissionPaymentId = commission.CommissionPaymentId,
                                        OldRate = oldRate,
                                        OldAmount = oldAmount,
                                        NewRate = newRate,
                                        NewAmount = newCommissionAmount,
                                        OrderSubtotal = commission.Subtotal,
                                        OrderNumber = commission.OrderNumber,
                                        VendorName = _selectedVendor?.VendorName ?? "Desconocido",
                                        ChangedBy = _currentUser.Id,
                                        ChangedByName = _currentUser.FullName,
                                        ChangedAt = now,
                                        ChangeReason = $"Cambio manual de tasa: {oldRate:F2}% → {newRate:F2}%"
                                    });

                                var commissionTask = supabaseClient
                                    .From<VendorCommissionPaymentDb>()
                                    .Where(x => x.Id == commission.CommissionPaymentId)
                                    .Set(x => x.CommissionRate, newRate)
                                    .Set(x => x.CommissionAmount, newCommissionAmount)
                                    .Set(x => x.UpdatedBy, _currentUser.Id)
                                    .Set(x => x.UpdatedAt, now)
                                    .Update();

                                var orderTask = supabaseClient
                                    .From<OrderDb>()
                                    .Where(x => x.Id == commission.OrderId)
                                    .Set(x => x.CommissionRate, newRate)
                                    .Set(x => x.UpdatedBy, _currentUser.Id)
                                    .Set(x => x.UpdatedAt, now)
                                    .Update();

                                // Esperar todas en paralelo
                                await Task.WhenAll(historyTask, commissionTask, orderTask);
                            }
                            catch (Exception ex)
                            {
                                // Si falla, mostrar error y revertir en UI
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    commission.CommissionRate = oldRate;
                                    commission.CommissionAmount = oldAmount;
                                    commission.CommissionAmountFormatted = oldAmount.ToString("C", _cultureMX);
                                    textBox.Text = $"{oldRate:F2}%";
                                    UpdateVendorTotalsInUI(newCommissionAmount, oldAmount); // Revertir totales
                                    ShowTemporaryNotification($"⚠️ Error al guardar: {ex.Message}");
                                });
                            }
                        });

                        // Limpiar fondo después de un momento
                        await Task.Delay(300);
                        textBox.Background = Brushes.Transparent;
                        return;
                    }
                }
                else
                {
                    textBox.Text = $"{commission.CommissionRate:F2}%";
                }

                textBox.Text = $"{commission.CommissionRate:F2}%";
                commission.IsEditingRate = false;
                textBox.IsReadOnly = true;
            }
        }

        // Actualiza los totales del vendedor en la UI sin recargar de BD
        private void UpdateVendorTotalsInUI(decimal oldAmount, decimal newAmount)
        {
            if (_selectedVendor == null) return;

            decimal difference = newAmount - oldAmount;

            // Actualizar totales en el panel derecho
            if (decimal.TryParse(VendorTotalPendingText.Text.Replace("$", "").Replace(",", ""), out decimal currentPending))
            {
                VendorTotalPendingText.Text = (currentPending + difference).ToString("C", _cultureMX);
            }
            if (decimal.TryParse(VendorTotalAllText.Text.Replace("$", "").Replace(",", ""), out decimal currentAll))
            {
                VendorTotalAllText.Text = (currentAll + difference).ToString("C", _cultureMX);
            }

            // Actualizar en la lista de vendedores
            _selectedVendor.TotalPending += difference;
            _selectedVendor.TotalAll += difference;
            _selectedVendor.TotalPendingFormatted = _selectedVendor.TotalPending.ToString("C", _cultureMX);
            _selectedVendor.TotalAllFormatted = _selectedVendor.TotalAll.ToString("C", _cultureMX);
        }

        private async Task LoadCommissionThumbnailsAsync(List<CommissionDetailViewModel> commissions)
        {
            foreach (var commission in commissions)
            {
                foreach (var file in commission.Files)
                {
                    if (!file.IsImage) continue;
                    try
                    {
                        var bytes = await _supabaseService.DownloadOrderFile(file.StoragePath);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var bitmap = new BitmapImage();
                            using (var ms = new MemoryStream(bytes))
                            {
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.DecodePixelWidth = 120;
                                bitmap.StreamSource = ms;
                                bitmap.EndInit();
                                bitmap.Freeze();
                            }
                            file.ThumbnailSource = bitmap;
                            file.HasThumbnail = true;
                        });
                    }
                    catch { }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ManageVendorsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vendorManagementWindow = new VendorManagementWindow(_currentUser);
                vendorManagementWindow.ShowDialog();
                _ = LoadVendorsWithCommissions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir gestión de vendedores: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "??";

            var words = name.Trim().Split(' ');
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();

            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private string GetRandomColor(int seed)
        {
            var colors = new[]
            {
                "#5B3FF9", "#7c5ce6", "#e84118", "#c23616",
                "#00b894", "#00cec9", "#0984e3", "#74b9ff"
            };
            return colors[Math.Abs(seed) % colors.Length];
        }

        private async Task SafeLoadAsync(Func<Task> loadAction)
        {
            try
            {
                await loadAction();
            }
            catch (OperationCanceledException) { /* Window closed during load */ }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] Error in async load: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Cancel();
            _cts.Dispose();
            base.OnClosed(e);
        }
    }

    // ViewModels actualizados
    public class VendorSummaryViewModel : INotifyPropertyChanged
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string Initials { get; set; }
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int DraftCount { get; set; }
        public decimal TotalPending { get; set; }
        public decimal TotalDraft { get; set; }
        public decimal TotalAll { get; set; }
        public string TotalPendingFormatted { get; set; }
        public string TotalDraftFormatted { get; set; }
        public string TotalAllFormatted { get; set; }
        public string AvatarColor1 { get; set; }
        public string AvatarColor2 { get; set; }
        public bool HasPendingPayments { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CommissionDetailViewModel : INotifyPropertyChanged
    {
        private bool _isEditingRate;
        private decimal _commissionRate;
        private decimal _commissionAmount;
        private string _commissionAmountFormatted;
        private string _paymentStatus;
        private int _fileCount;
        private bool _hasFiles;
        private bool _isFilesExpanded;
        private ObservableCollection<FileItemViewModel> _files = new();

        public int CommissionPaymentId { get; set; }
        public int OrderId { get; set; }
        public int VendorId { get; set; }
        public string OrderNumber { get; set; }
        public string OrderDescription { get; set; }
        public DateTime OrderDate { get; set; }
        public string ClientName { get; set; }
        public decimal Subtotal { get; set; }
        public string SubtotalFormatted { get; set; }

        public string PaymentStatus
        {
            get => _paymentStatus;
            set
            {
                _paymentStatus = value;
                OnPropertyChanged();
            }
        }

        public decimal CommissionRate
        {
            get => _commissionRate;
            set
            {
                _commissionRate = value;
                OnPropertyChanged();
            }
        }

        public decimal CommissionAmount
        {
            get => _commissionAmount;
            set
            {
                _commissionAmount = value;
                OnPropertyChanged();
            }
        }

        public string CommissionAmountFormatted
        {
            get => _commissionAmountFormatted;
            set
            {
                _commissionAmountFormatted = value;
                OnPropertyChanged();
            }
        }

        public bool IsEditingRate
        {
            get => _isEditingRate;
            set
            {
                _isEditingRate = value;
                OnPropertyChanged();
            }
        }

        public int FileCount
        {
            get => _fileCount;
            set { _fileCount = value; OnPropertyChanged(); }
        }

        public bool HasFiles
        {
            get => _hasFiles;
            set { _hasFiles = value; OnPropertyChanged(); }
        }

        public bool IsFilesExpanded
        {
            get => _isFilesExpanded;
            set { _isFilesExpanded = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FileItemViewModel> Files
        {
            get => _files;
            set { _files = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}