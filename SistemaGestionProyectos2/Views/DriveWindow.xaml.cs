using Microsoft.Win32;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SistemaGestionProyectos2.Views
{
    public partial class DriveWindow : Window
    {
        private readonly UserSession _currentUser;
        private CancellationTokenSource _cts = new();

        // Navigation state
        private int? _currentFolderId;
        private List<DriveFolderDb> _breadcrumb = new();
        private List<DriveFolderDb> _currentFolders = new();
        private List<DriveFileDb> _currentFiles = new();

        // Navigation history (for mouse back/forward)
        private readonly Stack<int> _backHistory = new();
        private readonly Stack<int> _forwardHistory = new();

        // Selection mode (link folder to order)
        private bool _isSelectionMode;
        private int? _selectionOrderId;
        private string _selectionOrderPo;

        // Context menu target
        private object _contextTarget;

        // Colors
        private static readonly SolidColorBrush FolderColor = new(Color.FromRgb(255, 193, 7));   // #FFC107
        private static readonly SolidColorBrush PdfColor = new(Color.FromRgb(229, 57, 53));      // #E53935
        private static readonly SolidColorBrush ImageColor = new(Color.FromRgb(67, 160, 71));    // #43A047
        private static readonly SolidColorBrush WordColor = new(Color.FromRgb(21, 101, 192));    // #1565C0
        private static readonly SolidColorBrush ExcelColor = new(Color.FromRgb(46, 125, 50));    // #2E7D32
        private static readonly SolidColorBrush DefaultColor = new(Color.FromRgb(158, 158, 158));// #9E9E9E
        private static readonly SolidColorBrush HoverBg = new(Color.FromRgb(227, 242, 253));     // #E3F2FD
        private static readonly SolidColorBrush LinkedBorder = new(Color.FromRgb(25, 118, 210)); // #1976D2

        public DriveWindow(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;

            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            this.SourceInitialized += (s, e) => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);

            // Mouse back/forward buttons (XButton1 = back, XButton2 = forward)
            MouseDown += DriveWindow_MouseDown;

            Loaded += async (s, e) => await SafeLoadAsync(() => NavigateToRoot());
        }

        /// <summary>
        /// Open in selection mode: user picks a folder to link to an order
        /// </summary>
        public DriveWindow(UserSession user, int orderId, string orderPo) : this(user)
        {
            _isSelectionMode = true;
            _selectionOrderId = orderId;
            _selectionOrderPo = orderPo;

            SelectionBanner.Visibility = Visibility.Visible;
            SelectionBannerText.Text = $"Seleccione una carpeta para vincular a {orderPo}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            base.OnClosed(e);
        }

        // ===============================================
        // NAVIGATION
        // ===============================================

        private async Task NavigateToRoot()
        {
            // Find root folder (parent_id is null)
            var roots = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token);
            if (roots.Any())
            {
                await NavigateToFolder(roots.First().Id);
            }
            else
            {
                _currentFolderId = null;
                _breadcrumb.Clear();
                await LoadCurrentFolder();
            }
        }

        private async Task NavigateToFolder(int folderId, bool addToHistory = true)
        {
            if (addToHistory && _currentFolderId.HasValue && _currentFolderId.Value != folderId)
            {
                _backHistory.Push(_currentFolderId.Value);
                _forwardHistory.Clear();
            }

            _currentFolderId = folderId;
            _breadcrumb = await SupabaseService.Instance.GetDriveBreadcrumb(folderId, _cts.Token);
            await LoadCurrentFolder();
        }

        private async Task NavigateBack()
        {
            if (_backHistory.Count == 0) return;
            if (_currentFolderId.HasValue)
                _forwardHistory.Push(_currentFolderId.Value);
            var targetId = _backHistory.Pop();
            await NavigateToFolder(targetId, addToHistory: false);
        }

        private async Task NavigateForward()
        {
            if (_forwardHistory.Count == 0) return;
            if (_currentFolderId.HasValue)
                _backHistory.Push(_currentFolderId.Value);
            var targetId = _forwardHistory.Pop();
            await NavigateToFolder(targetId, addToHistory: false);
        }

        private async Task LoadCurrentFolder()
        {
            LoadingText.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            ContentPanel.Children.Clear();

            try
            {
                // Load folders and files in parallel
                var foldersTask = SupabaseService.Instance.GetDriveChildFolders(_currentFolderId, _cts.Token);
                var filesTask = _currentFolderId.HasValue
                    ? SupabaseService.Instance.GetDriveFilesByFolder(_currentFolderId.Value, _cts.Token)
                    : Task.FromResult(new List<DriveFileDb>());

                await Task.WhenAll(foldersTask, filesTask);

                _currentFolders = foldersTask.Result;
                _currentFiles = filesTask.Result;

                // Render breadcrumb
                RenderBreadcrumb();

                // Load order POs for linked folders
                var linkedOrderIds = _currentFolders
                    .Where(f => f.LinkedOrderId.HasValue)
                    .Select(f => f.LinkedOrderId.Value).Distinct().ToList();
                var orderPoMap = new Dictionary<int, string>();
                if (linkedOrderIds.Count > 0)
                {
                    foreach (var oid in linkedOrderIds)
                    {
                        try
                        {
                            var order = await SupabaseService.Instance.GetOrderById(oid);
                            if (order != null)
                                orderPoMap[oid] = order.Po ?? $"#{oid}";
                        }
                        catch { orderPoMap[oid] = $"#{oid}"; }
                    }
                }

                // Render items
                foreach (var folder in _currentFolders)
                    ContentPanel.Children.Add(CreateFolderCard(folder, orderPoMap));

                foreach (var file in _currentFiles)
                    ContentPanel.Children.Add(CreateFileCard(file));

                // Status
                var totalItems = _currentFolders.Count + _currentFiles.Count;
                StatusText.Text = $"{totalItems} elemento{(totalItems != 1 ? "s" : "")}";

                // Show linked order info
                if (_currentFolderId.HasValue)
                {
                    var currentFolder = _breadcrumb.LastOrDefault();
                    if (currentFolder?.LinkedOrderId != null)
                        LinkedOrderText.Text = $"Vinculada a orden #{currentFolder.LinkedOrderId}";
                    else
                        LinkedOrderText.Text = "";
                }

                // Empty state
                if (totalItems == 0)
                    EmptyState.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading folder: {ex.Message}");
                StatusText.Text = "Error al cargar carpeta";
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        // ===============================================
        // BREADCRUMB
        // ===============================================

        private void RenderBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();

            for (int i = 0; i < _breadcrumb.Count; i++)
            {
                var folder = _breadcrumb[i];
                var isLast = i == _breadcrumb.Count - 1;

                // Capture folderId in local variable to avoid closure issues
                var targetFolderId = folder.Id;

                var btn = new Button
                {
                    Content = folder.Name,
                    Style = FindResource("BreadcrumbButton") as Style,
                    Tag = targetFolderId,
                    FontWeight = isLast ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isLast
                        ? new SolidColorBrush(Color.FromRgb(33, 33, 33))
                        : new SolidColorBrush(Color.FromRgb(25, 118, 210))
                };

                if (!isLast)
                {
                    btn.Click += BreadcrumbItem_Click;
                }

                BreadcrumbPanel.Children.Add(btn);

                if (!isLast)
                {
                    BreadcrumbPanel.Children.Add(new TextBlock
                    {
                        Text = "  >  ",
                        Foreground = new SolidColorBrush(Color.FromRgb(189, 189, 189)),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 14
                    });
                }
            }
        }

        private async void BreadcrumbItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int folderId)
            {
                await SafeLoadAsync(() => NavigateToFolder(folderId));
            }
        }

        // ===============================================
        // CARD RENDERING
        // ===============================================

        private UIElement CreateFolderCard(DriveFolderDb folder, Dictionary<int, string> orderPoMap = null)
        {
            var isLinked = folder.LinkedOrderId.HasValue;

            var border = new Border
            {
                Style = FindResource("DriveCardStyle") as Style,
                BorderBrush = isLinked ? LinkedBorder : new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = isLinked ? new Thickness(2) : new Thickness(1),
                Tag = folder
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Folder icon
            var icon = new TextBlock
            {
                Text = "\uD83D\uDCC1",
                FontSize = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(icon, 0);
            grid.Children.Add(icon);

            // Name
            var name = new TextBlock
            {
                Text = folder.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 130,
                Margin = new Thickness(5, 2, 5, 0),
                ToolTip = folder.Name
            };
            Grid.SetRow(name, 1);
            grid.Children.Add(name);

            // Linked order badge
            if (isLinked)
            {
                var poText = orderPoMap != null && folder.LinkedOrderId.HasValue && orderPoMap.ContainsKey(folder.LinkedOrderId.Value)
                    ? orderPoMap[folder.LinkedOrderId.Value]
                    : $"#{folder.LinkedOrderId}";
                var badge = new TextBlock
                {
                    Text = poText,
                    FontSize = 10,
                    Foreground = LinkedBorder,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5),
                    ToolTip = $"Vinculada a orden {poText}"
                };
                Grid.SetRow(badge, 2);
                grid.Children.Add(badge);
            }
            else
            {
                var spacer = new TextBlock { Margin = new Thickness(0, 0, 0, 8) };
                Grid.SetRow(spacer, 2);
                grid.Children.Add(spacer);
            }

            border.Child = grid;

            // Events
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    _ = SafeLoadAsync(() => NavigateToFolder(folder.Id));
            };

            border.MouseRightButtonDown += (s, e) =>
            {
                _contextTarget = folder;
                ContextUnlinkItem.Visibility = isLinked ? Visibility.Visible : Visibility.Collapsed;

                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("Abrir", (_, _) => _ = SafeLoadAsync(() => NavigateToFolder(folder.Id))));
                menu.Items.Add(CreateMenuItem("Renombrar", (_, _) => RenameFolder(folder)));
                menu.Items.Add(new Separator());
                menu.Items.Add(CreateMenuItem("Vincular a Orden...", (_, _) => LinkFolderToOrder(folder)));
                if (isLinked)
                    menu.Items.Add(CreateMenuItem("Desvincular de Orden", async (_, _) => await UnlinkFolder(folder)));
                menu.Items.Add(new Separator());
                var deleteItem = CreateMenuItem("Eliminar", async (_, _) => await DeleteFolder(folder));
                deleteItem.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                menu.Items.Add(deleteItem);

                border.ContextMenu = menu;
            };

            // Hover effect
            border.MouseEnter += (s, e) => border.Background = HoverBg;
            border.MouseLeave += (s, e) => border.Background = Brushes.White;

            return border;
        }

        private UIElement CreateFileCard(DriveFileDb file)
        {
            var border = new Border
            {
                Style = FindResource("DriveCardStyle") as Style,
                Tag = file
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // File icon
            var icon = new TextBlock
            {
                Text = GetFileIconText(file.FileName),
                FontSize = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = GetFileIconColor(file.FileName)
            };
            Grid.SetRow(icon, 0);
            grid.Children.Add(icon);

            // Name
            var name = new TextBlock
            {
                Text = file.FileName,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 130,
                Margin = new Thickness(5, 2, 5, 0),
                ToolTip = file.FileName
            };
            Grid.SetRow(name, 1);
            grid.Children.Add(name);

            // Size
            var size = new TextBlock
            {
                Text = Services.Drive.DriveService.FormatFileSize(file.FileSize),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(size, 2);
            grid.Children.Add(size);

            border.Child = grid;

            // Double click: download
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    _ = DownloadFile(file);
            };

            // Right click: context menu
            border.MouseRightButtonDown += (s, e) =>
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("Descargar", async (_, _) => await DownloadFile(file)));
                menu.Items.Add(CreateMenuItem("Renombrar", (_, _) => RenameFile(file)));
                menu.Items.Add(new Separator());
                var deleteItem = CreateMenuItem("Eliminar", async (_, _) => await DeleteFile(file));
                deleteItem.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                menu.Items.Add(deleteItem);

                border.ContextMenu = menu;
            };

            // Hover
            border.MouseEnter += (s, e) => border.Background = HoverBg;
            border.MouseLeave += (s, e) => border.Background = Brushes.White;

            return border;
        }

        // ===============================================
        // ACTIONS
        // ===============================================

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var name = PromptInput("Nueva Carpeta", "Nombre de la carpeta:", "Nueva carpeta");
            if (string.IsNullOrWhiteSpace(name)) return;

            await SafeLoadAsync(async () =>
            {
                var created = await SupabaseService.Instance.CreateDriveFolder(name, _currentFolderId, _currentUser.Id, _cts.Token);
                if (created != null)
                    await LoadCurrentFolder();
                else
                    MessageBox.Show("No se pudo crear la carpeta. Verifique que no exista una con el mismo nombre.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private async void PurgeR2_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Se eliminaran TODOS los archivos del bucket R2.\nLos registros en la BD NO se tocan.\n\nContinuar?",
                "Purgar R2", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            StatusText.Text = "Purgando R2...";
            var count = await SupabaseService.Instance.PurgeDriveR2Files();
            StatusText.Text = count >= 0 ? $"R2 purgado: {count} archivos eliminados" : "Error al purgar R2";
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue)
            {
                MessageBox.Show("Abra una carpeta primero para subir archivos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!SupabaseService.Instance.IsDriveStorageConfigured)
            {
                MessageBox.Show("El almacenamiento en la nube (R2) no esta configurado.\nContacte al administrador.",
                    "Storage no configurado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar archivos para subir",
                Multiselect = true,
                Filter = "Todos los archivos (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            // Build upload tracker UI
            UploadPanel.Visibility = Visibility.Visible;
            UploadItemsPanel.Children.Clear();

            var fileTrackers = new Dictionary<string, (TextBlock status, ProgressBar bar)>();
            foreach (var filePath in dialog.FileNames)
            {
                var fileName = System.IO.Path.GetFileName(filePath);
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                var nameBlock = new TextBlock
                {
                    Text = fileName,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameBlock, 0);
                row.Children.Add(nameBlock);

                var statusBlock = new TextBlock
                {
                    Text = "Pendiente",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(statusBlock, 1);
                row.Children.Add(statusBlock);

                UploadItemsPanel.Children.Add(row);

                var progressBar = new ProgressBar
                {
                    Height = 3,
                    IsIndeterminate = false,
                    Value = 0,
                    Margin = new Thickness(0, 0, 0, 4),
                    Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210))
                };
                UploadItemsPanel.Children.Add(progressBar);

                fileTrackers[filePath] = (statusBlock, progressBar);
            }

            int uploaded = 0;
            int failed = 0;

            foreach (var filePath in dialog.FileNames)
            {
                var (status, bar) = fileTrackers[filePath];
                try
                {
                    status.Text = "Subiendo...";
                    status.Foreground = new SolidColorBrush(Color.FromRgb(25, 118, 210));
                    bar.IsIndeterminate = true;

                    await SupabaseService.Instance.UploadDriveFile(filePath, _currentFolderId.Value, _currentUser.Id, _cts.Token);

                    bar.IsIndeterminate = false;
                    bar.Value = 100;
                    bar.Foreground = new SolidColorBrush(Color.FromRgb(67, 160, 71));
                    status.Text = "Listo";
                    status.Foreground = new SolidColorBrush(Color.FromRgb(67, 160, 71));
                    uploaded++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error uploading {filePath}: {ex.Message}");
                    bar.IsIndeterminate = false;
                    bar.Value = 100;
                    bar.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                    status.Text = "Error";
                    status.Foreground = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                    failed++;
                }
            }

            StatusText.Text = failed > 0
                ? $"{uploaded} archivo(s) subido(s), {failed} fallido(s)"
                : $"{uploaded} archivo(s) subido(s)";

            await SafeLoadAsync(() => LoadCurrentFolder());

            // Auto-hide upload panel after 3 seconds
            _ = Task.Delay(3000).ContinueWith(_ =>
                Dispatcher.Invoke(() => UploadPanel.Visibility = Visibility.Collapsed));
        }

        private async Task DownloadFile(DriveFileDb file)
        {
            if (!SupabaseService.Instance.IsDriveStorageConfigured)
            {
                MessageBox.Show("El almacenamiento en la nube (R2) no esta configurado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Guardar archivo",
                FileName = file.FileName,
                Filter = "Todos los archivos (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            await SafeLoadAsync(async () =>
            {
                StatusText.Text = $"Descargando {file.FileName}...";
                var success = await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, dialog.FileName, _cts.Token);
                StatusText.Text = success ? $"{file.FileName} descargado" : "Error al descargar";
            });
        }

        private void RenameFolder(DriveFolderDb folder)
        {
            var newName = PromptInput("Renombrar Carpeta", "Nuevo nombre:", folder.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == folder.Name) return;

            _ = SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.RenameDriveFolder(folder.Id, newName, _cts.Token);
                if (success)
                    await LoadCurrentFolder();
                else
                    MessageBox.Show("No se pudo renombrar. Verifique que no exista otra carpeta con el mismo nombre.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void RenameFile(DriveFileDb file)
        {
            var newName = PromptInput("Renombrar Archivo", "Nuevo nombre:", file.FileName);
            if (string.IsNullOrWhiteSpace(newName) || newName == file.FileName) return;

            _ = SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.RenameDriveFile(file.Id, newName, _cts.Token);
                if (success)
                    await LoadCurrentFolder();
                else
                    MessageBox.Show("No se pudo renombrar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private async Task DeleteFolder(DriveFolderDb folder)
        {
            var result = MessageBox.Show(
                $"Se eliminara la carpeta \"{folder.Name}\" y TODO su contenido (subcarpetas y archivos).\n\nEsta accion no se puede deshacer.",
                "Confirmar eliminacion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            await SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.DeleteDriveFolder(folder.Id, _cts.Token);
                if (success)
                    await LoadCurrentFolder();
            });
        }

        private async Task DeleteFile(DriveFileDb file)
        {
            var result = MessageBox.Show(
                $"Se eliminara el archivo \"{file.FileName}\".\n\nEsta accion no se puede deshacer.",
                "Confirmar eliminacion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            await SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.DeleteDriveFile(file.Id, _cts.Token);
                if (success)
                    await LoadCurrentFolder();
            });
        }

        // ===============================================
        // ORDER LINKING
        // ===============================================

        private async void LinkFolderToOrder(DriveFolderDb folder)
        {
            // Only root-level folders (direct children of IMA MECATRONICA root) can be linked
            var rootFolders = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token);
            var rootId = rootFolders.FirstOrDefault()?.Id;
            if (rootId == null || folder.ParentId != rootId)
            {
                MessageBox.Show("Solo las carpetas del primer nivel pueden vincularse a una orden.",
                    "No permitido", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Load recent orders for ComboBox
            var orders = await SupabaseService.Instance.GetOrders(200);
            if (orders == null || orders.Count == 0)
            {
                MessageBox.Show("No se encontraron ordenes.", "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get already-linked order IDs to exclude them
            var allLinkedFolders = await SupabaseService.Instance.GetDriveChildFolders(rootId, _cts.Token);
            var linkedOrderIds = allLinkedFolders
                .Where(f => f.LinkedOrderId.HasValue && f.Id != folder.Id)
                .Select(f => f.LinkedOrderId.Value)
                .ToHashSet();

            // Filter out orders that already have a folder linked (1:1)
            var availableOrders = orders.Where(o => !linkedOrderIds.Contains(o.Id)).ToList();

            if (availableOrders.Count == 0)
            {
                MessageBox.Show("Todas las ordenes ya tienen una carpeta vinculada.", "Sin ordenes disponibles",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Load clients for display
            var clients = await SupabaseService.Instance.GetClients();
            var clientNames = clients?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();

            // Show dialog with ComboBox
            var selectedOrder = ShowOrderSelectionDialog(availableOrders, clientNames);
            if (selectedOrder == null) return;

            await SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.LinkDriveFolderToOrder(folder.Id, selectedOrder.Id, _cts.Token);
                if (success)
                {
                    StatusText.Text = $"Carpeta vinculada a {selectedOrder.Po ?? $"Orden #{selectedOrder.Id}"}";
                    await LoadCurrentFolder();
                }
            });
        }

        private static Models.Database.OrderDb ShowOrderSelectionDialog(List<Models.Database.OrderDb> orders, Dictionary<int, string> clientNames)
        {
            var dialog = new Window
            {
                Title = "Vincular a Orden",
                Width = 520,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock
            {
                Text = "Seleccione la orden a vincular:",
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 14
            });

            // Search filter
            var searchBox = new TextBox
            {
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            };

            // Placeholder via GotFocus/LostFocus
            searchBox.Text = "Buscar por OC, cliente o detalle...";
            searchBox.Foreground = Brushes.Gray;
            searchBox.GotFocus += (s, e) =>
            {
                if (searchBox.Foreground == Brushes.Gray)
                {
                    searchBox.Text = "";
                    searchBox.Foreground = Brushes.Black;
                }
            };
            searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    searchBox.Text = "Buscar por OC, cliente o detalle...";
                    searchBox.Foreground = Brushes.Gray;
                }
            };

            panel.Children.Add(searchBox);

            var combo = new ComboBox
            {
                FontSize = 12,
                Height = 30,
                IsEditable = false,
                DisplayMemberPath = "DisplayText"
            };

            // Build items with display text: OC | Cliente | Detalle truncado
            var items = orders.Select(o =>
            {
                var client = o.ClientId.HasValue && clientNames.ContainsKey(o.ClientId.Value)
                    ? clientNames[o.ClientId.Value] : "";
                var desc = Truncate(o.Description, 35);
                var display = $"{o.Po ?? "Sin OC"} | {Truncate(client, 20)}";
                if (!string.IsNullOrEmpty(desc))
                    display += $" | {desc}";

                return new { Order = o, DisplayText = display };
            }).ToList();

            combo.ItemsSource = items;
            if (items.Count > 0) combo.SelectedIndex = 0;

            // Filter on search
            searchBox.TextChanged += (s, e) =>
            {
                if (searchBox.Foreground == Brushes.Gray) return;
                var filter = searchBox.Text?.Trim().ToLowerInvariant() ?? "";
                var filtered = items.Where(i =>
                    i.DisplayText.ToLowerInvariant().Contains(filter)).ToList();
                combo.ItemsSource = filtered;
                if (filtered.Count > 0) combo.SelectedIndex = 0;
            };

            panel.Children.Add(combo);

            Models.Database.OrderDb result = null;

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okBtn = new Button
            {
                Content = "Vincular",
                Width = 90,
                Padding = new Thickness(0, 6, 0, 6),
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold
            };
            okBtn.Click += (s, e) =>
            {
                var sel = combo.SelectedItem;
                if (sel != null)
                {
                    result = ((dynamic)sel).Order;
                    dialog.Close();
                }
            };

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                Width = 80,
                Padding = new Thickness(0, 6, 0, 6),
                Margin = new Thickness(8, 0, 0, 0),
                IsCancel = true
            };

            buttonsPanel.Children.Add(okBtn);
            buttonsPanel.Children.Add(cancelBtn);
            panel.Children.Add(buttonsPanel);

            dialog.Content = panel;
            dialog.ShowDialog();

            return result;
        }

        private async Task UnlinkFolder(DriveFolderDb folder)
        {
            await SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.UnlinkDriveFolder(folder.Id, _cts.Token);
                if (success)
                {
                    StatusText.Text = "Carpeta desvinculada";
                    await LoadCurrentFolder();
                }
            });
        }

        private async void LinkThisFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue || !_selectionOrderId.HasValue) return;

            await SafeLoadAsync(async () =>
            {
                var success = await SupabaseService.Instance.LinkDriveFolderToOrder(
                    _currentFolderId.Value, _selectionOrderId.Value, _cts.Token);

                if (success)
                {
                    MessageBox.Show($"Carpeta vinculada a {_selectionOrderPo}", "Vinculado",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
            });
        }

        private void CancelSelection_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // ===============================================
        // CONTEXT MENU HANDLERS (from XAML - unused, using dynamic menus instead)
        // ===============================================

        private void ContextOpen_Click(object sender, RoutedEventArgs e) { }
        private void ContextRename_Click(object sender, RoutedEventArgs e) { }
        private void ContextLinkOrder_Click(object sender, RoutedEventArgs e) { }
        private void ContextUnlinkOrder_Click(object sender, RoutedEventArgs e) { }
        private void ContextDelete_Click(object sender, RoutedEventArgs e) { }

        // ===============================================
        // NAVIGATION HANDLERS
        // ===============================================

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void DriveWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                // Mouse back button → navigate back in history
                e.Handled = true;
                await SafeLoadAsync(() => NavigateBack());
            }
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                // Mouse forward button → navigate forward in history
                e.Handled = true;
                await SafeLoadAsync(() => NavigateForward());
            }
        }

        // ===============================================
        // HELPERS
        // ===============================================

        private async Task SafeLoadAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DriveWindow] Error: {ex.Message}");
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private static string PromptInput(string title, string label, string defaultValue)
        {
            // Simple input dialog using WPF
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 8) });

            var textBox = new TextBox { Text = defaultValue, FontSize = 14 };
            textBox.SelectAll();
            panel.Children.Add(textBox);

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            string result = null;

            var okBtn = new Button
            {
                Content = "Aceptar",
                Width = 80,
                Padding = new Thickness(0, 5, 0, 5),
                IsDefault = true,
                Background = new SolidColorBrush(Color.FromRgb(25, 118, 210)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okBtn.Click += (s, e) => { result = textBox.Text; dialog.Close(); };

            var cancelBtn = new Button
            {
                Content = "Cancelar",
                Width = 80,
                Padding = new Thickness(0, 5, 0, 5),
                Margin = new Thickness(8, 0, 0, 0),
                IsCancel = true
            };

            buttonsPanel.Children.Add(okBtn);
            buttonsPanel.Children.Add(cancelBtn);
            panel.Children.Add(buttonsPanel);

            dialog.Content = panel;
            dialog.Loaded += (s, e) => textBox.Focus();
            dialog.ShowDialog();

            return result;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        private static string GetFileIconText(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "PDF",
                ".doc" or ".docx" => "DOC",
                ".xls" or ".xlsx" => "XLS",
                ".ppt" or ".pptx" => "PPT",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "IMG",
                ".zip" or ".rar" => "ZIP",
                ".txt" or ".csv" => "TXT",
                ".dwg" or ".dxf" => "CAD",
                ".step" or ".stp" => "3D",
                _ => "FILE"
            };
        }

        private static SolidColorBrush GetFileIconColor(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => PdfColor,
                ".doc" or ".docx" => WordColor,
                ".xls" or ".xlsx" => ExcelColor,
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => ImageColor,
                _ => DefaultColor
            };
        }
    }
}
