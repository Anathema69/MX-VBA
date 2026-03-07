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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SistemaGestionProyectos2.Views
{
    public partial class DriveV2Window : Window
    {
        private readonly UserSession _currentUser;
        private CancellationTokenSource _cts = new();

        // Navigation
        private int? _currentFolderId;
        private List<DriveFolderDb> _breadcrumb = new();
        private List<DriveFolderDb> _currentFolders = new();
        private List<DriveFileDb> _currentFiles = new();
        private readonly Stack<int> _backHistory = new();
        private readonly Stack<int> _forwardHistory = new();

        // Selection mode
        private bool _isSelectionMode;
        private int? _selectionOrderId;
        private string _selectionOrderPo = "";

        // UI
        private string _activeNav = "all";
        private string _viewMode = "grid";
        private DriveFileDb? _selectedFile = null;
        private readonly List<Border> _navItems = new();

        // === CACHES (persist across navigations for performance) ===
        private readonly Dictionary<int, string> _userNameCache = new();
        private readonly Dictionary<int, (int fileCount, int subCount, long totalSize)> _statsCache = new();
        private readonly Dictionary<int, (string Po, string Client, string Detail)> _orderInfoCache = new();

        // ===============================================
        // THEME
        // ===============================================
        private static readonly SolidColorBrush Primary = Freeze(0x1D, 0x4E, 0xD8);
        private static readonly SolidColorBrush BorderColor = Freeze(0xE2, 0xE8, 0xF0);
        private static readonly SolidColorBrush BorderLight = Freeze(0xF1, 0xF5, 0xF9);
        private static readonly SolidColorBrush TextPrimary = Freeze(0x0F, 0x17, 0x2A);
        private static readonly SolidColorBrush TextSecondary = Freeze(0x47, 0x55, 0x69);
        private static readonly SolidColorBrush TextMuted = Freeze(0x64, 0x74, 0x8B);
        private static readonly SolidColorBrush TextLight = Freeze(0x94, 0xA3, 0xB8);
        private static readonly SolidColorBrush SlateLight = Freeze(0xCB, 0xD5, 0xE1);
        private static readonly SolidColorBrush HoverBg = Freeze(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush ActiveBg = Freeze(0xEF, 0xF6, 0xFF);
        private static readonly SolidColorBrush Background = Freeze(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush Destructive = Freeze(0xDC, 0x26, 0x26);
        private static readonly SolidColorBrush GreenOk = Freeze(0x43, 0xA0, 0x47);
        static SolidColorBrush Freeze(byte r, byte g, byte b) { var b2 = new SolidColorBrush(Color.FromRgb(r, g, b)); b2.Freeze(); return b2; }

        private static readonly string[] FolderAccentColors = { "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#06B6D4" };
        private static readonly Geometry FolderGeometry = Geometry.Parse("M10,4 H4 C2.9,4 2,4.9 2,6 V18 C2,19.1 2.9,20 4,20 H20 C21.1,20 22,19.1 22,18 V8 C22,6.9 21.1,6 20,6 H12 L10,4 Z");

        private static readonly Dictionary<string, (string color, string bg)> FileTypeConfig = new()
        {
            ["pdf"] = ("#EF4444", "#FEF2F2"), ["dwg"] = ("#8B5CF6", "#F5F3FF"), ["dxf"] = ("#8B5CF6", "#F5F3FF"),
            ["step"] = ("#8B5CF6", "#F5F3FF"), ["stp"] = ("#8B5CF6", "#F5F3FF"),
            ["xlsx"] = ("#10B981", "#F0FDF4"), ["xls"] = ("#10B981", "#F0FDF4"), ["csv"] = ("#10B981", "#F0FDF4"),
            ["docx"] = ("#3B82F6", "#EFF6FF"), ["doc"] = ("#3B82F6", "#EFF6FF"),
            ["pptx"] = ("#F59E0B", "#FFFBEB"), ["ppt"] = ("#F59E0B", "#FFFBEB"),
            ["mp4"] = ("#EC4899", "#FDF2F8"), ["zip"] = ("#F59E0B", "#FFFBEB"), ["rar"] = ("#F59E0B", "#FFFBEB"),
            ["jpg"] = ("#10B981", "#F0FDF4"), ["jpeg"] = ("#10B981", "#F0FDF4"), ["png"] = ("#10B981", "#F0FDF4"),
            ["gif"] = ("#10B981", "#F0FDF4"), ["bmp"] = ("#10B981", "#F0FDF4"), ["webp"] = ("#10B981", "#F0FDF4"),
            ["txt"] = ("#64748B", "#F8FAFC"), ["log"] = ("#64748B", "#F8FAFC"),
            ["html"] = ("#F59E0B", "#FFFBEB"), ["xml"] = ("#F59E0B", "#FFFBEB"), ["json"] = ("#F59E0B", "#FFFBEB"),
        };
        private static (string color, string bg) GetFileCfg(string fn) { var e = System.IO.Path.GetExtension(fn)?.TrimStart('.').ToLowerInvariant() ?? ""; return FileTypeConfig.TryGetValue(e, out var c) ? c : ("#64748B", "#F8FAFC"); }

        // ===============================================
        // CONSTRUCTORS
        // ===============================================
        public DriveV2Window(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;
            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            SourceInitialized += (s, e) => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            MouseDown += OnMouseNav;
            Loaded += async (s, e) => { InitializeSidebar(); UpdateViewToggle(); await SafeLoad(() => NavigateToRoot()); };
        }

        public DriveV2Window(UserSession user, int orderId, string orderPo) : this(user)
        {
            _isSelectionMode = true; _selectionOrderId = orderId; _selectionOrderPo = orderPo;
            SelectionBanner.Visibility = Visibility.Visible;
            SelectionBannerText.Text = $"Seleccione una carpeta para vincular a {orderPo}";
        }

        protected override void OnClosed(EventArgs e) { _cts?.Cancel(); _cts?.Dispose(); base.OnClosed(e); }

        // ===============================================
        // SIDEBAR
        // ===============================================
        private void InitializeSidebar()
        {
            foreach (var (id, ico, lbl) in new[] { ("all", "\uE80F", "Todos los archivos"), ("recent", "\uE823", "Recientes"), ("starred", "\uE734", "Destacados"), ("trash", "\uE74D", "Papelera") })
            { var it = MakeNavItem(id, ico, lbl); NavPanel.Children.Add(it); _navItems.Add(it); }
            SetActiveNav("all");
            foreach (var (id, clr, lbl, cnt) in new[] { ("pdf", "#EF4444", "PDFs", "--"), ("img", "#10B981", "Imagenes", "--"), ("cad", "#8B5CF6", "Archivos CAD", "--"), ("xls", "#10B981", "Hojas de calculo", "--"), ("vid", "#EC4899", "Videos", "--") })
                FilterPanel.Children.Add(MakeFilterItem(clr, lbl, cnt));
        }

        private Border MakeNavItem(string id, string ico, string lbl)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = id };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = ico, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = TextSecondary });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Foreground = TextSecondary });
            b.Child = sp;
            b.MouseLeftButtonDown += async (s, e) => { if (id == "all") { SetActiveNav(id); _selectedFile = null; HideDetail(); await SafeLoad(() => NavigateToRoot()); } };
            b.MouseEnter += (s, e) => { if (b.Tag as string != _activeNav) b.Background = HoverBg; };
            b.MouseLeave += (s, e) => { if (b.Tag as string != _activeNav) b.Background = Brushes.Transparent; };
            return b;
        }

        private void SetActiveNav(string id)
        {
            _activeNav = id;
            foreach (var it in _navItems) { var a = it.Tag as string == id; it.Background = a ? ActiveBg : Brushes.Transparent; if (it.Child is StackPanel sp) foreach (var c in sp.Children.OfType<TextBlock>()) c.Foreground = a ? Primary : TextSecondary; }
        }

        private UIElement MakeFilterItem(string clr, string lbl, string cnt)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            var g = new Grid();
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = BrushHex(clr), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(sp);
            g.Children.Add(new TextBlock { Text = cnt, FontSize = 12, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
            b.Child = g;
            b.MouseEnter += (s, e) => b.Background = HoverBg;
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            return b;
        }

        // ===============================================
        // NAVIGATION
        // ===============================================
        private async Task NavigateToRoot()
        {
            var roots = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token);
            if (roots.Any()) await NavigateToFolder(roots.First().Id);
            else { _currentFolderId = null; _breadcrumb.Clear(); await LoadCurrentFolder(); }
        }

        private async Task NavigateToFolder(int fId, bool history = true)
        {
            if (history && _currentFolderId.HasValue && _currentFolderId.Value != fId) { _backHistory.Push(_currentFolderId.Value); _forwardHistory.Clear(); }
            _currentFolderId = fId; _selectedFile = null; HideDetail();
            _breadcrumb = await SupabaseService.Instance.GetDriveBreadcrumb(fId, _cts.Token);
            await LoadCurrentFolder();
        }

        private async Task NavBack() { if (_backHistory.Count == 0) return; if (_currentFolderId.HasValue) _forwardHistory.Push(_currentFolderId.Value); await NavigateToFolder(_backHistory.Pop(), history: false); }
        private async Task NavFwd() { if (_forwardHistory.Count == 0) return; if (_currentFolderId.HasValue) _backHistory.Push(_currentFolderId.Value); await NavigateToFolder(_forwardHistory.Pop(), history: false); }

        // ===============================================
        // LOAD CURRENT FOLDER (with perf logging)
        // ===============================================
        private async Task LoadCurrentFolder()
        {
            var totalSw = Stopwatch.StartNew();
            Debug.WriteLine($"[DriveV2] === LoadCurrentFolder START (folderId={_currentFolderId}) ===");

            LoadingText.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            ContentHost.Content = null;

            try
            {
                // PHASE 1: Load folders + files (parallel)
                var sw = Stopwatch.StartNew();
                var fTask = SupabaseService.Instance.GetDriveChildFolders(_currentFolderId, _cts.Token);
                var fiTask = _currentFolderId.HasValue ? SupabaseService.Instance.GetDriveFilesByFolder(_currentFolderId.Value, _cts.Token) : Task.FromResult(new List<DriveFileDb>());
                await Task.WhenAll(fTask, fiTask);
                _currentFolders = fTask.Result; _currentFiles = fiTask.Result;
                Debug.WriteLine($"[DriveV2]   Phase1 Folders+Files: {sw.ElapsedMilliseconds}ms (folders={_currentFolders.Count}, files={_currentFiles.Count})");

                // PHASE 2: Load stats ONLY for uncached folders (parallel)
                sw.Restart();
                var uncached = _currentFolders.Where(f => !_statsCache.ContainsKey(f.Id)).ToList();
                if (uncached.Count > 0)
                {
                    var tasks = uncached.Select(async f =>
                    {
                        var files = await SupabaseService.Instance.GetDriveFilesByFolder(f.Id, _cts.Token);
                        var subs = await SupabaseService.Instance.GetDriveChildFolders(f.Id, _cts.Token);
                        return (f.Id, fc: files.Count, sc: subs.Count, sz: files.Sum(x => x.FileSize ?? 0));
                    });
                    foreach (var r in await Task.WhenAll(tasks))
                        _statsCache[r.Id] = (r.fc, r.sc, r.sz);
                }
                Debug.WriteLine($"[DriveV2]   Phase2 Stats: {sw.ElapsedMilliseconds}ms (queried={uncached.Count}, cached={_currentFolders.Count - uncached.Count})");

                // PHASE 3: Load order info ONLY for uncached linked orders (parallel)
                sw.Restart();
                var linkedIds = _currentFolders.Where(f => f.LinkedOrderId.HasValue).Select(f => f.LinkedOrderId!.Value).Distinct().ToList();
                var uncachedOrders = linkedIds.Where(id => !_orderInfoCache.ContainsKey(id)).ToList();
                if (uncachedOrders.Count > 0)
                {
                    var clients = await SupabaseService.Instance.GetClients();
                    var cMap = clients?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();

                    var oTasks = uncachedOrders.Select(async oid =>
                    {
                        try
                        {
                            var o = await SupabaseService.Instance.GetOrderById(oid);
                            if (o != null)
                            {
                                var client = o.ClientId.HasValue && cMap.ContainsKey(o.ClientId.Value) ? cMap[o.ClientId.Value] : "";
                                _orderInfoCache[oid] = (o.Po ?? $"#{oid}", client, Trunc(o.Description, 40));
                            }
                            else _orderInfoCache[oid] = ($"#{oid}", "", "");
                        }
                        catch { _orderInfoCache[oid] = ($"#{oid}", "", ""); }
                    });
                    await Task.WhenAll(oTasks);
                }
                Debug.WriteLine($"[DriveV2]   Phase3 Orders: {sw.ElapsedMilliseconds}ms (queried={uncachedOrders.Count}, cached={linkedIds.Count - uncachedOrders.Count})");

                // PHASE 4: Render
                sw.Restart();
                RenderBreadcrumb();
                var cur = _breadcrumb.LastOrDefault();
                BackToFoldersBtn.Visibility = _breadcrumb.Count <= 1 ? Visibility.Collapsed : Visibility.Visible;
                SectionTitle.Text = cur?.Name ?? "IMA Drive";

                var total = _currentFolders.Count + _currentFiles.Count;
                var p = new List<string>();
                if (_currentFolders.Count > 0) p.Add($"{_currentFolders.Count} carpeta{(_currentFolders.Count != 1 ? "s" : "")}");
                if (_currentFiles.Count > 0) p.Add($"{_currentFiles.Count} archivo{(_currentFiles.Count != 1 ? "s" : "")}");
                SectionSubtitle.Text = p.Count > 0 ? string.Join(" - ", p) : "Sin contenido";
                StatusText.Text = $"{total} elemento{(total != 1 ? "s" : "")}";

                RenderContent();
                if (total == 0) EmptyState.Visibility = Visibility.Visible;
                Debug.WriteLine($"[DriveV2]   Phase4 Render: {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DriveV2]   ERROR: {ex.Message}");
                StatusText.Text = "Error al cargar"; SectionTitle.Text = "Error"; SectionSubtitle.Text = ex.Message;
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
                Debug.WriteLine($"[DriveV2] === LoadCurrentFolder TOTAL: {totalSw.ElapsedMilliseconds}ms ===");
            }
        }

        private void InvalidateStatsCache(int? folderId = null)
        {
            if (folderId.HasValue) _statsCache.Remove(folderId.Value);
            else _statsCache.Clear();
            // Also invalidate parent
            if (_currentFolderId.HasValue) _statsCache.Remove(_currentFolderId.Value);
        }

        // ===============================================
        // BREADCRUMB
        // ===============================================
        private void RenderBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();
            var home = new TextBlock { Text = "\uE80F", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            home.MouseLeftButtonDown += async (s, e) => await SafeLoad(() => NavigateToRoot());
            BreadcrumbPanel.Children.Add(home);
            for (int i = 0; i < _breadcrumb.Count; i++)
            {
                var f = _breadcrumb[i]; var last = i == _breadcrumb.Count - 1; var fId = f.Id;
                BreadcrumbPanel.Children.Add(new TextBlock { Text = "\uE76C", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Foreground = TextLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
                var seg = new TextBlock { Text = f.Name, FontSize = 13, Foreground = last ? TextPrimary : TextMuted, FontWeight = last ? FontWeights.Medium : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Cursor = last ? Cursors.Arrow : Cursors.Hand };
                if (!last) { seg.MouseEnter += (s, e) => ((TextBlock)s!).Foreground = Primary; seg.MouseLeave += (s, e) => ((TextBlock)s!).Foreground = TextMuted; seg.MouseLeftButtonDown += async (s, e) => await SafeLoad(() => NavigateToFolder(fId)); }
                BreadcrumbPanel.Children.Add(seg);
            }
        }

        // ===============================================
        // CONTENT RENDERING
        // ===============================================
        private void RenderContent()
        {
            EmptyState.Visibility = Visibility.Collapsed;
            if (_currentFiles.Count == 0 && _currentFolders.Count > 0 && _viewMode == "list") RenderFolderList();
            else RenderGrid();
        }

        private void RenderGrid()
        {
            var g = new UniformGrid { Columns = _currentFiles.Count > 0 ? 4 : 3 };
            foreach (var f in _currentFolders) g.Children.Add(MakeFolderCard(f));
            foreach (var f in _currentFiles) g.Children.Add(MakeFileCard(f));
            ContentHost.Content = g;
        }

        private void RenderFolderList()
        {
            var wrap = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), ClipToBounds = true };
            var stack = new StackPanel();
            // Header
            var hg = new Grid { Background = Background, Height = 40 };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var hdrs = new[] { "NOMBRE", "ARCHIVOS", "TAMANO", "ORDEN VINCULADA", "MODIFICADO" };
            for (int i = 0; i < hdrs.Length; i++) { var h = new TextBlock { Text = hdrs[i], FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(i == 0 ? 24 : 0, 0, 0, 0) }; Grid.SetColumn(h, i); hg.Children.Add(h); }
            stack.Children.Add(new Border { BorderBrush = BorderColor, BorderThickness = new Thickness(0, 0, 0, 1), Child = hg });
            foreach (var f in _currentFolders) stack.Children.Add(MakeFolderListRow(f));
            wrap.Child = stack; ContentHost.Content = wrap;
        }

        private UIElement MakeFolderListRow(DriveFolderDb folder)
        {
            var ac = ColorHex(FolderAccentColors[folder.Id % FolderAccentColors.Length]);
            var rg = new Grid { Height = 56, Cursor = Cursors.Hand, Background = Brushes.White };
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            // Name
            var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
            var ib = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(38, ac.R, ac.G, ac.B)), Margin = new Thickness(0, 0, 12, 0) };
            ib.Child = MakeFolderIcon(16, new SolidColorBrush(ac));
            np.Children.Add(ib);
            var nt = new TextBlock { Text = folder.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center };
            np.Children.Add(nt);
            Grid.SetColumn(np, 0); rg.Children.Add(np);

            // Stats
            var hs = _statsCache.TryGetValue(folder.Id, out var st);
            Grid.SetColumn(new TextBlock { Text = hs ? st.fileCount.ToString() : "-", FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center }.Also(x => rg.Children.Add(x)), 1);
            Grid.SetColumn(new TextBlock { Text = hs ? Services.Drive.DriveService.FormatFileSize(st.totalSize) : "-", FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center }.Also(x => rg.Children.Add(x)), 2);

            // Order info
            var orderDisplay = GetOrderDisplayText(folder.LinkedOrderId);
            Grid.SetColumn(new TextBlock { Text = orderDisplay, FontSize = 12, Foreground = folder.LinkedOrderId.HasValue ? Primary : TextMuted, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 190, ToolTip = orderDisplay != "-" ? GetOrderTooltip(folder.LinkedOrderId) : null }.Also(x => rg.Children.Add(x)), 3);

            Grid.SetColumn(new TextBlock { Text = RelTime(folder.UpdatedAt), FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center }.Also(x => rg.Children.Add(x)), 4);

            var rb = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1), Child = rg };
            rb.MouseEnter += (s, e) => { rg.Background = HoverBg; nt.Foreground = Primary; };
            rb.MouseLeave += (s, e) => { rg.Background = Brushes.White; nt.Foreground = TextPrimary; };
            rb.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = SafeLoad(() => NavigateToFolder(folder.Id)); };
            return rb;
        }

        // ===============================================
        // FOLDER CARD
        // ===============================================
        private UIElement MakeFolderCard(DriveFolderDb folder)
        {
            var aHex = FolderAccentColors[folder.Id % FolderAccentColors.Length];
            var aC = ColorHex(aHex); var aB = new SolidColorBrush(aC);
            var tint = new SolidColorBrush(Color.FromArgb(38, aC.R, aC.G, aC.B));
            var linked = folder.LinkedOrderId.HasValue;

            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), Margin = new Thickness(8), Cursor = Cursors.Hand, ClipToBounds = true };
            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // R0: accent
            var acc = new Border { Background = aB, Height = 4 }; Grid.SetRow(acc, 0); mg.Children.Add(acc);

            // R1: header
            var hg = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBdr = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), Background = tint, Margin = new Thickness(0, 0, 12, 0) };
            iconBdr.Child = MakeFolderIcon(22, aB);
            Grid.SetColumn(iconBdr, 0); hg.Children.Add(iconBdr);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameT = new TextBlock { Text = folder.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200, Margin = new Thickness(0, 0, 0, 2), ToolTip = folder.Name };
            info.Children.Add(nameT);

            // Linked order: show "OC | Client"
            if (linked && _orderInfoCache.TryGetValue(folder.LinkedOrderId!.Value, out var oi))
            {
                var orderLine = !string.IsNullOrEmpty(oi.Client) ? $"{oi.Po} | {Trunc(oi.Client, 18)}" : oi.Po;
                info.Children.Add(new TextBlock { Text = orderLine, FontSize = 12, Foreground = Primary, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 200, ToolTip = GetOrderTooltip(folder.LinkedOrderId) });
            }
            else if (linked)
            {
                info.Children.Add(new TextBlock { Text = $"#{folder.LinkedOrderId}", FontSize = 12, Foreground = Primary });
            }
            Grid.SetColumn(info, 1); hg.Children.Add(info);

            var moreBtn = new Button { Style = FindResource("IconButton") as Style, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Top };
            moreBtn.Content = new TextBlock { Text = "\uE712", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = TextMuted };
            Grid.SetColumn(moreBtn, 2); hg.Children.Add(moreBtn);
            Grid.SetRow(hg, 1); mg.Children.Add(hg);

            // R2: stats
            var hs = _statsCache.TryGetValue(folder.Id, out var st);
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 20, 0) };
            sp.Children.Add(new TextBlock { Text = "\uE8A5", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = hs ? $"{st.fileCount} archivo{(st.fileCount != 1 ? "s" : "")}" : "...", FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center });
            if (hs && st.totalSize > 0)
            {
                sp.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = SlateLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) });
                sp.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(st.totalSize), FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center });
            }
            if (hs && st.subCount > 0)
            {
                sp.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = SlateLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) });
                sp.Children.Add(new TextBlock { Text = $"{st.subCount} subcarpeta{(st.subCount != 1 ? "s" : "")}", FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center });
            }
            Grid.SetRow(sp, 2); mg.Children.Add(sp);

            // R3: footer
            var ft = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(20, 16, 20, 0), Padding = new Thickness(0, 12, 0, 16) };
            ft.Child = new TextBlock { Text = $"Modificado {RelTime(folder.UpdatedAt)}", FontSize = 11, Foreground = TextLight };
            Grid.SetRow(ft, 3); mg.Children.Add(ft);

            card.Child = mg;

            // Hover
            card.MouseEnter += (s, e) => { card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 4, Opacity = 0.08 }; nameT.Foreground = Primary; moreBtn.Visibility = Visibility.Visible; };
            card.MouseLeave += (s, e) => { card.Effect = null; nameT.Foreground = TextPrimary; moreBtn.Visibility = Visibility.Collapsed; };
            card.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = SafeLoad(() => NavigateToFolder(folder.Id)); };

            // Context menu
            card.MouseRightButtonDown += (s, e) =>
            {
                var m = new ContextMenu();
                m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavigateToFolder(folder.Id))));
                m.Items.Add(MI("Renombrar", (_, _) => RenameFolder(folder)));
                m.Items.Add(new Separator());
                m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkFolderToOrder(folder)));
                if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await UnlinkFolder(folder)));
                m.Items.Add(new Separator());
                var del = MI("Eliminar", async (_, _) => await DeleteFolder(folder)); del.Foreground = Destructive; m.Items.Add(del);
                card.ContextMenu = m;
            };
            return card;
        }

        // ===============================================
        // FILE CARD
        // ===============================================
        private UIElement MakeFileCard(DriveFileDb file)
        {
            var (cH, bH) = GetFileCfg(file.FileName);
            var fC = ColorHex(cH); var fB = new SolidColorBrush(fC); var bgB = BrushHex(bH);
            var sel = _selectedFile?.Id == file.Id;

            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = sel ? Primary : BorderColor, BorderThickness = new Thickness(2), Margin = new Thickness(8), Cursor = Cursors.Hand, ClipToBounds = true };
            if (sel) card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 12, ShadowDepth = 2, Opacity = 0.15 };

            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Preview
            var prev = new Border { Background = bgB, CornerRadius = new CornerRadius(10, 10, 0, 0) };
            var pg = new Grid();
            pg.Children.Add(new TextBlock { Text = FileIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 48, Foreground = fB, Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var ext = System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            var badge = new Border { Background = fB, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0) };
            badge.Child = new TextBlock { Text = ext, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            pg.Children.Add(badge);
            prev.Child = pg; Grid.SetRow(prev, 0); mg.Children.Add(prev);

            // Info
            var ip = new StackPanel { Margin = new Thickness(16) };
            var nameT = new TextBlock { Text = file.FileName, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8), ToolTip = file.FileName };
            ip.Children.Add(nameT);
            var sg = new Grid();
            sg.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(file.FileSize), FontSize = 11, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Left });
            sg.Children.Add(new TextBlock { Text = RelTime(file.UploadedAt), FontSize = 11, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right });
            ip.Children.Add(sg);
            Grid.SetRow(ip, 1); mg.Children.Add(ip);
            card.Child = mg;

            card.MouseEnter += (s, e) => { if (_selectedFile?.Id != file.Id) { card.BorderBrush = SlateLight; card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 4, Opacity = 0.08 }; } nameT.Foreground = Primary; };
            card.MouseLeave += (s, e) => { if (_selectedFile?.Id != file.Id) { card.BorderBrush = BorderColor; card.Effect = null; } nameT.Foreground = TextPrimary; };
            card.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = DownloadFile(file); else { _selectedFile = file; ShowDetail(file); RenderContent(); } };
            card.MouseRightButtonDown += (s, e) =>
            {
                var m = new ContextMenu();
                m.Items.Add(MI("Descargar", async (_, _) => await DownloadFile(file)));
                m.Items.Add(MI("Renombrar", (_, _) => RenameFile(file)));
                m.Items.Add(new Separator());
                var del = MI("Eliminar", async (_, _) => await DeleteFile(file)); del.Foreground = Destructive; m.Items.Add(del);
                card.ContextMenu = m;
            };
            return card;
        }

        // ===============================================
        // DETAIL PANEL
        // ===============================================
        private async void ShowDetail(DriveFileDb file)
        {
            var (cH, _) = GetFileCfg(file.FileName);
            var fC = ColorHex(cH);
            DetailFileName.Text = file.FileName;
            var ext = System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            DetailFileExt.Text = $"Archivo {ext}";
            DetailPreviewIcon.Text = FileIcon(file.FileName);
            DetailPreviewBg.Background = new LinearGradientBrush(Color.FromArgb(20, fC.R, fC.G, fC.B), Color.FromArgb(40, fC.R, fC.G, fC.B), 45);
            DetailPreviewIcon.Foreground = new SolidColorBrush(fC);

            var uploader = await ResolveUser(file.UploadedBy);
            var loc = _breadcrumb.Count > 0 ? string.Join(" / ", _breadcrumb.Select(b => b.Name)) : "IMA Drive";

            DetailInfoPanel.Children.Clear();
            foreach (var (ico, lbl, val) in new[] {
                ("\uE8A5", "Tipo", FriendlyType(file.FileName)),
                ("\uEDA2", "Tamano", Services.Drive.DriveService.FormatFileSize(file.FileSize)),
                ("\uE787", "Fecha de subida", file.UploadedAt?.ToString("dd 'de' MMMM, yyyy") ?? "Sin fecha"),
                ("\uE77B", "Subido por", uploader),
                ("\uED41", "Ubicacion", loc) })
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                var ib = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(8), Background = Background, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                ib.Child = new TextBlock { Text = ico, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(ib);
                var tp = new StackPanel { Margin = new Thickness(44, 0, 0, 0) };
                tp.Children.Add(new TextBlock { Text = lbl, FontSize = 11, Foreground = TextLight, Margin = new Thickness(0, 0, 0, 2) });
                tp.Children.Add(new TextBlock { Text = val, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = TextPrimary, TextWrapping = TextWrapping.Wrap });
                g.Children.Add(tp);
                DetailInfoPanel.Children.Add(g);
            }
            DetailPanel.Visibility = Visibility.Visible;
        }

        private void HideDetail() { _selectedFile = null; DetailPanel.Visibility = Visibility.Collapsed; }

        // ===============================================
        // VIEW TOGGLE
        // ===============================================
        private void UpdateViewToggle()
        {
            GridViewBtn.Background = _viewMode == "grid" ? Brushes.White : Brushes.Transparent;
            if (GridViewBtn.Content is TextBlock gi) gi.Foreground = _viewMode == "grid" ? Primary : TextMuted;
            GridViewBtn.Effect = _viewMode == "grid" ? new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.1 } : null;
            ListViewBtn.Background = _viewMode == "list" ? Brushes.White : Brushes.Transparent;
            if (ListViewBtn.Content is TextBlock li) li.Foreground = _viewMode == "list" ? Primary : TextMuted;
            ListViewBtn.Effect = _viewMode == "list" ? new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.1 } : null;
        }

        // ===============================================
        // CRUD
        // ===============================================
        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var n = Prompt("Nueva Carpeta", "Nombre de la carpeta:", "Nueva carpeta"); if (string.IsNullOrWhiteSpace(n)) return;
            await SafeLoad(async () => { var c = await SupabaseService.Instance.CreateDriveFolder(n, _currentFolderId, _currentUser.Id, _cts.Token); if (c != null) { InvalidateStatsCache(); await LoadCurrentFolder(); } else MessageBox.Show("No se pudo crear. Nombre duplicado?", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); });
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue) { MessageBox.Show("Abra una carpeta primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (!SupabaseService.Instance.IsDriveStorageConfigured) { MessageBox.Show("R2 no configurado.", "Storage", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var dlg = new OpenFileDialog { Title = "Seleccionar archivos", Multiselect = true, Filter = "Todos (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            UploadPanel.Visibility = Visibility.Visible; UploadItemsPanel.Children.Clear();
            var tr = new Dictionary<string, (TextBlock st, ProgressBar pb)>();
            foreach (var fp in dlg.FileNames)
            {
                var fn = System.IO.Path.GetFileName(fp);
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                var nb = new TextBlock { Text = fn, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(nb, 0); row.Children.Add(nb);
                var sb = new TextBlock { Text = "Pendiente", FontSize = 10, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(sb, 1); row.Children.Add(sb);
                UploadItemsPanel.Children.Add(row);
                var pb = new ProgressBar { Height = 3, Value = 0, Margin = new Thickness(0, 0, 0, 4), Foreground = Primary }; UploadItemsPanel.Children.Add(pb);
                tr[fp] = (sb, pb);
            }
            int ok = 0, fail = 0;
            foreach (var fp in dlg.FileNames)
            {
                var (s, b) = tr[fp];
                try { s.Text = "Subiendo..."; s.Foreground = Primary; b.IsIndeterminate = true; await SupabaseService.Instance.UploadDriveFile(fp, _currentFolderId.Value, _currentUser.Id, _cts.Token); b.IsIndeterminate = false; b.Value = 100; b.Foreground = GreenOk; s.Text = "Listo"; s.Foreground = GreenOk; ok++; }
                catch (Exception ex) { Debug.WriteLine($"Upload err: {ex.Message}"); b.IsIndeterminate = false; b.Value = 100; b.Foreground = Destructive; s.Text = "Error"; s.Foreground = Destructive; fail++; }
            }
            StatusText.Text = fail > 0 ? $"{ok} subido(s), {fail} fallido(s)" : $"{ok} archivo(s) subido(s)";
            InvalidateStatsCache(); await SafeLoad(() => LoadCurrentFolder());
            _ = Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() => UploadPanel.Visibility = Visibility.Collapsed));
        }

        private async Task DownloadFile(DriveFileDb file)
        {
            if (!SupabaseService.Instance.IsDriveStorageConfigured) { MessageBox.Show("R2 no configurado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var d = new SaveFileDialog { Title = "Guardar", FileName = file.FileName, Filter = "Todos (*.*)|*.*" }; if (d.ShowDialog() != true) return;
            await SafeLoad(async () => { StatusText.Text = $"Descargando {file.FileName}..."; var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, d.FileName, _cts.Token); StatusText.Text = ok ? $"{file.FileName} descargado" : "Error al descargar"; });
        }

        private void RenameFolder(DriveFolderDb f) { var n = Prompt("Renombrar", "Nuevo nombre:", f.Name); if (string.IsNullOrWhiteSpace(n) || n == f.Name) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFolder(f.Id, n, _cts.Token)) { InvalidateStatsCache(); await LoadCurrentFolder(); } }); }
        private void RenameFile(DriveFileDb f) { var n = Prompt("Renombrar", "Nuevo nombre:", f.FileName); if (string.IsNullOrWhiteSpace(n) || n == f.FileName) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFile(f.Id, n, _cts.Token)) await LoadCurrentFolder(); }); }

        private async Task DeleteFolder(DriveFolderDb f)
        {
            if (MessageBox.Show($"Eliminar \"{f.Name}\" y TODO su contenido?\n\nNo se puede deshacer.", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await SafeLoad(async () => { if (await SupabaseService.Instance.DeleteDriveFolder(f.Id, _cts.Token)) { _statsCache.Remove(f.Id); InvalidateStatsCache(); await LoadCurrentFolder(); } });
        }

        private async Task DeleteFile(DriveFileDb f)
        {
            if (MessageBox.Show($"Eliminar \"{f.FileName}\"?\n\nNo se puede deshacer.", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await SafeLoad(async () => { if (await SupabaseService.Instance.DeleteDriveFile(f.Id, _cts.Token)) { if (_selectedFile?.Id == f.Id) HideDetail(); InvalidateStatsCache(); await LoadCurrentFolder(); } });
        }

        // ===============================================
        // ORDER LINKING
        // ===============================================
        private async void LinkFolderToOrder(DriveFolderDb folder)
        {
            var roots = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token);
            var rootId = roots.FirstOrDefault()?.Id;
            if (rootId == null || folder.ParentId != rootId) { MessageBox.Show("Solo carpetas de primer nivel.", "No permitido", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var orders = await SupabaseService.Instance.GetOrders(200);
            if (orders == null || orders.Count == 0) { MessageBox.Show("Sin ordenes.", "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var allL = await SupabaseService.Instance.GetDriveChildFolders(rootId, _cts.Token);
            var used = allL.Where(f => f.LinkedOrderId.HasValue && f.Id != folder.Id).Select(f => f.LinkedOrderId!.Value).ToHashSet();
            var avail = orders.Where(o => !used.Contains(o.Id)).ToList();
            if (avail.Count == 0) { MessageBox.Show("Todas las ordenes ya vinculadas.", "Sin disponibles", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var clients = await SupabaseService.Instance.GetClients();
            var cN = clients?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();
            var sel = OrderDialog(avail, cN); if (sel == null) return;
            await SafeLoad(async () => { if (await SupabaseService.Instance.LinkDriveFolderToOrder(folder.Id, sel.Id, _cts.Token)) { _orderInfoCache.Remove(sel.Id); StatusText.Text = $"Vinculada a {sel.Po ?? $"#{sel.Id}"}"; await LoadCurrentFolder(); } });
        }

        private async Task UnlinkFolder(DriveFolderDb f) { await SafeLoad(async () => { if (await SupabaseService.Instance.UnlinkDriveFolder(f.Id, _cts.Token)) { StatusText.Text = "Desvinculada"; await LoadCurrentFolder(); } }); }

        private async void LinkThisFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue || !_selectionOrderId.HasValue) return;
            await SafeLoad(async () => { if (await SupabaseService.Instance.LinkDriveFolderToOrder(_currentFolderId.Value, _selectionOrderId.Value, _cts.Token)) { MessageBox.Show($"Vinculada a {_selectionOrderPo}", "OK", MessageBoxButton.OK, MessageBoxImage.Information); DialogResult = true; Close(); } });
        }

        private void CancelSelection_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private static OrderDb? OrderDialog(List<OrderDb> orders, Dictionary<int, string> cNames)
        {
            var w = new Window { Title = "Vincular a Orden", Width = 520, Height = 240, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow };
            var p = new StackPanel { Margin = new Thickness(15) };
            p.Children.Add(new TextBlock { Text = "Seleccione la orden a vincular:", Margin = new Thickness(0, 0, 0, 8), FontSize = 14 });
            var sb = new TextBox { FontSize = 13, Margin = new Thickness(0, 0, 0, 8), Text = "Buscar por OC, cliente o detalle...", Foreground = Brushes.Gray };
            sb.GotFocus += (s, e) => { if (sb.Foreground == Brushes.Gray) { sb.Text = ""; sb.Foreground = Brushes.Black; } };
            sb.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(sb.Text)) { sb.Text = "Buscar por OC, cliente o detalle..."; sb.Foreground = Brushes.Gray; } };
            p.Children.Add(sb);
            var cb = new ComboBox { FontSize = 12, Height = 30, IsEditable = false, DisplayMemberPath = "DisplayText" };
            var items = orders.Select(o => { var c = o.ClientId.HasValue && cNames.ContainsKey(o.ClientId.Value) ? cNames[o.ClientId.Value] : ""; var d = Trunc(o.Description, 30); var t = $"{o.Po ?? "Sin OC"} | {Trunc(c, 18)}"; if (!string.IsNullOrEmpty(d)) t += $" | {d}"; return new { Order = o, DisplayText = t }; }).ToList();
            cb.ItemsSource = items; if (items.Count > 0) cb.SelectedIndex = 0;
            sb.TextChanged += (s, e) => { if (sb.Foreground == Brushes.Gray) return; var f = sb.Text?.Trim().ToLowerInvariant() ?? ""; var fl = items.Where(i => i.DisplayText.ToLowerInvariant().Contains(f)).ToList(); cb.ItemsSource = fl; if (fl.Count > 0) cb.SelectedIndex = 0; };
            p.Children.Add(cb);
            OrderDb? res = null;
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var ok = new Button { Content = "Vincular", Width = 90, Padding = new Thickness(0, 6, 0, 6), IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold };
            ok.Click += (s, e) => { var sel = cb.SelectedItem; if (sel != null) { res = ((dynamic)sel).Order; w.Close(); } };
            bp.Children.Add(ok); bp.Children.Add(new Button { Content = "Cancelar", Width = 80, Padding = new Thickness(0, 6, 0, 6), Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
            p.Children.Add(bp); w.Content = p; w.ShowDialog(); return res;
        }

        // ===============================================
        // EVENT HANDLERS
        // ===============================================
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        private void GridView_Click(object sender, RoutedEventArgs e) { _viewMode = "grid"; UpdateViewToggle(); RenderContent(); }
        private void ListView_Click(object sender, RoutedEventArgs e) { _viewMode = "list"; UpdateViewToggle(); RenderContent(); }
        private async void BackToFolders_Click(object sender, RoutedEventArgs e) { if (_breadcrumb.Count >= 2) await SafeLoad(() => NavigateToFolder(_breadcrumb[^2].Id)); else await SafeLoad(() => NavigateToRoot()); }
        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
        private void DetailClose_Click(object sender, RoutedEventArgs e) { HideDetail(); RenderContent(); }
        private async void DetailDownload_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) await DownloadFile(_selectedFile); }
        private void DetailLink_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) { Clipboard.SetText(_selectedFile.StoragePath ?? _selectedFile.FileName); StatusText.Text = "Ruta copiada"; } }
        private async void DetailDelete_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) await DeleteFile(_selectedFile); }

        private void Window_DragEnter(object sender, DragEventArgs e) { if (_currentFolderId.HasValue && e.Data.GetDataPresent(DataFormats.FileDrop)) DragDropOverlay.Visibility = Visibility.Visible; }
        private void Window_DragLeave(object sender, DragEventArgs e) => DragDropOverlay.Visibility = Visibility.Collapsed;
        private void Window_DragOver(object sender, DragEventArgs e) => e.Handled = true;
        private async void Window_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed; e.Handled = true;
            if (!_currentFolderId.HasValue || !e.Data.GetDataPresent(DataFormats.FileDrop) || !SupabaseService.Instance.IsDriveStorageConfigured) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!; if (files.Length == 0) return;
            int ok = 0, fail = 0; StatusText.Text = $"Subiendo {files.Length} archivo(s)...";
            foreach (var fp in files) { try { await SupabaseService.Instance.UploadDriveFile(fp, _currentFolderId.Value, _currentUser.Id, _cts.Token); ok++; } catch { fail++; } }
            StatusText.Text = fail > 0 ? $"{ok} subido(s), {fail} fallido(s)" : $"{ok} subido(s)";
            InvalidateStatsCache(); await SafeLoad(() => LoadCurrentFolder());
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { if (_selectedFile != null) { HideDetail(); RenderContent(); } else if (_breadcrumb.Count > 1) BackToFolders_Click(this, new RoutedEventArgs()); else Close(); }
            base.OnKeyDown(e);
        }
        private async void OnMouseNav(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1) { e.Handled = true; await SafeLoad(() => NavBack()); }
            else if (e.ChangedButton == MouseButton.XButton2) { e.Handled = true; await SafeLoad(() => NavFwd()); }
        }

        // ===============================================
        // HELPERS
        // ===============================================
        private async Task SafeLoad(Func<Task> a) { try { await a(); } catch (OperationCanceledException) { } catch (Exception ex) { Debug.WriteLine($"[DriveV2] ERR: {ex.Message}"); StatusText.Text = $"Error: {ex.Message}"; } }

        private async Task<string> ResolveUser(int? uid)
        {
            if (!uid.HasValue) return "-";
            if (_userNameCache.TryGetValue(uid.Value, out var c)) return c;
            try { var u = await SupabaseService.Instance.GetUserById(uid.Value); var n = u?.FullName ?? u?.Username ?? $"#{uid}"; _userNameCache[uid.Value] = n; return n; }
            catch { return $"#{uid}"; }
        }

        private string GetOrderDisplayText(int? orderId)
        {
            if (!orderId.HasValue) return "-";
            if (!_orderInfoCache.TryGetValue(orderId.Value, out var oi)) return $"#{orderId}";
            return !string.IsNullOrEmpty(oi.Client) ? $"{oi.Po} | {Trunc(oi.Client, 16)}" : oi.Po;
        }

        private string? GetOrderTooltip(int? orderId)
        {
            if (!orderId.HasValue || !_orderInfoCache.TryGetValue(orderId.Value, out var oi)) return null;
            var lines = new List<string> { $"Orden: {oi.Po}" };
            if (!string.IsNullOrEmpty(oi.Client)) lines.Add($"Cliente: {oi.Client}");
            if (!string.IsNullOrEmpty(oi.Detail)) lines.Add($"Detalle: {oi.Detail}");
            return string.Join("\n", lines);
        }

        private static UIElement MakeFolderIcon(double sz, Brush fill) => new System.Windows.Shapes.Path { Data = FolderGeometry, Fill = fill, Stretch = Stretch.Uniform, Width = sz, Height = sz, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        private static string FileIcon(string fn) { var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant(); return e switch { ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\uEB9F", ".mp4" or ".avi" or ".mkv" => "\uE714", ".zip" or ".rar" => "\uE8C8", _ => "\uE8A5" }; }

        private static string FriendlyType(string fn) { var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant(); return e switch { ".pdf" => "Documento PDF", ".doc" or ".docx" => "Documento Word", ".xls" or ".xlsx" => "Hoja de calculo Excel", ".ppt" or ".pptx" => "Presentacion PowerPoint", ".jpg" or ".jpeg" => "Imagen JPEG", ".png" => "Imagen PNG", ".gif" => "Imagen GIF", ".bmp" => "Imagen BMP", ".webp" => "Imagen WebP", ".mp4" => "Video MP4", ".zip" => "Archivo ZIP", ".rar" => "Archivo RAR", ".txt" => "Archivo de texto", ".csv" => "Archivo CSV", ".log" => "Archivo de registro", ".dwg" => "Plano AutoCAD", ".dxf" => "Plano DXF", ".step" or ".stp" => "Modelo 3D STEP", _ => $"Archivo {e?.TrimStart('.').ToUpperInvariant()}" }; }

        private static string RelTime(DateTime? d) { if (!d.HasValue) return "sin fecha"; var df = DateTime.Now - d.Value; if (df.TotalSeconds < 60) return "hace un momento"; if (df.TotalMinutes < 60) return $"hace {(int)df.TotalMinutes} min"; if (df.TotalHours < 24) return $"hace {(int)df.TotalHours} hora{((int)df.TotalHours != 1 ? "s" : "")}"; if (df.TotalDays < 2) return "ayer"; if (df.TotalDays < 7) return $"hace {(int)df.TotalDays} dias"; if (df.TotalDays < 30) return $"hace {(int)(df.TotalDays / 7)} semana{((int)(df.TotalDays / 7) != 1 ? "s" : "")}"; return d.Value.ToString("dd/MM/yyyy"); }

        private static string Prompt(string title, string label, string def)
        {
            var w = new Window { Title = title, Width = 400, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow };
            var p = new StackPanel { Margin = new Thickness(15) }; p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 8) });
            var tb = new TextBox { Text = def, FontSize = 14 }; tb.SelectAll(); p.Children.Add(tb);
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            string? res = null;
            var ok = new Button { Content = "Aceptar", Width = 80, Padding = new Thickness(0, 5, 0, 5), IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            ok.Click += (s, e) => { res = tb.Text; w.Close(); };
            bp.Children.Add(ok); bp.Children.Add(new Button { Content = "Cancelar", Width = 80, Padding = new Thickness(0, 5, 0, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true });
            p.Children.Add(bp); w.Content = p; w.Loaded += (s, e) => tb.Focus(); w.ShowDialog(); return res ?? "";
        }

        private static string Trunc(string? v, int m) => string.IsNullOrEmpty(v) ? "" : v.Length <= m ? v : v[..m] + "...";
        private static MenuItem MI(string h, RoutedEventHandler hnd) { var m = new MenuItem { Header = h }; m.Click += hnd; return m; }
        private static Color ColorHex(string h) { h = h.TrimStart('#'); return Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16)); }
        private static SolidColorBrush BrushHex(string h) => new(ColorHex(h));
    }

    static class DriveV2Ext { public static T Also<T>(this T o, Action<T> a) { a(o); return o; } }
}
