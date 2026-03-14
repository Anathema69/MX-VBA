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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Core;
using SistemaGestionProyectos2.Services.Storage;

namespace SistemaGestionProyectos2.Views
{
    public partial class VendorDashboard_V2 : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private ObservableCollection<VendorCommissionCardViewModel> _allCommissions;
        private ObservableCollection<VendorCommissionCardViewModel> _filteredCommissions;
        private readonly CultureInfo _cultureMX = new CultureInfo("es-MX");
        private int? _vendorId;

        private static readonly Dictionary<string, string> ExtColors = new()
        {
            { ".pdf", "#FEF3C7" }, { ".doc", "#DBEAFE" }, { ".docx", "#DBEAFE" },
            { ".xls", "#D1FAE5" }, { ".xlsx", "#D1FAE5" }, { ".jpg", "#F3E8FF" },
            { ".jpeg", "#F3E8FF" }, { ".jfif", "#F3E8FF" }, { ".png", "#FEE2E2" }, { ".gif", "#DBEAFE" }
        };

        public VendorDashboard_V2(UserSession currentUser)
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

        // Chip click → preview
        private void FileChip_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var chip = sender as Border;
            var fileVm = chip?.Tag as FileItemViewModel;
            if (fileVm == null) return;

            var parentCommission = _filteredCommissions.FirstOrDefault(c => c.Files.Contains(fileVm));
            var fileList = parentCommission?.Files?.ToList() ?? new List<FileItemViewModel> { fileVm };
            int currentIndex = fileList.IndexOf(fileVm);
            ShowPreviewModal(fileList, currentIndex);
        }

        // Chip right-click → styled popup menu (matches V2 design)
        private void FileChip_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            var chip = sender as Border;
            var fileVm = chip?.Tag as FileItemViewModel;
            if (fileVm == null) return;

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = chip,
                AllowsTransparency = true
            };

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 4, 0, 0),
                MinWidth = 160,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0x1E, 0x29, 0x3B),
                    BlurRadius = 16, ShadowDepth = 4, Opacity = 0.12
                }
            };

            var sp = new StackPanel();

            // File name header
            var headerTb = new TextBlock
            {
                Text = fileVm.FileName,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180,
                Margin = new Thickness(10, 6, 10, 4)
            };
            sp.Children.Add(headerTb);
            sp.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), Margin = new Thickness(4, 2, 4, 2) });

            // Download option
            var dlRow = MakePopupMenuItem("\uE896", "Descargar", Color.FromRgb(0x5B, 0x3F, 0xF9));
            dlRow.MouseLeftButtonDown += async (s, args) =>
            {
                popup.IsOpen = false;
                var saveDialog = new SaveFileDialog { FileName = fileVm.FileName, Filter = "Todos los archivos|*.*" };
                if (saveDialog.ShowDialog() != true) return;
                try
                {
                    var bytes = await _supabaseService.DownloadOrderFile(fileVm.StoragePath);
                    File.WriteAllBytes(saveDialog.FileName, bytes);
                    ShowTemporaryNotification("Archivo descargado");
                }
                catch { ShowTemporaryNotification("Error al descargar"); }
            };
            sp.Children.Add(dlRow);

            // Preview option
            var previewRow = MakePopupMenuItem("\uE7B3", "Vista previa", Color.FromRgb(0x47, 0x55, 0x69));
            previewRow.MouseLeftButtonDown += (s, args) =>
            {
                popup.IsOpen = false;
                var parentCommission = _filteredCommissions.FirstOrDefault(c => c.Files.Contains(fileVm));
                var fileList = parentCommission?.Files?.ToList() ?? new List<FileItemViewModel> { fileVm };
                ShowPreviewModal(fileList, fileList.IndexOf(fileVm));
            };
            sp.Children.Add(previewRow);

            // Delete option (only if allowed)
            if (fileVm.CanDelete)
            {
                sp.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), Margin = new Thickness(4, 2, 4, 2) });
                var delRow = MakePopupMenuItem("\uE74D", "Eliminar", Color.FromRgb(0xEF, 0x44, 0x44));
                delRow.MouseLeftButtonDown += async (s, args) =>
                {
                    popup.IsOpen = false;
                    try
                    {
                        if (await _supabaseService.DeleteOrderFile(fileVm.FileId, fileVm.StoragePath))
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
                    }
                    catch { ShowTemporaryNotification("Error al eliminar"); }
                };
                sp.Children.Add(delRow);
            }

            card.Child = sp;
            popup.Child = card;
            popup.IsOpen = true;
        }

        /// <summary>Creates a styled popup menu row matching V2 design</summary>
        Border MakePopupMenuItem(string icon, string label, Color accentColor)
        {
            var row = new Border
            {
                Padding = new Thickness(10, 7, 14, 7),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };
            var rsp = new StackPanel { Orientation = Orientation.Horizontal };
            rsp.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            var labelTb = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                VerticalAlignment = VerticalAlignment.Center
            };
            rsp.Children.Add(labelTb);
            row.Child = rsp;
            row.MouseEnter += (s, me) => { row.Background = new SolidColorBrush(Color.FromRgb(0xF7, 0xFA, 0xFC)); labelTb.Foreground = new SolidColorBrush(accentColor); };
            row.MouseLeave += (s, me) => { row.Background = Brushes.Transparent; labelTb.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)); };
            return row;
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
                                            bitmap.DecodePixelWidth = 120;
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
            if (fileVm == null) return;

            try
            {
                var deleted = await _supabaseService.DeleteOrderFile(fileVm.FileId, fileVm.StoragePath);
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

            var parentCommission = _filteredCommissions.FirstOrDefault(c => c.Files.Contains(fileVm));
            var fileList = parentCommission?.Files?.ToList() ?? new List<FileItemViewModel> { fileVm };
            int currentIndex = fileList.IndexOf(fileVm);

            ShowPreviewModal(fileList, currentIndex);
        }

        private void ShowPreviewModal(List<FileItemViewModel> fileList, int startIndex)
        {
            if (fileList.Count == 0) return;
            int currentIndex = Math.Max(0, Math.Min(startIndex, fileList.Count - 1));

            // Zoom state
            double currentZoom = 1.0;
            const double zoomStep = 0.15;
            const double minZoom = 0.5;
            const double maxZoom = 5.0;
            var scaleTransform = new ScaleTransform(1, 1);
            Image activeImage = null;

            var modal = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(204, 24, 24, 27)),
                WindowState = WindowState.Maximized,
                Topmost = true
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.MouseLeftButtonDown += (s, ev) => modal.Close();

            // Content area
            var contentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 900
            };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.MouseLeftButtonDown += (s, ev) => ev.Handled = true;

            // Header
            var filenameText = new TextBlock { Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            var counterText = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(179, 255, 255, 255)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            var zoomText = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(179, 255, 255, 255)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };

            // ScrollViewer for pan when zoomed
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                MaxHeight = 700,
                Background = Brushes.Transparent
            };
            scrollViewer.MouseLeftButtonDown += (s, ev) => ev.Handled = true;
            Grid.SetRow(scrollViewer, 1);

            // Helper: update cursor based on zoom level
            Action updateCursor = () =>
            {
                scrollViewer.Cursor = currentZoom > 1.05
                    ? System.Windows.Input.Cursors.Hand
                    : System.Windows.Input.Cursors.Arrow;
            };

            // Pan with left-click drag when zoomed
            Point? panStart = null;
            double panOffsetH = 0, panOffsetV = 0;
            scrollViewer.PreviewMouseLeftButtonDown += (s, ev) =>
            {
                if (currentZoom > 1.05)
                {
                    panStart = ev.GetPosition(scrollViewer);
                    panOffsetH = scrollViewer.HorizontalOffset;
                    panOffsetV = scrollViewer.VerticalOffset;
                    scrollViewer.Cursor = System.Windows.Input.Cursors.ScrollAll;
                    scrollViewer.CaptureMouse();
                    ev.Handled = true;
                }
            };
            scrollViewer.PreviewMouseMove += (s, ev) =>
            {
                if (panStart.HasValue && scrollViewer.IsMouseCaptured)
                {
                    var pos = ev.GetPosition(scrollViewer);
                    scrollViewer.ScrollToHorizontalOffset(panOffsetH - (pos.X - panStart.Value.X));
                    scrollViewer.ScrollToVerticalOffset(panOffsetV - (pos.Y - panStart.Value.Y));
                }
            };
            scrollViewer.PreviewMouseLeftButtonUp += (s, ev) =>
            {
                if (panStart.HasValue)
                {
                    panStart = null;
                    scrollViewer.ReleaseMouseCapture();
                    updateCursor();
                }
            };

            var previewContainer = new Border { MinHeight = 300 };
            scrollViewer.Content = previewContainer;

            // Zoom pill text (persistente bottom-right)
            var zoomPillText = new TextBlock { Text = "100%", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            // Resolution text (en header)
            var resolutionText = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

            Action updateZoomUI = () =>
            {
                zoomPillText.Text = $"{(int)(currentZoom * 100)}%";
                updateCursor();
            };

            Action<double> applyZoom = (newZoom) =>
            {
                currentZoom = Math.Max(minZoom, Math.Min(maxZoom, newZoom));
                scaleTransform.ScaleX = currentZoom;
                scaleTransform.ScaleY = currentZoom;
                updateZoomUI();
            };

            // Mouse wheel zoom
            scrollViewer.PreviewMouseWheel += (s, ev) =>
            {
                ev.Handled = true;
                double delta = ev.Delta > 0 ? zoomStep : -zoomStep;
                applyZoom(currentZoom + delta);
            };

            // Double-click to reset zoom
            scrollViewer.MouseDoubleClick += (s, ev) =>
            {
                applyZoom(1.0);
                scrollViewer.ScrollToHorizontalOffset(0);
                scrollViewer.ScrollToVerticalOffset(0);
                ev.Handled = true;
            };

            Action updatePreview = () =>
            {
                var file = fileList[currentIndex];
                filenameText.Text = file.FileName;
                counterText.Text = $"{currentIndex + 1} / {fileList.Count}";

                // Reset zoom on navigation
                currentZoom = 1.0;
                scaleTransform = new ScaleTransform(1, 1);
                updateZoomUI();
                scrollViewer.ScrollToHorizontalOffset(0);
                scrollViewer.ScrollToVerticalOffset(0);

                // Resolution info
                resolutionText.Text = "";

                if (file.IsImage)
                {
                    var previewImage = new Image
                    {
                        Stretch = Stretch.Uniform,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        LayoutTransform = scaleTransform
                    };
                    activeImage = previewImage;

                    Action<BitmapSource> showResolution = (src) =>
                    {
                        if (src != null)
                            resolutionText.Text = $"{src.PixelWidth} x {src.PixelHeight} px  ·  {file.FileSizeFormatted}";
                    };

                    if (file.FullImageSource != null)
                    {
                        previewImage.Source = file.FullImageSource;
                        showResolution(file.FullImageSource);
                    }
                    else if (file.ThumbnailSource != null)
                    {
                        previewImage.Source = file.ThumbnailSource;

                        var capturedFile = file;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var bytes = await _supabaseService.DownloadOrderFile(capturedFile.StoragePath);
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    var fullBitmap = new BitmapImage();
                                    using (var ms = new MemoryStream(bytes))
                                    {
                                        fullBitmap.BeginInit();
                                        fullBitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        fullBitmap.StreamSource = ms;
                                        fullBitmap.EndInit();
                                        fullBitmap.Freeze();
                                    }
                                    capturedFile.FullImageSource = fullBitmap;
                                    if (fileList.Count > 0 && currentIndex < fileList.Count && fileList[currentIndex] == capturedFile)
                                    {
                                        previewImage.Source = fullBitmap;
                                        showResolution(fullBitmap);
                                    }
                                });
                            }
                            catch { }
                        });
                    }

                    previewContainer.Child = new Border
                    {
                        CornerRadius = new CornerRadius(12),
                        ClipToBounds = true,
                        Child = previewImage
                    };
                }
                else
                {
                    activeImage = null;
                    resolutionText.Text = file.FileSizeFormatted;
                    var ph = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    ph.Children.Add(new TextBlock { Text = file.FileIcon, FontSize = 64, HorizontalAlignment = HorizontalAlignment.Center });
                    ph.Children.Add(new TextBlock { Text = "Vista previa del documento", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });
                    ph.Children.Add(new TextBlock { Text = $"{file.FileName} — {file.FileSizeFormatted}", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
                    previewContainer.Child = new Border { Background = new SolidColorBrush(Color.FromRgb(244, 244, 245)), CornerRadius = new CornerRadius(12), Padding = new Thickness(120, 80, 120, 80), Child = ph };
                }
            };

            // ========== Header bar ==========
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };
            headerLeft.Children.Add(filenameText);
            headerLeft.Children.Add(counterText);
            headerLeft.Children.Add(resolutionText);
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

            Grid.SetRow(headerGrid, 0);
            contentGrid.Children.Add(headerGrid);
            contentGrid.Children.Add(scrollViewer);
            mainGrid.Children.Add(contentGrid);

            // ========== Navigation arrows ==========
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

            // ========== Close button ==========
            var closeBtn = new Button { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 20, 20, 0), Cursor = System.Windows.Input.Cursors.Hand };
            closeBtn.Template = CreateCloseButtonTemplate();
            closeBtn.Click += (s, ev) => modal.Close();
            mainGrid.Children.Add(closeBtn);

            // ========== Zoom pill (persistente bottom-right) ==========
            var zoomPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(140, 30, 30, 35)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 30, 50)
            };
            zoomPill.MouseLeftButtonDown += (s, ev) => ev.Handled = true;

            var zoomPillStack = new StackPanel { Orientation = Orientation.Horizontal };

            // [−] button
            var zoomOutBtn = new Button { Width = 28, Height = 28, Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(0) };
            var zoomOutTemplate = new ControlTemplate(typeof(Button));
            var zoomOutBorder = new FrameworkElementFactory(typeof(Border));
            zoomOutBorder.SetValue(Border.WidthProperty, 28.0);
            zoomOutBorder.SetValue(Border.HeightProperty, 28.0);
            zoomOutBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            zoomOutBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var zoomOutText = new FrameworkElementFactory(typeof(TextBlock));
            zoomOutText.SetValue(TextBlock.TextProperty, "−");
            zoomOutText.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            zoomOutText.SetValue(TextBlock.FontSizeProperty, 16.0);
            zoomOutText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            zoomOutText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            zoomOutText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            zoomOutBorder.AppendChild(zoomOutText);
            zoomOutTemplate.VisualTree = zoomOutBorder;
            zoomOutBtn.Template = zoomOutTemplate;
            zoomOutBtn.Click += (s, ev) => { applyZoom(currentZoom - zoomStep); ev.Handled = true; };
            zoomPillStack.Children.Add(zoomOutBtn);

            // [100%] text (clickable to reset)
            var zoomResetBtn = new Button { Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(0), MinWidth = 44 };
            var zoomResetTemplate = new ControlTemplate(typeof(Button));
            var zoomResetBorder = new FrameworkElementFactory(typeof(Border));
            zoomResetBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            zoomResetBorder.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));
            var zoomResetContent = new FrameworkElementFactory(typeof(ContentPresenter));
            zoomResetContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            zoomResetContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            zoomResetBorder.AppendChild(zoomResetContent);
            zoomResetTemplate.VisualTree = zoomResetBorder;
            zoomResetBtn.Template = zoomResetTemplate;
            zoomResetBtn.Content = zoomPillText;
            zoomResetBtn.Click += (s, ev) => { applyZoom(1.0); scrollViewer.ScrollToHorizontalOffset(0); scrollViewer.ScrollToVerticalOffset(0); ev.Handled = true; };
            zoomPillStack.Children.Add(zoomResetBtn);

            // [+] button
            var zoomInBtn = new Button { Width = 28, Height = 28, Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(0) };
            var zoomInTemplate = new ControlTemplate(typeof(Button));
            var zoomInBorder = new FrameworkElementFactory(typeof(Border));
            zoomInBorder.SetValue(Border.WidthProperty, 28.0);
            zoomInBorder.SetValue(Border.HeightProperty, 28.0);
            zoomInBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            zoomInBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var zoomInText = new FrameworkElementFactory(typeof(TextBlock));
            zoomInText.SetValue(TextBlock.TextProperty, "+");
            zoomInText.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            zoomInText.SetValue(TextBlock.FontSizeProperty, 16.0);
            zoomInText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            zoomInText.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            zoomInText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            zoomInBorder.AppendChild(zoomInText);
            zoomInTemplate.VisualTree = zoomInBorder;
            zoomInBtn.Template = zoomInTemplate;
            zoomInBtn.Click += (s, ev) => { applyZoom(currentZoom + zoomStep); ev.Handled = true; };
            zoomPillStack.Children.Add(zoomInBtn);

            zoomPill.Child = zoomPillStack;
            mainGrid.Children.Add(zoomPill);

            // ========== Hints bar (auto-fade after 4s) ==========
            var hintsPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(20, 8, 20, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 16),
                Opacity = 1
            };
            Grid.SetRow(hintsPanel, 0);
            var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var hintColor = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            var hintKeyColor = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
            Action<string, string> addHint = (key, desc) =>
            {
                if (hintsStack.Children.Count > 0)
                    hintsStack.Children.Add(new TextBlock { Text = "  ·  ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center });
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 2, 7, 2),
                    Margin = new Thickness(0, 0, 5, 0),
                    Child = new TextBlock { Text = key, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = hintKeyColor }
                });
                sp.Children.Add(new TextBlock { Text = desc, FontSize = 11, Foreground = hintColor, VerticalAlignment = VerticalAlignment.Center });
                hintsStack.Children.Add(sp);
            };
            addHint("Scroll", "Zoom");
            addHint("Doble clic", "Restablecer");
            if (fileList.Count > 1)
                addHint("← →", "Navegar");
            addHint("Esc", "Cerrar");
            hintsPanel.Child = hintsStack;
            mainGrid.Children.Add(hintsPanel);

            // Auto-fade hints after 4 seconds
            var fadeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            fadeTimer.Tick += (s, ev) =>
            {
                fadeTimer.Stop();
                var fadeAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(600));
                hintsPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            };
            fadeTimer.Start();

            // ========== Keyboard ==========
            modal.KeyDown += (s, ev) =>
            {
                if (ev.Key == System.Windows.Input.Key.Left) { currentIndex = (currentIndex - 1 + fileList.Count) % fileList.Count; updatePreview(); }
                else if (ev.Key == System.Windows.Input.Key.Right) { currentIndex = (currentIndex + 1) % fileList.Count; updatePreview(); }
                else if (ev.Key == System.Windows.Input.Key.Escape) modal.Close();
                else if (ev.Key == System.Windows.Input.Key.Add || ev.Key == System.Windows.Input.Key.OemPlus) applyZoom(currentZoom + zoomStep);
                else if (ev.Key == System.Windows.Input.Key.Subtract || ev.Key == System.Windows.Input.Key.OemMinus) applyZoom(currentZoom - zoomStep);
                else if (ev.Key == System.Windows.Input.Key.D0 || ev.Key == System.Windows.Input.Key.NumPad0) { applyZoom(1.0); scrollViewer.ScrollToHorizontalOffset(0); scrollViewer.ScrollToVerticalOffset(0); }
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

            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // 1. Cambiar estado de la ORDEN a LIBERADA (2)
                //    El trigger record_order_history registra automáticamente en order_history
                await supabaseClient.From<OrderDb>()
                    .Filter("f_order", Postgrest.Constants.Operator.Equals, commission.OrderId)
                    .Set(x => x.OrderStatus, 2) // LIBERADA
                    .Set(x => x.UpdatedBy, _currentUser.Id)
                    .Update();

                System.Diagnostics.Debug.WriteLine($"[VENDOR] Orden {commission.OrderId} ({commission.OrderNumber}) -> LIBERADA (2)");

                // 2. Cambiar comisión de draft → pending
                await supabaseClient.From<VendorCommissionPaymentDb>()
                    .Where(x => x.Id == commission.CommissionId)
                    .Set(x => x.PaymentStatus, "pending")
                    .Set(x => x.UpdatedBy, _currentUser.Id)
                    .Set(x => x.UpdatedAt, DateTime.Now)
                    .Update();

                System.Diagnostics.Debug.WriteLine($"[VENDOR] Comisión {commission.CommissionId} -> pending");

                ShowTemporaryNotification($"Orden {commission.OrderNumber} liberada exitosamente");
                await LoadVendorCommissions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR] Error en SolicitarLiberacion: {ex.Message}");
                ShowTemporaryNotification($"Error al liberar orden: {ex.Message}");
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

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            BaseSupabaseService.InvalidateAllCaches();
            await LoadVendorDataAsync();
        }

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
}
