using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Storage;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorDashboard : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<VendorCommissionCardViewModel> _allCommissions;
        private ObservableCollection<VendorCommissionCardViewModel> _filteredCommissions;
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");
        private int? _vendorId;

        // Colors for file type placeholders
        private static readonly Dictionary<string, string> ExtColors = new()
        {
            { ".pdf", "#FEF3C7" }, { ".doc", "#DBEAFE" }, { ".docx", "#DBEAFE" },
            { ".xls", "#D1FAE5" }, { ".xlsx", "#D1FAE5" }, { ".jpg", "#F3E8FF" },
            { ".jpeg", "#F3E8FF" }, { ".png", "#FEE2E2" }, { ".gif", "#DBEAFE" }
        };

        public VendorDashboard(UserSession currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _allCommissions = new ObservableCollection<VendorCommissionCardViewModel>();
            _filteredCommissions = new ObservableCollection<VendorCommissionCardViewModel>();

            MaximizeWithTaskbar();
            this.SourceInitialized += (s, e) => MaximizeWithTaskbar();

            InitializeUI();
            _ = LoadVendorDataAsync();
        }

        private void MaximizeWithTaskbar() => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);

        private void InitializeUI()
        {
            Title = $"Mis Comisiones - {_currentUser.FullName}";
            CommissionsItemsControl.ItemsSource = _filteredCommissions;
            VendorNameText.Text = _currentUser.FullName;
            VendorInitials.Text = GetInitials(_currentUser.FullName);
            StatusBarTime.Text = $"Ultima actualizacion: {DateTime.Now:dd/MM/yyyy HH:mm}";
        }

        private async Task LoadVendorDataAsync()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                var vendorResponse = await supabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.UserId == _currentUser.Id)
                    .Single();

                if (vendorResponse == null)
                {
                    ShowNoDataMessage("No se encontro informacion del vendedor");
                    return;
                }

                _vendorId = vendorResponse.Id;
                await LoadVendorCommissions();
            }
            catch (Exception ex)
            {
                ShowTemporaryNotification("Error al cargar datos");
            }
        }

        private async Task LoadVendorCommissions()
        {
            if (!_vendorId.HasValue) return;

            try
            {
                var supabaseClient = _supabaseService.GetClient();

                var commissionsResponse = await supabaseClient
                    .From<VendorCommissionPaymentDb>()
                    .Select("*")
                    .Where(x => x.VendorId == _vendorId.Value)
                    .Filter("payment_status", Postgrest.Constants.Operator.In, new[] { "draft", "pending", "paid" })
                    .Order("f_order", Postgrest.Constants.Ordering.Descending)
                    .Get();

                var commissions = commissionsResponse?.Models ?? new List<VendorCommissionPaymentDb>();

                if (commissions.Count == 0)
                {
                    ShowNoDataMessage("No tienes comisiones registradas!");
                    _allCommissions.Clear();
                    ApplyFilters();
                    return;
                }

                HideNoDataMessage();

                var orderIds = commissions.Select(c => c.OrderId).Distinct().ToList();
                var commissionIds = commissions.Select(c => c.Id).ToList();

                var ordersTask = supabaseClient.From<OrderDb>().Select("*").Get();
                var clientsTask = supabaseClient.From<ClientDb>().Select("*").Get();
                var allFilesTask = supabaseClient
                    .From<OrderFileDb>()
                    .Filter("commission_id", Postgrest.Constants.Operator.In, commissionIds)
                    .Order("created_at", Postgrest.Constants.Ordering.Descending)
                    .Get();

                await Task.WhenAll(ordersTask, clientsTask, allFilesTask);

                var orders = (ordersTask.Result?.Models ?? new List<OrderDb>())
                    .Where(o => orderIds.Contains(o.Id)).ToDictionary(o => o.Id);
                var clientIds = orders.Values.Where(o => o.ClientId.HasValue)
                    .Select(o => o.ClientId.Value).Distinct().ToList();
                var clients = (clientsTask.Result?.Models ?? new List<ClientDb>())
                    .Where(c => clientIds.Contains(c.Id)).ToDictionary(c => c.Id);
                var allFiles = allFilesTask.Result?.Models ?? new List<OrderFileDb>();
                var filesByCommission = allFiles
                    .Where(f => f.CommissionId.HasValue)
                    .GroupBy(f => f.CommissionId.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                decimal totalPending = 0, totalPaid = 0, totalDraft = 0;
                int pendingCount = 0, paidCount = 0, draftCount = 0;
                var tempCommissions = new List<VendorCommissionCardViewModel>();

                foreach (var commission in commissions)
                {
                    OrderDb order = orders.ContainsKey(commission.OrderId) ? orders[commission.OrderId] : null;
                    ClientDb client = null;
                    if (order?.ClientId.HasValue == true && clients.ContainsKey(order.ClientId.Value))
                        client = clients[order.ClientId.Value];

                    var commissionFiles = filesByCommission.ContainsKey(commission.Id)
                        ? filesByCommission[commission.Id] : new List<OrderFileDb>();
                    bool canModifyFiles = commission.PaymentStatus != "paid";

                    var filesVm = new ObservableCollection<FileItemViewModel>(
                        commissionFiles.Select(f => CreateFileItemVm(f, canModifyFiles)));

                    var cardVm = new VendorCommissionCardViewModel
                    {
                        OrderId = commission.OrderId,
                        CommissionId = commission.Id,
                        OrderNumber = order?.Po ?? $"ORD-{commission.OrderId}",
                        OrderDescription = order?.Description ?? "",
                        OrderDate = order?.PoDate ?? DateTime.Now,
                        ClientName = client?.Name ?? "Sin Cliente",
                        CommissionAmount = commission.CommissionAmount,
                        CommissionAmountFormatted = commission.CommissionAmount.ToString("C", _cultureMX),
                        Status = commission.PaymentStatus,
                        PaymentDate = commission.PaymentDate,
                        Files = filesVm,
                        FileCount = filesVm.Count,
                        HasFiles = filesVm.Count > 0,
                        IsFilesExpanded = false
                    };

                    tempCommissions.Add(cardVm);

                    if (commission.PaymentStatus == "pending") { totalPending += commission.CommissionAmount; pendingCount++; }
                    else if (commission.PaymentStatus == "paid") { totalPaid += commission.CommissionAmount; paidCount++; }
                    else if (commission.PaymentStatus == "draft") { totalDraft += commission.CommissionAmount; draftCount++; }
                }

                _allCommissions = new ObservableCollection<VendorCommissionCardViewModel>(tempCommissions);

                TotalDraftText.Text = totalDraft.ToString("C", _cultureMX);
                TotalPendingText.Text = totalPending.ToString("C", _cultureMX);
                TotalPaidText.Text = totalPaid.ToString("C", _cultureMX);
                DraftCountText.Text = $"{draftCount} {(draftCount == 1 ? "orden" : "ordenes")}";
                PendingCountText.Text = $"{pendingCount} {(pendingCount == 1 ? "orden" : "ordenes")}";
                PaidCountText.Text = $"{paidCount} {(paidCount == 1 ? "orden" : "ordenes")}";
                StatusBarTime.Text = $"Ultima actualizacion: {DateTime.Now:dd/MM/yyyy HH:mm}";

                ApplyFilters();
                _ = LoadThumbnailsAsync(tempCommissions);
            }
            catch (Exception ex)
            {
                ShowTemporaryNotification("Error al cargar comisiones");
            }
        }

        private FileItemViewModel CreateFileItemVm(OrderFileDb f, bool canDelete)
        {
            var ext = Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "";
            var colorHex = ExtColors.ContainsKey(ext) ? ExtColors[ext] : "#F4F4F5";
            var isImage = StorageService.IsImageFile(f.FileName);

            return new FileItemViewModel
            {
                FileId = f.Id,
                FileName = f.FileName,
                StoragePath = f.StoragePath,
                FileSize = f.FileSize,
                FileSizeFormatted = StorageService.FormatFileSize(f.FileSize),
                ContentType = f.ContentType,
                CreatedAt = f.CreatedAt,
                IsImage = isImage,
                CanDelete = canDelete,
                FileIcon = isImage ? "🖼" : "📄",
                FileExt = ext.TrimStart('.').ToUpperInvariant(),
                PlaceholderColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                HasThumbnail = false
            };
        }

        private async Task LoadThumbnailsAsync(List<VendorCommissionCardViewModel> commissions)
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
                                bitmap.DecodePixelWidth = 130;
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

        private void ApplyFilters()
        {
            var sorted = _allCommissions
                .OrderBy(c => c.Status == "draft" ? 0 : c.Status == "pending" ? 1 : 2)
                .ThenByDescending(c => c.OrderDate)
                .ToList();

            CommissionsItemsControl.ItemsSource = null;
            _filteredCommissions = new ObservableCollection<VendorCommissionCardViewModel>(sorted);
            CommissionsItemsControl.ItemsSource = _filteredCommissions;
            NoDataPanel.Visibility = _filteredCommissions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ========== TOGGLE FILES ==========

        private void ToggleFiles_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as VendorCommissionCardViewModel;
            if (commission != null)
                commission.IsFilesExpanded = !commission.IsFilesExpanded;
        }

        // ========== FILE ACTIONS ==========

        private async void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as VendorCommissionCardViewModel;
            if (commission == null) return;

            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar archivo para subir",
                Filter = "Imagenes y documentos|*.jpg;*.jpeg;*.png;*.pdf;*.doc;*.docx;*.xls;*.xlsx|Todos los archivos|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true) return;

            int uploadedCount = 0;
            foreach (var filePath in dialog.FileNames)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 10 * 1024 * 1024)
                    {
                        ShowTemporaryNotification($"El archivo {fileInfo.Name} excede el limite de 10MB");
                        continue;
                    }

                    var uploaded = await _supabaseService.UploadOrderFile(
                        filePath, commission.OrderId, _currentUser.Id, _vendorId, commission.CommissionId);

                    if (uploaded != null)
                    {
                        var fileVm = CreateFileItemVm(uploaded, commission.Status != "paid");
                        commission.Files.Add(fileVm);
                        commission.FileCount = commission.Files.Count;
                        commission.HasFiles = true;
                        commission.IsFilesExpanded = true;
                        uploadedCount++;

                        if (fileVm.IsImage)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var bytes = await _supabaseService.DownloadOrderFile(uploaded.StoragePath);
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        var bitmap = new BitmapImage();
                                        using (var ms = new MemoryStream(bytes))
                                        {
                                            bitmap.BeginInit();
                                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                            bitmap.DecodePixelWidth = 130;
                                            bitmap.StreamSource = ms;
                                            bitmap.EndInit();
                                            bitmap.Freeze();
                                        }
                                        fileVm.ThumbnailSource = bitmap;
                                        fileVm.HasThumbnail = true;
                                    });
                                }
                                catch { }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowTemporaryNotification($"Error al subir {Path.GetFileName(filePath)}");
                }
            }

            if (uploadedCount > 0)
                ShowTemporaryNotification($"Se subieron {uploadedCount} archivo(s)");
        }

        private async void DeleteFileInline_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var fileVm = button?.Tag as FileItemViewModel;
            if (fileVm == null)
            {
                System.Diagnostics.Debug.WriteLine("[VendorDashboard] DeleteFile: fileVm is null (Tag binding failed)");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[VendorDashboard] DeleteFile: id={fileVm.FileId}, path={fileVm.StoragePath}");

            try
            {
                var deleted = await _supabaseService.DeleteOrderFile(fileVm.FileId, fileVm.StoragePath);
                System.Diagnostics.Debug.WriteLine($"[VendorDashboard] DeleteFile result: {deleted}");

                if (deleted)
                {
                    foreach (var commission in _filteredCommissions)
                    {
                        if (commission.Files.Remove(fileVm))
                        {
                            commission.FileCount = commission.Files.Count;
                            commission.HasFiles = commission.Files.Count > 0;
                            break;
                        }
                    }
                    ShowTemporaryNotification("Archivo eliminado");
                }
                else
                {
                    ShowTemporaryNotification("No se pudo eliminar el archivo");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VendorDashboard] DeleteFile error: {ex.Message}\n{ex.StackTrace}");
                ShowTemporaryNotification("Error al eliminar archivo");
            }
        }

        private async void DownloadFileInline_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var fileVm = button?.Tag as FileItemViewModel;
            if (fileVm == null) return;

            var saveDialog = new SaveFileDialog { FileName = fileVm.FileName, Filter = "Todos los archivos|*.*" };
            if (saveDialog.ShowDialog() != true) return;

            try
            {
                var bytes = await _supabaseService.DownloadOrderFile(fileVm.StoragePath);
                File.WriteAllBytes(saveDialog.FileName, bytes);
                ShowTemporaryNotification("Archivo descargado");
            }
            catch (Exception ex)
            {
                ShowTemporaryNotification($"Error al descargar archivo");
            }
        }

        private async void PreviewFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var fileVm = button?.Tag as FileItemViewModel;
            if (fileVm == null) return;

            // Find the parent commission to get the full file list
            var parentCommission = _filteredCommissions.FirstOrDefault(c => c.Files.Contains(fileVm));
            var fileList = parentCommission?.Files?.ToList() ?? new List<FileItemViewModel> { fileVm };
            int currentIndex = fileList.IndexOf(fileVm);

            ShowPreviewModal(fileList, currentIndex);
        }

        private void ShowPreviewModal(List<FileItemViewModel> fileList, int startIndex)
        {
            if (fileList.Count == 0) return;
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

            // Content area (blocks click-through)
            var contentPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 800
            };
            contentPanel.MouseLeftButtonDown += (s, ev) => ev.Handled = true;

            // Elements that update on navigation
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

            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var dlBtn = CreateModalButton("⬇ Descargar", false);
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
            actionsPanel.Children.Add(dlBtn);

            var delBtn = CreateModalButton("✕ Eliminar", true);
            delBtn.Margin = new Thickness(8, 0, 0, 0);
            delBtn.Click += async (s, ev) =>
            {
                var file = fileList[currentIndex];
                if (!file.CanDelete) return;
                var deleted = await _supabaseService.DeleteOrderFile(file.FileId, file.StoragePath);
                if (deleted)
                {
                    foreach (var c in _filteredCommissions)
                    {
                        if (c.Files.Remove(file))
                        {
                            c.FileCount = c.Files.Count;
                            c.HasFiles = c.Files.Count > 0;
                            break;
                        }
                    }
                    fileList.RemoveAt(currentIndex);
                    if (fileList.Count == 0) { modal.Close(); ShowTemporaryNotification("Archivo eliminado"); return; }
                    if (currentIndex >= fileList.Count) currentIndex = fileList.Count - 1;
                    updatePreview();
                    ShowTemporaryNotification("Archivo eliminado");
                }
            };
            actionsPanel.Children.Add(delBtn);
            Grid.SetColumn(actionsPanel, 1);
            headerGrid.Children.Add(actionsPanel);

            contentPanel.Children.Add(headerGrid);
            contentPanel.Children.Add(previewContainer);
            mainGrid.Children.Add(contentPanel);

            // Navigation arrows (left)
            if (fileList.Count > 1)
            {
                var leftArrow = CreateNavArrow("←", HorizontalAlignment.Left);
                leftArrow.Click += (s, ev) =>
                {
                    ev.Handled = true;
                    currentIndex = (currentIndex - 1 + fileList.Count) % fileList.Count;
                    updatePreview();
                };
                mainGrid.Children.Add(leftArrow);

                var rightArrow = CreateNavArrow("→", HorizontalAlignment.Right);
                rightArrow.Click += (s, ev) =>
                {
                    ev.Handled = true;
                    currentIndex = (currentIndex + 1) % fileList.Count;
                    updatePreview();
                };
                mainGrid.Children.Add(rightArrow);
            }

            // Close button
            var closeBtn = new Button { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 20, 20, 0), Cursor = System.Windows.Input.Cursors.Hand };
            closeBtn.Template = CreateCloseButtonTemplate();
            closeBtn.Click += (s, ev) => modal.Close();
            mainGrid.Children.Add(closeBtn);

            // Keyboard navigation
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

        private Button CreateNavArrow(string text, HorizontalAlignment alignment)
        {
            var btn = new Button
            {
                HorizontalAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(30, 0, 30, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, 48.0);
            border.SetValue(Border.HeightProperty, 48.0);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(24));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)));
            border.SetValue(Border.NameProperty, "Bd");
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

        private Button CreateModalButton(string text, bool isDanger)
        {
            var btn = new Button { Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(0) };
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.PaddingProperty, new Thickness(16, 8, 16, 8));
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetValue(TextBlock.TextProperty, text);
            tb.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            tb.SetValue(TextBlock.FontSizeProperty, 13.0);
            tb.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            border.AppendChild(tb);
            template.VisualTree = border;
            btn.Template = template;
            return btn;
        }

        private ControlTemplate CreateCloseButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, 36.0);
            border.SetValue(Border.HeightProperty, 36.0);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(18));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)));
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetValue(TextBlock.TextProperty, "✕");
            tb.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            tb.SetValue(TextBlock.FontSizeProperty, 18.0);
            tb.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            tb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(tb);
            template.VisualTree = border;
            return template;
        }

        private async void SolicitarLiberacion_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var commission = button?.Tag as VendorCommissionCardViewModel;
            if (commission == null || commission.Status != "draft") return;

            // TODO: Pendiente confirmacion admin - "Liberar" podria cambiar el estado de la orden a LIBERADA(2)
            // Por ahora solo cambia payment_status de la comision: draft -> pending
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                await supabaseClient.From<VendorCommissionPaymentDb>()
                    .Where(x => x.Id == commission.CommissionId)
                    .Set(x => x.PaymentStatus, "pending")
                    .Set(x => x.UpdatedBy, _currentUser.Id)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                ShowTemporaryNotification($"Liberacion solicitada para orden {commission.OrderNumber}");
                await LoadVendorCommissions();
            }
            catch (Exception ex)
            {
                ShowTemporaryNotification($"Error al solicitar liberacion");
            }
        }

        // ========== UI HELPERS ==========

        private void ShowTemporaryNotification(string message)
        {
            var notification = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 60, 0, 0),
                Child = new TextBlock { Text = message, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14 }
            };

            if (this.Content is Grid mainGrid)
            {
                mainGrid.Children.Add(notification);
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, ev) => { mainGrid.Children.Remove(notification); timer.Stop(); };
                timer.Start();
            }
        }

        private void ShowNoDataMessage(string message)
        {
            NoDataPanel.Visibility = Visibility.Visible;
            NoDataTitle.Text = "Sin comisiones";
            NoDataMessage.Text = message;
        }

        private void HideNoDataMessage() => NoDataPanel.Visibility = Visibility.Collapsed;

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            app.ForceLogout("Usuario cerro sesion manualmente");
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var words = name.Trim().Split(' ');
            if (words.Length >= 2) return $"{words[0][0]}{words[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }
    }

    // ========== VIEW MODELS ==========

    public class FileItemViewModel : INotifyPropertyChanged
    {
        private BitmapSource _thumbnailSource;
        private bool _hasThumbnail;

        public int FileId { get; set; }
        public string FileName { get; set; }
        public string StoragePath { get; set; }
        public long? FileSize { get; set; }
        public string FileSizeFormatted { get; set; }
        public string ContentType { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsImage { get; set; }
        public bool CanDelete { get; set; }
        public string FileIcon { get; set; }
        public string FileExt { get; set; }
        public Brush PlaceholderColor { get; set; }

        public BitmapSource ThumbnailSource
        {
            get => _thumbnailSource;
            set { _thumbnailSource = value; OnPropertyChanged(); }
        }

        public bool HasThumbnail
        {
            get => _hasThumbnail;
            set { _hasThumbnail = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class VendorCommissionCardViewModel : INotifyPropertyChanged
    {
        private int _fileCount;
        private bool _hasFiles;
        private string _status;
        private bool _isFilesExpanded;
        private ObservableCollection<FileItemViewModel> _files = new();

        public int OrderId { get; set; }
        public int CommissionId { get; set; }
        public string OrderNumber { get; set; }
        public string OrderDescription { get; set; }
        public DateTime OrderDate { get; set; }
        public string ClientName { get; set; }
        public decimal CommissionAmount { get; set; }
        public string CommissionAmountFormatted { get; set; }
        public DateTime? PaymentDate { get; set; }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
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
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
