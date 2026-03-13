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
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SistemaGestionProyectos2.Views
{
    public partial class DriveV2Window : Window
    {
        private readonly UserSession _currentUser;
        private CancellationTokenSource _cts = new();

        private int? _currentFolderId;
        private List<DriveFolderDb> _breadcrumb = new();
        private List<DriveFolderDb> _currentFolders = new();
        private List<DriveFileDb> _currentFiles = new();
        private readonly Stack<int> _backHistory = new();
        private readonly Stack<int> _forwardHistory = new();

        private bool _isSelectionMode;
        private int? _selectionOrderId;
        private string _selectionOrderPo = "";

        private string _activeNav = "all";
        private static string _persistedViewMode = "grid"; // MEJORA-9: persists across folder navigation within session
        private string _viewMode = _persistedViewMode;
        private string? _activeFilter = null;
        private string _sortField = "name"; // MEJORA-7: name, type, size, date
        private bool _sortAsc = true;
        private long _globalStorageBytes = -1;
        private DriveFileDb? _selectedFile = null;
        private readonly HashSet<int> _selectedFileIds = new(); // Multi-select
        private readonly List<Border> _navItems = new();
        private readonly List<Border> _filterItems = new(); // BUG-3: filter sidebar items

        // Caches
        private readonly Dictionary<int, string> _userNameCache = new();
        private readonly Dictionary<int, (int fileCount, int subCount, long totalSize)> _statsCache = new();
        private readonly Dictionary<int, (string Po, string Client, string Detail)> _orderInfoCache = new();

        // Benchmark
        private bool _benchmarkActive;
        private BenchmarkPhaseResult? _lastPhaseResult;

        // Navigation cache (stale-while-revalidate)
        private readonly Dictionary<int, FolderSnapshot> _folderCache = new();
        record FolderSnapshot(List<DriveFolderDb> Folders, List<DriveFileDb> Files,
            List<DriveFolderDb> Breadcrumb, DateTime CachedAt);

        // Theme
        private static readonly SolidColorBrush Primary = Fr(0x1D, 0x4E, 0xD8);
        private static readonly SolidColorBrush BorderColor = Fr(0xE2, 0xE8, 0xF0);
        private static readonly SolidColorBrush BorderLight = Fr(0xF1, 0xF5, 0xF9);
        private static readonly SolidColorBrush TextPrimary = Fr(0x0F, 0x17, 0x2A);
        private static readonly SolidColorBrush TextSecondary = Fr(0x47, 0x55, 0x69);
        private static readonly SolidColorBrush TextMuted = Fr(0x64, 0x74, 0x8B);
        private static readonly SolidColorBrush TextLight = Fr(0x94, 0xA3, 0xB8);
        private static readonly SolidColorBrush SlateLight = Fr(0xCB, 0xD5, 0xE1);
        private static readonly SolidColorBrush HoverBg = Fr(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush ActiveBg = Fr(0xEF, 0xF6, 0xFF);
        private static readonly SolidColorBrush Background = Fr(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush Destructive = Fr(0xDC, 0x26, 0x26);
        private static readonly SolidColorBrush GreenOk = Fr(0x43, 0xA0, 0x47);
        static SolidColorBrush Fr(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

        private static readonly string[] FolderColors = { "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#06B6D4" };
        private static readonly Geometry FolderGeo = Geometry.Parse("M10,4 H4 C2.9,4 2,4.9 2,6 V18 C2,19.1 2.9,20 4,20 H20 C21.1,20 22,19.1 22,18 V8 C22,6.9 21.1,6 20,6 H12 L10,4 Z");

        private static readonly Dictionary<string, (string c, string bg)> FileCfg = new()
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
        static (string c, string bg) GFC(string fn) { var e = System.IO.Path.GetExtension(fn)?.TrimStart('.').ToLowerInvariant() ?? ""; return FileCfg.TryGetValue(e, out var v) ? v : ("#64748B", "#F8FAFC"); }

        // P6: Safe Segoe MDL2 icons
        static string FIcon(string fn) { var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant(); return e switch { ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\uE91B", ".mp4" or ".avi" or ".mkv" or ".mov" => "\uE714", ".zip" or ".rar" or ".7z" => "\uE8C8", ".pdf" => "\uEA90", ".xls" or ".xlsx" or ".csv" => "\uE80B", ".dwg" or ".dxf" or ".step" or ".stp" => "\uE8FD", _ => "\uE8A5" }; }
        static string FType(string fn) { var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant(); return e switch { ".pdf" => "Documento PDF", ".doc" or ".docx" => "Documento Word", ".xls" or ".xlsx" => "Hoja de calculo Excel", ".ppt" or ".pptx" => "Presentacion PowerPoint", ".jpg" or ".jpeg" => "Imagen JPEG", ".png" => "Imagen PNG", ".gif" => "Imagen GIF", ".mp4" => "Video MP4", ".zip" => "Archivo ZIP", ".rar" => "Archivo RAR", ".txt" => "Archivo de texto", ".csv" => "Archivo CSV", ".log" => "Archivo de registro", ".dwg" => "Plano AutoCAD", ".dxf" => "Plano DXF", ".step" or ".stp" => "Modelo 3D STEP", _ => $"Archivo {e?.TrimStart('.').ToUpperInvariant()}" }; }
        static string RelT(DateTime? d) { if (!d.HasValue) return "sin fecha"; var df = DateTime.Now - d.Value; if (df.TotalSeconds < 60) return "hace un momento"; if (df.TotalMinutes < 60) return $"hace {(int)df.TotalMinutes} min"; if (df.TotalHours < 24) return $"hace {(int)df.TotalHours} hora{((int)df.TotalHours != 1 ? "s" : "")}"; if (df.TotalDays < 2) return "ayer"; if (df.TotalDays < 7) return $"hace {(int)df.TotalDays} dias"; if (df.TotalDays < 30) return $"hace {(int)(df.TotalDays / 7)} semana{((int)(df.TotalDays / 7) != 1 ? "s" : "")}"; return d.Value.ToString("dd/MM/yyyy"); }

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
            Loaded += async (s, e) => { InitSidebar(); UpdateViewToggle(); _ = LoadGlobalStorage(); await SafeLoad(() => _navigateToFolderId.HasValue ? NavTo(_navigateToFolderId.Value, hist: false) : NavigateToRoot()); };
        }

        /// <summary>Open Drive navigating directly to a specific folder</summary>
        public DriveV2Window(UserSession user, int folderId) : this(user)
        {
            _navigateToFolderId = folderId;
        }
        private int? _navigateToFolderId;

        /// <summary>Open Drive in selection mode for linking a folder to an order</summary>
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
        private void InitSidebar()
        {
            foreach (var (id, ico, lbl) in new[] { ("all", "\uE80F", "Todos los archivos"), ("recent", "\uE823", "Recientes"), ("starred", "\uE734", "Destacados"), ("trash", "\uE74D", "Papelera") })
            { var it = MkNav(id, ico, lbl); NavPanel.Children.Add(it); _navItems.Add(it); }
            SetNav("all");
            foreach (var (id, clr, lbl) in new[] { ("pdf", "#EF4444", "PDFs"), ("img", "#10B981", "Imagenes"), ("cad", "#8B5CF6", "Archivos CAD"), ("xls", "#10B981", "Hojas de calculo"), ("vid", "#EC4899", "Videos") })
            { var fi = MkFilter(id, clr, lbl, "0"); _filterItems.Add(fi); FilterPanel.Children.Add(fi); }

            // Temporal: boton Purgar R2 para limpieza durante desarrollo
            var purgeBtn = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 24, 0, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)) };
            var psp = new StackPanel { Orientation = Orientation.Horizontal };
            psp.Children.Add(new TextBlock { Text = "\uE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = Destructive, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            psp.Children.Add(new TextBlock { Text = "Purgar R2 (temp)", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Destructive, VerticalAlignment = VerticalAlignment.Center });
            purgeBtn.Child = psp;
            purgeBtn.MouseEnter += (s, e) => purgeBtn.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
            purgeBtn.MouseLeave += (s, e) => purgeBtn.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2));
            purgeBtn.MouseLeftButtonDown += async (s, e) =>
            {
                if (!Confirm("ATENCION: Esto eliminara TODOS los archivos del bucket R2.\nLos registros en BD NO se eliminan.\n\nContinuar?")) return;
                await SafeLoad(async () =>
                {
                    StatusText.Text = "Purgando R2...";
                    var count = await SupabaseService.Instance.PurgeDriveR2Files();
                    StatusText.Text = count >= 0 ? $"R2 purgado: {count} archivos eliminados" : "Error purgando R2";
                    if (count >= 0) { _statsCache.Clear(); await LoadFolder(); }
                });
            };
            NavPanel.Children.Add(purgeBtn);

            // Temporal: boton Benchmark para medir rendimiento
            var benchBtn = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 4, 0, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF)) };
            var bsp = new StackPanel { Orientation = Orientation.Horizontal };
            bsp.Children.Add(new TextBlock { Text = "\uE9D2", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = Primary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            bsp.Children.Add(new TextBlock { Text = "Benchmark (temp)", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Primary, VerticalAlignment = VerticalAlignment.Center });
            benchBtn.Child = bsp;
            benchBtn.MouseEnter += (s, e) => benchBtn.Background = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE));
            benchBtn.MouseLeave += (s, e) => benchBtn.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF));
            benchBtn.MouseLeftButtonDown += RunBenchmark_Click;
            NavPanel.Children.Add(benchBtn);
        }

        Border MkNav(string id, string ico, string lbl)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = id };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = ico, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = TextSecondary });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Foreground = TextSecondary });
            b.Child = sp;
            b.MouseLeftButtonDown += async (s, e) => { if (id == "all") { SetNav(id); ClearMultiSelect(); _activeFilter = null; UpdateFilterHighlight(); HideDetail(); await SafeLoad(() => NavigateToRoot()); } };
            b.MouseEnter += (s, e) => { if (b.Tag as string != _activeNav) b.Background = HoverBg; };
            b.MouseLeave += (s, e) => { if (b.Tag as string != _activeNav) b.Background = Brushes.Transparent; };
            return b;
        }

        void SetNav(string id) { _activeNav = id; foreach (var it in _navItems) { var a = it.Tag as string == id; it.Background = a ? ActiveBg : Brushes.Transparent; if (it.Child is StackPanel sp) foreach (var c in sp.Children.OfType<TextBlock>()) c.Foreground = a ? Primary : TextSecondary; } }

        Border MkFilter(string filterId, string clr, string lbl, string cnt)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = filterId };
            var g = new Grid(); var sp = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            sp.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = BH(clr), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(sp);
            var cntTb = new TextBlock { Text = cnt, FontSize = 12, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            g.Children.Add(cntTb);
            g.IsHitTestVisible = false; // Ensure clicks pass through to Border
            b.Child = g;
            // BUG-3: Click handler for filtering
            b.MouseLeftButtonDown += (s, e) =>
            {
                Debug.WriteLine($"[DriveV2] Filter click: {filterId}, currentFiles={_currentFiles.Count}, activeFilter={_activeFilter}");
                if (_activeFilter == filterId) _activeFilter = null; // toggle off
                else _activeFilter = filterId;
                UpdateFilterHighlight();
                RenderContent();
                var tot = _activeFilter != null ? ApplyFileFilter(_currentFiles, _activeFilter).Count : _currentFiles.Count;
                StatusText.Text = _activeFilter != null ? $"Filtro: {lbl} ({tot} archivo(s))" : $"{_currentFolders.Count + _currentFiles.Count} elemento(s)";
                Debug.WriteLine($"[DriveV2] Filter result: filter={_activeFilter}, matched={tot}");
            };
            b.MouseEnter += (s, e) => { if (b.Tag as string != _activeFilter) b.Background = HoverBg; };
            b.MouseLeave += (s, e) => { if (b.Tag as string != _activeFilter) b.Background = Brushes.Transparent; };
            return b;
        }

        // BUG-3: Highlight active filter
        void UpdateFilterHighlight()
        {
            foreach (var fi in _filterItems)
            {
                var active = fi.Tag as string == _activeFilter;
                fi.Background = active ? ActiveBg : Brushes.Transparent;
                if (fi.Child is Grid g)
                    foreach (var sp in g.Children.OfType<StackPanel>())
                        foreach (var tb in sp.Children.OfType<TextBlock>())
                            tb.Foreground = active ? Primary : TextSecondary;
            }
        }

        // BUG-3: File filter logic
        static readonly Dictionary<string, string[]> FilterExtensions = new()
        {
            ["pdf"] = new[] { ".pdf" },
            ["img"] = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" },
            ["cad"] = new[] { ".dwg", ".dxf", ".step", ".stp", ".igs", ".sldprt", ".sldasm", ".ipt", ".iam", ".mcx-9" },
            ["xls"] = new[] { ".xls", ".xlsx", ".csv" },
            ["vid"] = new[] { ".mp4", ".avi", ".mkv", ".mov" },
        };

        static List<DriveFileDb> ApplyFileFilter(List<DriveFileDb> files, string filterId)
        {
            if (!FilterExtensions.TryGetValue(filterId, out var exts)) return files;
            return files.Where(f => exts.Any(x => f.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        // P12: Update filter counts + disable when no files in current folder
        void UpdateFilterCounts()
        {
            var filterKeys = new[] { "pdf", "img", "cad", "xls", "vid" };
            var hasFiles = _currentFiles.Count > 0;
            Debug.WriteLine($"[DriveV2] UpdateFilterCounts: {_currentFiles.Count} files, hasFiles={hasFiles}");
            if (hasFiles)
            {
                var exts = _currentFiles.Select(f => System.IO.Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "?").GroupBy(e => e).Select(g => $"{g.Key}({g.Count()})");
                Debug.WriteLine($"[DriveV2]   Extensions: {string.Join(", ", exts)}");
            }
            for (int i = 0; i < _filterItems.Count && i < filterKeys.Length; i++)
            {
                var fi = _filterItems[i];
                var count = hasFiles ? ApplyFileFilter(_currentFiles, filterKeys[i]).Count : 0;
                // Update count text
                if (fi.Child is Grid g)
                {
                    var ct = g.Children.OfType<TextBlock>().FirstOrDefault(t => t.HorizontalAlignment == HorizontalAlignment.Right);
                    if (ct != null) ct.Text = count.ToString();
                }
                // Disable/enable filter visually
                fi.Opacity = hasFiles ? 1.0 : 0.4;
                fi.Cursor = hasFiles ? Cursors.Hand : Cursors.Arrow;
                fi.IsHitTestVisible = hasFiles;
            }
            // Clear active filter if no files
            if (!hasFiles && _activeFilter != null) { _activeFilter = null; UpdateFilterHighlight(); }
        }

        // ===============================================
        // NAVIGATION
        // ===============================================
        async Task NavigateToRoot() { var r = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token); if (r.Any()) await NavTo(r.First().Id); else { _currentFolderId = null; _breadcrumb.Clear(); await LoadFolderFull(0); } }
        async Task NavTo(int fId, bool hist = true)
        {
            if (hist && _currentFolderId.HasValue && _currentFolderId.Value != fId) { _backHistory.Push(_currentFolderId.Value); _forwardHistory.Clear(); }
            _currentFolderId = fId; _selectedFile = null; _selectedFileIds.Clear(); HideDetail();
            // Clear search box on navigation (e.g. clicking a search result)
            if (!string.IsNullOrEmpty(SearchBox.Text)) { SearchBox.Text = ""; SearchPlaceholder.Visibility = Visibility.Visible; }
            // BUG-3: Reset filter on navigation
            if (_activeFilter != null) { _activeFilter = null; UpdateFilterHighlight(); }

            // Stale-while-revalidate: if we have cached data, render it INSTANTLY then refresh in background
            if (!_benchmarkActive && _folderCache.TryGetValue(fId, out var snap))
            {
                Debug.WriteLine($"[DriveV2] CACHE HIT folder={fId}, age={(DateTime.Now - snap.CachedAt).TotalSeconds:F1}s");
                _breadcrumb = snap.Breadcrumb;
                _currentFolders = snap.Folders;
                _currentFiles = snap.Files;
                RenderFolderUI();
                // Revalidate silently in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bcT = SupabaseService.Instance.GetDriveBreadcrumb(fId, _cts.Token);
                        var fT = SupabaseService.Instance.GetDriveChildFolders(fId, _cts.Token);
                        var fiT = SupabaseService.Instance.GetDriveFilesByFolder(fId, _cts.Token);
                        var stT = SupabaseService.Instance.GetDriveFolderStats(fId, _cts.Token);
                        await Task.WhenAll(bcT, fT, fiT, stT);
                        foreach (var kv in stT.Result) _statsCache[kv.Key] = kv.Value;
                        var changed = fT.Result.Count != snap.Folders.Count || fiT.Result.Count != snap.Files.Count;
                        _folderCache[fId] = new FolderSnapshot(fT.Result, fiT.Result, bcT.Result, DateTime.Now);
                        if (changed && _currentFolderId == fId)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                _breadcrumb = bcT.Result; _currentFolders = fT.Result; _currentFiles = fiT.Result;
                                RenderFolderUI();
                                Debug.WriteLine($"[DriveV2] REVALIDATED folder={fId} (content changed)");
                            });
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[DriveV2] Revalidate err: {ex.Message}"); }
                });
                return;
            }

            // Cold path: fetch breadcrumb + full load with spinner
            await LoadFolderFull(fId);
        }
        async Task NavBack() { if (_backHistory.Count == 0) return; if (_currentFolderId.HasValue) _forwardHistory.Push(_currentFolderId.Value); await NavTo(_backHistory.Pop(), hist: false); }
        async Task NavFwd() { if (_forwardHistory.Count == 0) return; if (_currentFolderId.HasValue) _backHistory.Push(_currentFolderId.Value); await NavTo(_forwardHistory.Pop(), hist: false); }

        // ===============================================
        // LOAD FOLDER (P2 spinner, P5 storage, P12 filters)
        // ===============================================

        /// <summary>Full cold load: breadcrumb + folders + files + stats in parallel</summary>
        async Task LoadFolderFull(int folderId)
        {
            var sw0 = Stopwatch.StartNew();
            Debug.WriteLine($"[DriveV2] === LoadFolderFull START (id={folderId}) ===");

            LoadingPanel.Visibility = Visibility.Visible;
            ((Storyboard)FindResource("SpinnerStoryboard")).Begin();
            EmptyState.Visibility = Visibility.Collapsed;
            ContentHost.Content = null;

            try
            {
                // Phase 1: breadcrumb + folders + files + stats (ALL parallel)
                var sw = Stopwatch.StartNew();
                var bcTask = SupabaseService.Instance.GetDriveBreadcrumb(folderId, _cts.Token);
                var ft = SupabaseService.Instance.GetDriveChildFolders(folderId, _cts.Token);
                var fit = SupabaseService.Instance.GetDriveFilesByFolder(folderId, _cts.Token);
                var stTask = SupabaseService.Instance.GetDriveFolderStats(folderId, _cts.Token);
                await Task.WhenAll(bcTask, ft, fit, stTask);
                _breadcrumb = bcTask.Result; _currentFolders = ft.Result; _currentFiles = fit.Result;
                foreach (var kv in stTask.Result) _statsCache[kv.Key] = kv.Value;
                var p1Ms = sw.ElapsedMilliseconds;
                Debug.WriteLine($"[DriveV2]   P1 Breadcrumb+Folders+Files+Stats: {p1Ms}ms ({_currentFolders.Count}f, {_currentFiles.Count}fi, {stTask.Result.Count}st)");

                // Phase 2: order info (batch RPC, only uncached)
                sw.Restart();
                var lids = _currentFolders.Where(f => f.LinkedOrderId.HasValue).Select(f => f.LinkedOrderId!.Value).Distinct().ToList();
                var uncO = lids.Where(id => !_orderInfoCache.ContainsKey(id)).ToList();
                if (uncO.Count > 0)
                {
                    // Batch: 1 RPC for all orders + 1 query for clients (parallel)
                    var ordTask = SupabaseService.Instance.GetDriveOrdersByIds(uncO, _cts.Token);
                    var clTask = SupabaseService.Instance.GetClients();
                    await Task.WhenAll(ordTask, clTask);
                    var cm = clTask.Result?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();
                    foreach (var o in ordTask.Result)
                    {
                        var cn = o.F_client.HasValue && cm.ContainsKey(o.F_client.Value) ? cm[o.F_client.Value] : "";
                        _orderInfoCache[o.F_order] = (o.F_po ?? $"#{o.F_order}", cn, Tr(o.F_description, 40));
                    }
                    // Mark any missing orders
                    foreach (var oid in uncO.Where(id => !_orderInfoCache.ContainsKey(id)))
                        _orderInfoCache[oid] = ($"#{oid}", "", "");
                }
                var p2Ms = sw.ElapsedMilliseconds;
                Debug.WriteLine($"[DriveV2]   P2 Orders: {p2Ms}ms (q={uncO.Count}, cached={lids.Count - uncO.Count})");

                // Phase 3: Render
                sw.Restart();
                RenderFolderUI();
                var p3Ms = sw.ElapsedMilliseconds;
                Debug.WriteLine($"[DriveV2]   P3 Render: {p3Ms}ms");

                // Capture phase timings for benchmark
                if (_benchmarkActive)
                    _lastPhaseResult = new BenchmarkPhaseResult(p1Ms, p2Ms, p3Ms, sw0.ElapsedMilliseconds,
                        _currentFolders.Count, _currentFiles.Count, uncO.Count);

                // Save to navigation cache
                if (_currentFolderId.HasValue)
                    _folderCache[_currentFolderId.Value] = new FolderSnapshot(
                        _currentFolders, _currentFiles, _breadcrumb, DateTime.Now);

                // BUG-4: Storage UI already driven by _globalStorageBytes (loaded once at startup)
            }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] LoadFolderFull ERR: {ex.Message}\n{ex.StackTrace}"); StatusText.Text = "Error"; SectionTitle.Text = "Error"; SectionSubtitle.Text = ex.Message; }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ((Storyboard)FindResource("SpinnerStoryboard")).Stop();
                Debug.WriteLine($"[DriveV2] === LoadFolder TOTAL: {sw0.ElapsedMilliseconds}ms ===");
            }
        }

        /// <summary>Reload current folder (convenience wrapper after CRUD operations)</summary>
        async Task LoadFolder() { if (_currentFolderId.HasValue) await LoadFolderFull(_currentFolderId.Value); }

        /// <summary>Render current folder state to UI (used by both LoadFolderFull and cache-hit path)</summary>
        void RenderFolderUI()
        {
            // Clear all selection state when changing folders
            ClearMultiSelect(); HideDetail();
            RenderBreadcrumb();
            BackToFoldersBtn.Visibility = _breadcrumb.Count <= 1 ? Visibility.Collapsed : Visibility.Visible;
            SectionTitle.Text = _breadcrumb.LastOrDefault()?.Name ?? "IMA Drive";
            var tot = _currentFolders.Count + _currentFiles.Count;
            var pts = new List<string>();
            if (_currentFolders.Count > 0) pts.Add($"{_currentFolders.Count} carpeta{(_currentFolders.Count != 1 ? "s" : "")}");
            if (_currentFiles.Count > 0) pts.Add($"{_currentFiles.Count} archivo{(_currentFiles.Count != 1 ? "s" : "")}");
            SectionSubtitle.Text = pts.Count > 0 ? string.Join(" - ", pts) : "Sin contenido";
            StatusText.Text = $"{tot} elemento{(tot != 1 ? "s" : "")}";

            // MEJORA-6: Order header when current folder is linked to an order
            RenderOrderHeader();

            // MEJORA-8: Use persisted view mode (user's choice is respected)
            _viewMode = _persistedViewMode;

            RenderContent();
            EmptyState.Visibility = tot == 0 ? Visibility.Visible : Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Collapsed;
            UpdateFilterCounts();
            UpdateSearchPlaceholder();
        }

        // MEJORA-6: Render order info banner above content when folder is linked
        void RenderOrderHeader()
        {
            // Remove any existing order header
            var existing = ContentHost.Parent is StackPanel sp ? sp.Children.OfType<Border>().FirstOrDefault(b => b.Tag as string == "orderHeader") : null;
            if (existing != null && ContentHost.Parent is StackPanel sp2) sp2.Children.Remove(existing);

            var currentFolder = _breadcrumb.LastOrDefault();
            if (currentFolder?.LinkedOrderId == null) return;
            if (!_orderInfoCache.TryGetValue(currentFolder.LinkedOrderId.Value, out var oi)) return;

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF)),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 14, 20, 14),
                Margin = new Thickness(0, 0, 0, 16),
                Tag = "orderHeader"
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Icon
            var icBdr = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)), Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            icBdr.Child = new TextBlock { Text = "\uE8A7", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 18, Foreground = Primary, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(icBdr, 0); g.Children.Add(icBdr);

            // Info
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var topRow = new StackPanel { Orientation = Orientation.Horizontal };
            topRow.Children.Add(new TextBlock { Text = $"Orden: {oi.Po}", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center });
            var badge = new Border { Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            badge.Child = new TextBlock { Text = "VINCULADA", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            topRow.Children.Add(badge);
            if (!string.IsNullOrEmpty(oi.Client))
                topRow.Children.Add(new TextBlock { Text = $"  |  {oi.Client}", FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center });
            info.Children.Add(topRow);
            if (!string.IsNullOrEmpty(oi.Detail))
                info.Children.Add(new TextBlock { Text = oi.Detail, FontSize = 12, Foreground = TextMuted, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(info, 1); g.Children.Add(info);
            header.Child = g;

            // Insert before ContentHost in the parent StackPanel
            if (ContentHost.Parent is StackPanel parentSp)
            {
                var idx = parentSp.Children.IndexOf(ContentHost);
                if (idx >= 0) parentSp.Children.Insert(idx, header);
            }
        }

        // BUG-4: Load global storage once at startup
        async Task LoadGlobalStorage()
        {
            try
            {
                _globalStorageBytes = await SupabaseService.Instance.GetDriveTotalStorageBytes(_cts.Token);
                Debug.WriteLine($"[DriveV2] Global storage loaded: {Services.Drive.DriveService.FormatFileSize(_globalStorageBytes)} ({_globalStorageBytes} bytes)");
                UpdateStorageUI();
            }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] Global storage ERR: {ex.Message}\n{ex.StackTrace}"); }
        }

        // BUG-4: Update storage indicator from global bytes
        void UpdateStorageUI()
        {
            if (_globalStorageBytes < 0) return;
            long maxB = 10L * 1024 * 1024 * 1024;
            var pct = Math.Max(1, Math.Min(100, (int)(_globalStorageBytes * 100 / maxB)));
            StorageLabel.Text = $"{Services.Drive.DriveService.FormatFileSize(_globalStorageBytes)} de 10 GB";
            StorageAvailLabel.Text = $"{Services.Drive.DriveService.FormatFileSize(maxB - _globalStorageBytes)} disponibles";
            StorageFillCol.Width = new GridLength(pct, GridUnitType.Star);
            StorageEmptyCol.Width = new GridLength(100 - pct, GridUnitType.Star);
        }

        void InvalidateStats(int? fId = null)
        {
            if (fId.HasValue) { _statsCache.Remove(fId.Value); _folderCache.Remove(fId.Value); }
            else { _statsCache.Clear(); _folderCache.Clear(); }
            if (_currentFolderId.HasValue) { _statsCache.Remove(_currentFolderId.Value); _folderCache.Remove(_currentFolderId.Value); }
        }

        // ===============================================
        // BREADCRUMB
        // ===============================================
        void RenderBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();
            var h = new TextBlock { Text = "\uE80F", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            h.MouseLeftButtonDown += async (s, e) => await SafeLoad(() => NavigateToRoot());
            BreadcrumbPanel.Children.Add(h);
            for (int i = 0; i < _breadcrumb.Count; i++)
            {
                var f = _breadcrumb[i]; var last = i == _breadcrumb.Count - 1; var fId = f.Id;
                BreadcrumbPanel.Children.Add(new TextBlock { Text = "\uE76C", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Foreground = TextLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
                var seg = new TextBlock { Text = f.Name, FontSize = 13, Foreground = last ? TextPrimary : TextMuted, FontWeight = last ? FontWeights.Medium : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Cursor = last ? Cursors.Arrow : Cursors.Hand };
                if (!last) { seg.MouseEnter += (s, e) => ((TextBlock)s!).Foreground = Primary; seg.MouseLeave += (s, e) => ((TextBlock)s!).Foreground = TextMuted; seg.MouseLeftButtonDown += async (s, e) => await SafeLoad(() => NavTo(fId)); }
                BreadcrumbPanel.Children.Add(seg);
            }
        }

        // ===============================================
        // CONTENT - Responsive grid + sortable list
        // ===============================================
        void RenderContent()
        {
            EmptyState.Visibility = Visibility.Collapsed;
            var filteredFiles = _activeFilter != null ? ApplyFileFilter(_currentFiles, _activeFilter) : _currentFiles;
            var sortedFiles = SortFiles(filteredFiles);
            if (_viewMode == "list") RenderList(sortedFiles);
            else RenderWrap(sortedFiles);
        }

        // MEJORA-7: Sort files by current sort field
        List<DriveFileDb> SortFiles(List<DriveFileDb> files)
        {
            if (files.Count == 0) return files;
            return (_sortField, _sortAsc) switch
            {
                ("name", true) => files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
                ("name", false) => files.OrderByDescending(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
                ("type", true) => files.OrderBy(f => System.IO.Path.GetExtension(f.FileName)?.ToLowerInvariant()).ThenBy(f => f.FileName).ToList(),
                ("type", false) => files.OrderByDescending(f => System.IO.Path.GetExtension(f.FileName)?.ToLowerInvariant()).ThenBy(f => f.FileName).ToList(),
                ("size", true) => files.OrderBy(f => f.FileSize ?? 0).ToList(),
                ("size", false) => files.OrderByDescending(f => f.FileSize ?? 0).ToList(),
                ("date", true) => files.OrderBy(f => f.UploadedAt).ToList(),
                ("date", false) => files.OrderByDescending(f => f.UploadedAt).ToList(),
                _ => files
            };
        }

        // MEJORA-5: Responsive grid with section headers
        void RenderWrap(List<DriveFileDb> files)
        {
            var stk = new StackPanel();

            // MEJORA-8: Section header for folders
            if (_currentFolders.Count > 0 && files.Count > 0)
            {
                stk.Children.Add(new TextBlock { Text = $"Carpetas ({_currentFolders.Count})", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextMuted, Margin = new Thickness(6, 0, 0, 8) });
            }
            if (_currentFolders.Count > 0)
            {
                var fw = new WrapPanel();
                foreach (var f in _currentFolders) { var c = MkFolderCard(f); c.Width = 280; c.Margin = new Thickness(6); fw.Children.Add(c); }
                stk.Children.Add(fw);
            }

            // MEJORA-8: Section header for files
            if (files.Count > 0 && _currentFolders.Count > 0)
            {
                stk.Children.Add(new TextBlock { Text = $"Archivos ({files.Count})", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextMuted, Margin = new Thickness(6, 20, 0, 8) });
            }
            if (files.Count > 0)
            {
                var filew = new WrapPanel();
                foreach (var f in files) { var c = MkFileCard(f); c.Width = 200; c.Margin = new Thickness(6); filew.Children.Add(c); }
                stk.Children.Add(filew);
            }

            ContentHost.Content = stk.Children.Count > 0 ? stk : new WrapPanel();
        }

        // MEJORA-7: List view with sortable column headers
        void RenderList(List<DriveFileDb> files)
        {
            var wrap = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), ClipToBounds = true };
            var stk = new StackPanel();

            // Sortable header row
            var hg = new Grid { Background = Background, Height = 40 };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var hdrDefs = new[] { ("NOMBRE", "name"), ("TIPO", "type"), ("TAMANO", "size"), ("ORDEN / UBICACION", ""), ("MODIFICADO", "date") };
            for (int i = 0; i < hdrDefs.Length; i++)
            {
                var (label, field) = hdrDefs[i];
                var isSortable = !string.IsNullOrEmpty(field);
                var isActive = _sortField == field;
                var arrow = isActive ? (_sortAsc ? " \u25B2" : " \u25BC") : "";
                var t = new TextBlock { Text = label + arrow, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = isActive ? Primary : TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(i == 0 ? 24 : 0, 0, 0, 0), Cursor = isSortable ? Cursors.Hand : Cursors.Arrow };
                if (isSortable)
                {
                    var f = field; // capture
                    t.MouseLeftButtonDown += (s, e) =>
                    {
                        if (_sortField == f) _sortAsc = !_sortAsc; else { _sortField = f; _sortAsc = true; }
                        RenderContent();
                    };
                    t.MouseEnter += (s, e) => { if (_sortField != f) t.Foreground = Primary; };
                    t.MouseLeave += (s, e) => { if (_sortField != f) t.Foreground = TextMuted; };
                }
                Grid.SetColumn(t, i); hg.Children.Add(t);
            }
            stk.Children.Add(new Border { BorderBrush = BorderColor, BorderThickness = new Thickness(0, 0, 0, 1), Child = hg });

            foreach (var f in _currentFolders) stk.Children.Add(MkListRow(f));
            // MEJORA-8: Separator between folders and files in list view
            if (_currentFolders.Count > 0 && files.Count > 0)
            {
                var sep = new Border { Height = 1, Background = BorderColor, Margin = new Thickness(24, 4, 24, 4) };
                stk.Children.Add(sep);
            }
            foreach (var f in files) stk.Children.Add(MkFileListRow(f));
            wrap.Child = stk; ContentHost.Content = wrap;
        }

        UIElement MkListRow(DriveFolderDb folder)
        {
            var ac = CH(FolderColors[folder.Id % FolderColors.Length]);
            var rg = new Grid { Height = 48, Cursor = Cursors.Hand, Background = Brushes.White };
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
            var ib = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(38, ac.R, ac.G, ac.B)), Margin = new Thickness(0, 0, 10, 0) };
            ib.Child = MkFolderIco(14, new SolidColorBrush(ac)); np.Children.Add(ib);
            var nt = new TextBlock { Text = folder.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center, Tag = $"folderName_{folder.Id}" }; np.Children.Add(nt);
            Grid.SetColumn(np, 0); rg.Children.Add(np);
            var hs = _statsCache.TryGetValue(folder.Id, out var st);
            AddCol(rg, 1, "Carpeta", TextMuted);
            AddCol(rg, 2, hs ? Services.Drive.DriveService.FormatFileSize(st.totalSize) : "-");
            var otx = OrderTxt(folder.LinkedOrderId); AddCol(rg, 3, otx, folder.LinkedOrderId.HasValue ? Primary : TextMuted);
            AddCol(rg, 4, RelT(folder.UpdatedAt));
            var rb = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1), Child = rg };
            rb.MouseEnter += (s, e) => { rg.Background = HoverBg; nt.Foreground = Primary; }; rb.MouseLeave += (s, e) => { rg.Background = Brushes.White; nt.Foreground = TextPrimary; };
            // MEJORA-9: Double-click to open folder (change ClickCount == 2 to 1 for single-click)
            rb.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = SafeLoad(() => NavTo(folder.Id)); };
            var linked = folder.LinkedOrderId.HasValue;
            rb.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavTo(folder.Id)))); m.Items.Add(MI("Renombrar", (_, _) => RenFolder(folder))); m.Items.Add(new Separator()); m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkOrder(folder))); if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await Unlink(folder))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFolder(folder)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = rb; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
            return rb;
        }

        // File list row for list view
        UIElement MkFileListRow(DriveFileDb file)
        {
            var (cH, _) = GFC(file.FileName); var fC = CH(cH); var fB = new SolidColorBrush(fC);
            var ext = System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            var sel = _selectedFileIds.Contains(file.Id);
            var rg = new Grid { Height = 48, Cursor = Cursors.Hand, Background = sel ? ActiveBg : Brushes.White };
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            // Selection circle for list row
            var selCircle = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, Visibility = sel ? Visibility.Visible : Visibility.Collapsed, Background = sel ? Primary : Brushes.White, BorderBrush = sel ? Primary : SlateLight, BorderThickness = new Thickness(2), VerticalAlignment = VerticalAlignment.Center };
            selCircle.Child = new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = sel ? Visibility.Visible : Visibility.Collapsed };
            selCircle.MouseLeftButtonDown += (s2, e2) => { e2.Handled = true; ToggleFileSelect(file); };
            np.Children.Add(selCircle);
            var icB = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(25, fC.R, fC.G, fC.B)), Margin = new Thickness(0, 0, 10, 0) };
            icB.Child = new TextBlock { Text = FIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = fB, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            np.Children.Add(icB);
            var nt = new TextBlock { Text = file.FileName, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 400, ToolTip = file.FileName, Tag = $"fileName_{file.Id}" }; np.Children.Add(nt);
            Grid.SetColumn(np, 0); rg.Children.Add(np);
            // Badge de extension
            var extBadge = new Border { Background = new SolidColorBrush(Color.FromArgb(25, fC.R, fC.G, fC.B)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            extBadge.Child = new TextBlock { Text = ext, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = fB }; Grid.SetColumn(extBadge, 1); rg.Children.Add(extBadge);
            AddCol(rg, 2, Services.Drive.DriveService.FormatFileSize(file.FileSize));
            AddCol(rg, 3, _breadcrumb.LastOrDefault()?.Name ?? "-");
            AddCol(rg, 4, RelT(file.UploadedAt));
            var rb = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1), Child = rg };
            rb.MouseEnter += (s, e) => { selCircle.Visibility = Visibility.Visible; if (!_selectedFileIds.Contains(file.Id)) rg.Background = HoverBg; nt.Foreground = Primary; };
            rb.MouseLeave += (s, e) => { if (!_selectedFileIds.Contains(file.Id)) { selCircle.Visibility = Visibility.Collapsed; rg.Background = Brushes.White; } nt.Foreground = TextPrimary; };
            rb.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { _ = DlFile(file); return; }
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ToggleFileSelect(file); }
                else { ClearMultiSelect(); _selectedFile = file; _selectedFileIds.Add(file.Id); UpdateMultiSelectBar(); ShowDetail(file); RenderContent(); }
            };
            rb.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Descargar", async (_, _) => await DlFile(file))); m.Items.Add(MI("Renombrar", (_, _) => RenFile(file))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFile(file)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = rb; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
            return rb;
        }

        void AddCol(Grid g, int col, string txt, SolidColorBrush? fg = null) { var t = new TextBlock { Text = txt, FontSize = 12, Foreground = fg ?? TextMuted, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(t, col); g.Children.Add(t); }

        // ===============================================
        // FOLDER CARD (P4 no MaxWidth, P9 rich tooltip)
        // ===============================================
        Border MkFolderCard(DriveFolderDb folder)
        {
            var aH = FolderColors[folder.Id % FolderColors.Length]; var aC = CH(aH); var aB = new SolidColorBrush(aC);
            var tint = new SolidColorBrush(Color.FromArgb(38, aC.R, aC.G, aC.B)); var linked = folder.LinkedOrderId.HasValue;
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ClipToBounds = true };
            var mg = new Grid(); mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var acc = new Border { Background = aB, Height = 4 }; Grid.SetRow(acc, 0); mg.Children.Add(acc);
            // Header
            var hg = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icBdr = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), Background = tint, Margin = new Thickness(0, 0, 12, 0) };
            icBdr.Child = MkFolderIco(22, aB); Grid.SetColumn(icBdr, 0); hg.Children.Add(icBdr);
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            // P4: no MaxWidth, use TextWrapping
            var nameT = new TextBlock { Text = folder.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(0, 0, 0, 2), ToolTip = folder.Name, Tag = $"folderName_{folder.Id}" };
            info.Children.Add(nameT);
            if (linked && _orderInfoCache.TryGetValue(folder.LinkedOrderId!.Value, out var oi))
            {
                // Labeled order info like portal ventas
                var orderInfo = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
                orderInfo.Children.Add(new TextBlock { FontSize = 11, Foreground = Primary, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = MkOrderTip(folder.LinkedOrderId), Inlines = { new System.Windows.Documents.Run("Orden: ") { Foreground = TextMuted, FontSize = 11 }, new System.Windows.Documents.Run(oi.Po) { FontWeight = FontWeights.SemiBold } } });
                if (!string.IsNullOrEmpty(oi.Client))
                    orderInfo.Children.Add(new TextBlock { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Inlines = { new System.Windows.Documents.Run("Cliente: ") { Foreground = TextMuted }, new System.Windows.Documents.Run(Tr(oi.Client, 20)) { Foreground = TextPrimary } } });
                if (!string.IsNullOrEmpty(oi.Detail))
                    orderInfo.Children.Add(new TextBlock { FontSize = 10, Foreground = TextLight, Text = Tr(oi.Detail, 30), TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(orderInfo);
            }
            else if (linked) info.Children.Add(new TextBlock { Text = $"Orden: #{folder.LinkedOrderId}", FontSize = 11, Foreground = Primary });
            Grid.SetColumn(info, 1); hg.Children.Add(info);
            var moreBtn = new Button { Style = FindResource("IconButton") as Style, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Top };
            moreBtn.Content = new TextBlock { Text = "\uE712", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = TextMuted };
            // More button opens the same context menu as right-click
            moreBtn.Click += (s, e) =>
            {
                var m = new ContextMenu();
                m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavTo(folder.Id))));
                m.Items.Add(MI("Renombrar", (_, _) => RenFolder(folder)));
                m.Items.Add(new Separator());
                m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkOrder(folder)));
                if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await Unlink(folder)));
                m.Items.Add(new Separator());
                var del = MI("Eliminar", async (_, _) => await DelFolder(folder)); del.Foreground = Destructive; m.Items.Add(del);
                m.PlacementTarget = moreBtn; m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                m.IsOpen = true;
            };
            Grid.SetColumn(moreBtn, 2); hg.Children.Add(moreBtn);
            Grid.SetRow(hg, 1); mg.Children.Add(hg);
            // Stats
            var hs = _statsCache.TryGetValue(folder.Id, out var st);
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 20, 0) };
            sp.Children.Add(new TextBlock { Text = "\uD83D\uDCC4", FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = hs ? $"{st.fileCount} archivo{(st.fileCount != 1 ? "s" : "")}" : "...", FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center });
            if (hs && st.totalSize > 0) { sp.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = SlateLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) }); sp.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(st.totalSize), FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center }); }
            if (hs && st.subCount > 0) { sp.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = SlateLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) }); sp.Children.Add(new TextBlock { Text = $"{st.subCount} subcarpeta{(st.subCount != 1 ? "s" : "")}", FontSize = 12, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center }); }
            Grid.SetRow(sp, 2); mg.Children.Add(sp);
            // Footer
            var ft = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(20, 16, 20, 0), Padding = new Thickness(0, 12, 0, 16) };
            ft.Child = new TextBlock { Text = $"Modificado {RelT(folder.UpdatedAt)}", FontSize = 11, Foreground = TextLight };
            Grid.SetRow(ft, 3); mg.Children.Add(ft);
            // Selection mode: dim folders already linked to other orders
            var blockedInSelection = _isSelectionMode && linked;
            if (blockedInSelection)
            {
                card.Opacity = 0.5;
                card.Cursor = Cursors.No;
                // Add "VINCULADA" badge on the accent bar
                var badge = new TextBlock { Text = "VINCULADA", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                acc.Child = badge; acc.Height = 18;
            }
            card.Child = mg;
            card.MouseEnter += (s, e) => { if (!blockedInSelection) { card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 4, Opacity = 0.08 }; nameT.Foreground = Primary; moreBtn.Visibility = Visibility.Visible; } };
            card.MouseLeave += (s, e) => { card.Effect = null; nameT.Foreground = TextPrimary; moreBtn.Visibility = Visibility.Collapsed; };
            // MEJORA-9: Double-click to open folder (configurable: change ClickCount == 2 to ClickCount == 1 for single-click navigation)
            card.MouseLeftButtonDown += (s, e) => { if (blockedInSelection) return; if (e.ClickCount == 2) _ = SafeLoad(() => NavTo(folder.Id)); };
            card.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavTo(folder.Id)))); m.Items.Add(MI("Renombrar", (_, _) => RenFolder(folder))); m.Items.Add(new Separator()); m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkOrder(folder))); if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await Unlink(folder))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFolder(folder)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = card; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
            return card;
        }

        // ===============================================
        // FILE CARD
        // ===============================================
        Border MkFileCard(DriveFileDb file)
        {
            var (cH, bH) = GFC(file.FileName); var fC = CH(cH); var fB = new SolidColorBrush(fC); var bgB = BH(bH);
            var sel = _selectedFileIds.Contains(file.Id);
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = sel ? Primary : BorderColor, BorderThickness = new Thickness(2), Cursor = Cursors.Hand, ClipToBounds = true };
            if (sel) card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 12, ShadowDepth = 2, Opacity = 0.15 };
            var mg = new Grid(); mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var prev = new Border { Background = bgB, CornerRadius = new CornerRadius(10, 10, 0, 0) }; var pg = new Grid();
            pg.Children.Add(new TextBlock { Text = FIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 48, Foreground = fB, Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var ext = System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            var badge = new Border { Background = fB, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0) };
            badge.Child = new TextBlock { Text = ext, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }; pg.Children.Add(badge);
            // Selection circle (top-left)
            var selCircle = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 10, 0, 0), Cursor = Cursors.Hand, Visibility = sel ? Visibility.Visible : Visibility.Collapsed, Background = sel ? Primary : Brushes.White, BorderBrush = sel ? Primary : SlateLight, BorderThickness = new Thickness(2) };
            selCircle.Child = new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = sel ? Visibility.Visible : Visibility.Collapsed };
            selCircle.MouseLeftButtonDown += (s, e2) => { e2.Handled = true; ToggleFileSelect(file); };
            pg.Children.Add(selCircle);
            prev.Child = pg; Grid.SetRow(prev, 0); mg.Children.Add(prev);
            var ip = new StackPanel { Margin = new Thickness(16) };
            var nameT = new TextBlock { Text = file.FileName, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8), ToolTip = file.FileName, Tag = $"fileName_{file.Id}" }; ip.Children.Add(nameT);
            var sg = new Grid();
            var isRecent = file.UploadedAt.HasValue && (DateTime.Now - file.UploadedAt.Value).TotalHours < 1;
            sg.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(file.FileSize), FontSize = 11, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Left });
            if (isRecent)
            {
                // MEJORA-9: Recent file indicator
                var recentBadge = new Border { Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                recentBadge.Child = new TextBlock { Text = "Nuevo", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                sg.Children.Add(recentBadge);
            }
            else sg.Children.Add(new TextBlock { Text = RelT(file.UploadedAt), FontSize = 11, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right });
            ip.Children.Add(sg);
            Grid.SetRow(ip, 1); mg.Children.Add(ip); card.Child = mg;
            card.MouseEnter += (s, e) => { selCircle.Visibility = Visibility.Visible; if (!_selectedFileIds.Contains(file.Id)) { card.BorderBrush = SlateLight; card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 4, Opacity = 0.08 }; } nameT.Foreground = Primary; };
            card.MouseLeave += (s, e) => { if (!_selectedFileIds.Contains(file.Id)) { selCircle.Visibility = Visibility.Collapsed; card.BorderBrush = BorderColor; card.Effect = null; } nameT.Foreground = TextPrimary; };
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { _ = DlFile(file); return; }
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ToggleFileSelect(file); }
                else { ClearMultiSelect(); _selectedFile = file; _selectedFileIds.Add(file.Id); UpdateMultiSelectBar(); ShowDetail(file); RenderContent(); }
            };
            card.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Descargar", async (_, _) => await DlFile(file))); m.Items.Add(MI("Renombrar", (_, _) => RenFile(file))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFile(file)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = card; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
            return card;
        }

        // ===============================================
        // DETAIL PANEL (P6 safe icons)
        // ===============================================
        async void ShowDetail(DriveFileDb file)
        {
            var (cH, _) = GFC(file.FileName); var fC = CH(cH);
            DetailFileName.Text = file.FileName;
            DetailFileExt.Text = $"Archivo {System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToUpperInvariant()}";
            DetailPreviewIcon.Text = FIcon(file.FileName);
            DetailPreviewBg.Background = new LinearGradientBrush(Color.FromArgb(20, fC.R, fC.G, fC.B), Color.FromArgb(40, fC.R, fC.G, fC.B), 45);
            DetailPreviewIcon.Foreground = new SolidColorBrush(fC);
            var uploader = await ResolveUser(file.UploadedBy);
            var loc = _breadcrumb.Count > 0 ? string.Join(" / ", _breadcrumb.Select(b => b.Name)) : "IMA Drive";
            DetailInfoPanel.Children.Clear();
            foreach (var (ico, lbl, val) in new[] {
                ("\uE8A5", "Tipo", FType(file.FileName)),
                ("\uE7F8", "Tamano", Services.Drive.DriveService.FormatFileSize(file.FileSize)),
                ("\uE787", "Fecha de subida", file.UploadedAt?.ToString("dd 'de' MMMM, yyyy") ?? "Sin fecha"),
                ("\uE77B", "Subido por", uploader),
                ("\uE8B7", "Ubicacion", loc) })
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 16) };
                var ib2 = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(8), Background = Background, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                ib2.Child = new TextBlock { Text = ico, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(ib2);
                var tp = new StackPanel { Margin = new Thickness(44, 0, 0, 0) };
                tp.Children.Add(new TextBlock { Text = lbl, FontSize = 11, Foreground = TextLight, Margin = new Thickness(0, 0, 0, 2) });
                tp.Children.Add(new TextBlock { Text = val, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = TextPrimary, TextWrapping = TextWrapping.Wrap });
                g.Children.Add(tp); DetailInfoPanel.Children.Add(g);
            }
            DetailPanel.Visibility = Visibility.Visible;
        }
        void HideDetail() { _selectedFile = null; DetailPanel.Visibility = Visibility.Collapsed; }

        // P9: Rich order tooltip
        UIElement? MkOrderTip(int? oid)
        {
            if (!oid.HasValue || !_orderInfoCache.TryGetValue(oid.Value, out var oi)) return null;
            var p = new StackPanel { MaxWidth = 280 };
            var hdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            hdr.Children.Add(new TextBlock { Text = $"Orden: {oi.Po}", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center });
            var bdg = new Border { Background = new SolidColorBrush(Color.FromRgb(0xED, 0xEF, 0xF2)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            bdg.Child = new TextBlock { Text = "VINCULADA", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = TextMuted }; hdr.Children.Add(bdg); p.Children.Add(hdr);
            var inf = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), BorderThickness = new Thickness(2, 0, 0, 0), Padding = new Thickness(10, 4, 0, 4) };
            var ist = new StackPanel();
            if (!string.IsNullOrEmpty(oi.Client)) { var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) }; r.Children.Add(new TextBlock { Text = "Cliente:", FontSize = 12, Foreground = TextMuted, Width = 55 }); r.Children.Add(new TextBlock { Text = oi.Client, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary }); ist.Children.Add(r); }
            if (!string.IsNullOrEmpty(oi.Detail)) { var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) }; r.Children.Add(new TextBlock { Text = "Detalle:", FontSize = 12, Foreground = TextMuted, Width = 55 }); r.Children.Add(new TextBlock { Text = oi.Detail, FontSize = 12, Foreground = TextPrimary, TextWrapping = TextWrapping.Wrap }); ist.Children.Add(r); }
            inf.Child = ist; p.Children.Add(inf); return p;
        }

        // ===============================================
        // VIEW TOGGLE
        // ===============================================
        void UpdateViewToggle()
        {
            GridViewBtn.Background = _viewMode == "grid" ? Brushes.White : Brushes.Transparent;
            if (GridViewBtn.Content is TextBlock gi) gi.Foreground = _viewMode == "grid" ? Primary : TextMuted;
            GridViewBtn.Effect = _viewMode == "grid" ? new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.1 } : null;
            ListViewBtn.Background = _viewMode == "list" ? Brushes.White : Brushes.Transparent;
            if (ListViewBtn.Content is TextBlock li) li.Foreground = _viewMode == "list" ? Primary : TextMuted;
            ListViewBtn.Effect = _viewMode == "list" ? new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.1 } : null;
        }

        // ===============================================
        // CRUD (P3 improved upload panel)
        // ===============================================
        // Inline folder creation - works in both grid and list view
        private bool _isCreatingFolder; // prevents Escape from navigating back
        void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            _isCreatingFolder = true;
            var committed = false;

            // Use simple prompt dialog instead of inline card - works in both views
            var aC = CH(FolderColors[(_currentFolders.Count + 1) % FolderColors.Length]);
            var aB = new SolidColorBrush(aC);
            var tint = new SolidColorBrush(Color.FromArgb(38, aC.R, aC.G, aC.B));

            UIElement? inlineEl = null;
            TextBox? tb = null;

            if (_viewMode == "grid")
            {
                // Grid: inline editable card - find first WrapPanel (folders section)
                WrapPanel? wp = null;
                if (ContentHost.Content is StackPanel gridStk) wp = gridStk.Children.OfType<WrapPanel>().FirstOrDefault();
                else if (ContentHost.Content is WrapPanel directWp) wp = directWp;
                if (wp == null) { RenderWrap(SortFiles(_activeFilter != null ? ApplyFileFilter(_currentFiles, _activeFilter) : _currentFiles)); if (ContentHost.Content is StackPanel ns) wp = ns.Children.OfType<WrapPanel>().FirstOrDefault(); else if (ContentHost.Content is WrapPanel nw) wp = nw; }
                if (wp == null) { _isCreatingFolder = false; return; }

                var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = Primary, BorderThickness = new Thickness(2), Width = 280, Margin = new Thickness(6), Cursor = Cursors.IBeam, ClipToBounds = true };
                var mg = new Grid();
                mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var acc = new Border { Background = aB, Height = 4 }; Grid.SetRow(acc, 0); mg.Children.Add(acc);
                var hg = new Grid { Margin = new Thickness(20, 16, 20, 16) };
                hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var icBdr = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), Background = tint, Margin = new Thickness(0, 0, 12, 0) };
                icBdr.Child = MkFolderIco(22, aB); Grid.SetColumn(icBdr, 0); hg.Children.Add(icBdr);
                var tbBdr = new Border { CornerRadius = new CornerRadius(6), BorderBrush = Primary, BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
                tb = new TextBox { Text = "Nueva carpeta", FontSize = 14, FontWeight = FontWeights.SemiBold, Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(0) };
                tb.SelectAll();
                tbBdr.Child = tb; Grid.SetColumn(tbBdr, 1); hg.Children.Add(tbBdr);
                Grid.SetRow(hg, 1); mg.Children.Add(hg); card.Child = mg;
                wp.Children.Insert(0, card);
                inlineEl = card;
            }
            else
            {
                // List: inline editable row
                var stk = (ContentHost.Content as Border)?.Child as StackPanel;
                if (stk == null) { _isCreatingFolder = false; return; }
                var rg = new Grid { Height = 48, Background = ActiveBg };
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
                var ib = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = tint, Margin = new Thickness(0, 0, 10, 0) };
                ib.Child = MkFolderIco(14, aB); np.Children.Add(ib);
                var tbBdr = new Border { CornerRadius = new CornerRadius(4), BorderBrush = Primary, BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center, Width = 300 };
                tb = new TextBox { Text = "Nueva carpeta", FontSize = 13, FontWeight = FontWeights.SemiBold, Padding = new Thickness(6, 4, 6, 4), BorderThickness = new Thickness(0) };
                tb.SelectAll();
                tbBdr.Child = tb; np.Children.Add(tbBdr);
                Grid.SetColumn(np, 0); rg.Children.Add(np);
                var row = new Border { BorderBrush = Primary, BorderThickness = new Thickness(0, 0, 0, 2), Child = rg };
                // Insert after header row (index 1)
                var insertIdx = Math.Min(1, stk.Children.Count);
                stk.Children.Insert(insertIdx, row);
                inlineEl = row;
            }

            void Cleanup() { _isCreatingFolder = false; if (inlineEl is FrameworkElement fe && fe.Parent is Panel p) p.Children.Remove(inlineEl); }

            tb.Loaded += (s2, e2) => { tb.Focus(); tb.SelectAll(); };
            tb.KeyDown += async (s, ke) =>
            {
                if (ke.Key == Key.Enter && !committed)
                {
                    ke.Handled = true; committed = true;
                    var name = tb.Text.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        await SafeLoad(async () =>
                        {
                            var created = await SupabaseService.Instance.CreateDriveFolder(name, _currentFolderId, _currentUser.Id, _cts.Token);
                            if (created != null) { InvalidateStats(); await LoadFolder(); }
                            else { ShowToast("Nombre duplicado", "warning"); Cleanup(); }
                        });
                    }
                    else Cleanup();
                    _isCreatingFolder = false;
                }
                else if (ke.Key == Key.Escape) { ke.Handled = true; Cleanup(); }
            };
            tb.LostFocus += (s, le) => { if (!committed) Cleanup(); };
        }

        async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue) { ShowToast("Abra una carpeta primero", "warning"); return; }
            if (!SupabaseService.Instance.IsDriveStorageConfigured) { ShowToast("R2 Storage no configurado", "warning"); return; }
            var dlg = new OpenFileDialog { Title = "Seleccionar archivos", Multiselect = true, Filter = "Todos (*.*)|*.*" }; if (dlg.ShowDialog() != true) return;
            await UploadFiles(dlg.FileNames);
        }

        // Upload panel removed - ghost cards handle progress in-place

        async Task DlFile(DriveFileDb file) { if (!SupabaseService.Instance.IsDriveStorageConfigured) { ShowToast("R2 Storage no configurado", "warning"); return; } var d = new SaveFileDialog { Title = "Guardar", FileName = file.FileName, Filter = "Todos (*.*)|*.*" }; if (d.ShowDialog() != true) return; await SafeLoad(async () => { StatusText.Text = $"Descargando {file.FileName}..."; var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, d.FileName, _cts.Token); StatusText.Text = ok ? $"{file.FileName} descargado" : "Error"; }); }
        void RenFolder(DriveFolderDb f)
        {
            var tag = $"folderName_{f.Id}";
            var nameBlock = FindByTag<TextBlock>(ContentHost, tag);
            if (nameBlock == null) { var n = Prompt("Renombrar", "Nuevo nombre:", f.Name); if (string.IsNullOrWhiteSpace(n) || n == f.Name) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFolder(f.Id, n, _cts.Token)) { InvalidateStats(); await LoadFolder(); } }); return; }

            var parent = nameBlock.Parent as Panel;
            if (parent == null) return;
            var idx = parent.Children.IndexOf(nameBlock);

            var tbBdr = new Border { CornerRadius = new CornerRadius(4), BorderBrush = Primary, BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true, Margin = nameBlock.Margin, VerticalAlignment = nameBlock.VerticalAlignment };
            var tb = new TextBox { Text = f.Name, FontSize = nameBlock.FontSize, FontWeight = nameBlock.FontWeight, Padding = new Thickness(4, 2, 4, 2), BorderThickness = new Thickness(0), MinWidth = 80, MaxWidth = _viewMode == "list" ? 300 : 180 };
            tbBdr.Child = tb;

            nameBlock.Visibility = Visibility.Collapsed;
            parent.Children.Insert(idx + 1, tbBdr);
            _isCreatingFolder = true;
            tb.SelectAll();
            tb.Loaded += (s2, e2) => { tb.Focus(); tb.SelectAll(); };

            var committed = false;
            void Commit()
            {
                if (committed) return; committed = true;
                _isCreatingFolder = false;
                var newName = tb.Text.Trim();
                parent.Children.Remove(tbBdr);
                nameBlock.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(newName) || newName == f.Name) return;
                nameBlock.Text = newName;
                if (nameBlock.ToolTip != null) nameBlock.ToolTip = newName;
                _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFolder(f.Id, newName, _cts.Token)) { InvalidateStats(); await LoadFolder(); } });
            }

            tb.KeyDown += (s, ke) =>
            {
                if (ke.Key == Key.Enter) { ke.Handled = true; Commit(); }
                else if (ke.Key == Key.Escape) { ke.Handled = true; committed = true; _isCreatingFolder = false; parent.Children.Remove(tbBdr); nameBlock.Visibility = Visibility.Visible; }
            };
            tb.LostFocus += (s, le) => Commit();
        }
        void RenFile(DriveFileDb f)
        {
            // Find the name TextBlock in the rendered UI
            var tag = $"fileName_{f.Id}";
            var nameBlock = FindByTag<TextBlock>(ContentHost, tag);
            if (nameBlock == null) { Debug.WriteLine($"[DriveV2] RenFile: TextBlock '{tag}' not found, fallback to prompt"); var n = Prompt("Renombrar", "Nuevo nombre:", f.FileName); if (string.IsNullOrWhiteSpace(n) || n == f.FileName) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFile(f.Id, n, _cts.Token)) await LoadFolder(); }); return; }

            var parent = nameBlock.Parent as Panel;
            if (parent == null) return;
            var idx = parent.Children.IndexOf(nameBlock);

            // Split name and extension
            var ext = System.IO.Path.GetExtension(f.FileName) ?? "";
            var nameOnly = System.IO.Path.GetFileNameWithoutExtension(f.FileName);

            // Create inline TextBox + extension label
            var editPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = nameBlock.Margin, VerticalAlignment = nameBlock.VerticalAlignment };
            var tbBdr = new Border { CornerRadius = new CornerRadius(4), BorderBrush = Primary, BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true };
            var tb = new TextBox { Text = nameOnly, FontSize = nameBlock.FontSize, FontWeight = nameBlock.FontWeight, Padding = new Thickness(4, 2, 4, 2), BorderThickness = new Thickness(0), MinWidth = 80, MaxWidth = _viewMode == "list" ? 300 : 160 };
            tbBdr.Child = tb;
            editPanel.Children.Add(tbBdr);
            editPanel.Children.Add(new TextBlock { Text = ext, FontSize = nameBlock.FontSize, FontWeight = nameBlock.FontWeight, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) });

            // Swap
            nameBlock.Visibility = Visibility.Collapsed;
            parent.Children.Insert(idx + 1, editPanel);
            _isCreatingFolder = true; // reuse flag to block Escape navigation
            tb.SelectAll();
            tb.Loaded += (s2, e2) => { tb.Focus(); tb.SelectAll(); };

            var committed = false;
            void Commit()
            {
                if (committed) return; committed = true;
                _isCreatingFolder = false;
                var newName = tb.Text.Trim();
                parent.Children.Remove(editPanel);
                nameBlock.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(newName) || newName == nameOnly) return;
                var fullName = newName + ext;
                nameBlock.Text = fullName;
                nameBlock.ToolTip = fullName;
                _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFile(f.Id, fullName, _cts.Token)) await LoadFolder(); });
            }

            tb.KeyDown += (s, ke) =>
            {
                if (ke.Key == Key.Enter) { ke.Handled = true; Commit(); }
                else if (ke.Key == Key.Escape) { ke.Handled = true; committed = true; _isCreatingFolder = false; parent.Children.Remove(editPanel); nameBlock.Visibility = Visibility.Visible; }
            };
            tb.LostFocus += (s, le) => Commit();
        }
        async Task DelFolder(DriveFolderDb f) { if (!Confirm($"Eliminar \"{f.Name}\" y todo su contenido?")) return; await SafeLoad(async () => { if (await SupabaseService.Instance.DeleteDriveFolder(f.Id, _cts.Token)) { _statsCache.Remove(f.Id); InvalidateStats(); ShowToast($"\"{f.Name}\" eliminada", "success"); await LoadFolder(); } }); }
        async Task DelFile(DriveFileDb f) { if (!Confirm($"Eliminar \"{f.FileName}\"?")) return; await SafeLoad(async () => { if (await SupabaseService.Instance.DeleteDriveFile(f.Id, _cts.Token)) { if (_selectedFile?.Id == f.Id) HideDetail(); if (_globalStorageBytes > 0) { _globalStorageBytes -= f.FileSize ?? 0; UpdateStorageUI(); } InvalidateStats(); ShowToast($"\"{f.FileName}\" eliminado", "success"); await LoadFolder(); } }); }

        // ===============================================
        // ORDER LINKING
        // ===============================================
        async void LinkOrder(DriveFolderDb folder)
        {
            // Validate link rules before showing order picker
            var validation = await SupabaseService.Instance.ValidateDriveFolderLink(folder.Id, _cts.Token);
            if (!validation.CanLink) { ShowToast(validation.BlockReason ?? "No se puede vincular", "warning", 4000); return; }

            // Load data in parallel
            var ordersTask = SupabaseService.Instance.GetOrders(200);
            var treeTask = SupabaseService.Instance.GetDriveFolderTree(_cts.Token);
            var clientsTask = SupabaseService.Instance.GetClients();
            await Task.WhenAll(ordersTask, treeTask, clientsTask);

            var orders = ordersTask.Result;
            if (orders == null || orders.Count == 0) { ShowToast("No hay ordenes disponibles", "warning"); return; }
            var used = treeTask.Result.Where(f => f.Linked_order_id.HasValue && f.Id != folder.Id).Select(f => f.Linked_order_id!.Value).ToHashSet();
            var avail = orders.Where(o => !used.Contains(o.Id)).ToList();
            if (avail.Count == 0) { ShowToast("Todas las ordenes ya estan vinculadas", "warning"); return; }
            var cn = clientsTask.Result?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();

            // Show order picker as flyout popup
            var sel = await ShowOrderPickerFlyout(avail, cn, folder.Name);
            if (sel == null) return;

            // Show R5 warning if applicable
            if (validation.WarningMessage != null)
            {
                if (!Confirm($"{validation.WarningMessage}\n\n¿Continuar?")) return;
            }
            await SafeLoad(async () => { if (await SupabaseService.Instance.LinkDriveFolderToOrder(folder.Id, sel.Id, _cts.Token)) { _orderInfoCache.Remove(sel.Id); ShowToast($"Vinculada a {sel.Po}", "success"); await LoadFolder(); } });
        }
        async Task Unlink(DriveFolderDb f) { await SafeLoad(async () => { if (await SupabaseService.Instance.UnlinkDriveFolder(f.Id, _cts.Token)) { ShowToast("Carpeta desvinculada", "success"); await LoadFolder(); } }); }
        async void LinkThisFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue || !_selectionOrderId.HasValue) return;
            var currentFolder = _breadcrumb.LastOrDefault();
            if (currentFolder?.LinkedOrderId.HasValue == true)
            {
                var oi = _orderInfoCache.TryGetValue(currentFolder.LinkedOrderId.Value, out var info) ? info.Po : $"#{currentFolder.LinkedOrderId}";
                ShowToast($"Esta carpeta ya esta vinculada a {oi}", "warning", 4000);
                return;
            }
            var validation = await SupabaseService.Instance.ValidateDriveFolderLink(_currentFolderId.Value, _cts.Token);
            if (!validation.CanLink) { ShowToast(validation.BlockReason ?? "No se puede vincular", "warning", 4000); return; }
            if (validation.WarningMessage != null)
            {
                if (!Confirm($"{validation.WarningMessage}\n\n¿Continuar?")) return;
            }
            await SafeLoad(async () => { if (await SupabaseService.Instance.LinkDriveFolderToOrder(_currentFolderId.Value, _selectionOrderId.Value, _cts.Token)) { ShowToast($"Vinculada a {_selectionOrderPo}", "success"); DialogResult = true; Close(); } });
        }
        void CancelSelection_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        /// <summary>
        /// Shows an order picker as a flyout popup anchored to the Drive window.
        /// Loads first 30 orders immediately, renders inline with search.
        /// Returns the selected order or null if cancelled.
        /// </summary>
        Task<OrderDb?> ShowOrderPickerFlyout(List<OrderDb> orders, Dictionary<int, string> cNames, string folderName = "")
        {
            var tcs = new TaskCompletionSource<OrderDb?>();
            var popup = new System.Windows.Controls.Primitives.Popup { StaysOpen = false, Placement = PlacementMode.Center, PlacementTarget = this, AllowsTransparency = true };

            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), Width = 420, Height = 480, ClipToBounds = true, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 24, ShadowDepth = 8, Opacity = 0.15 } };
            var root = new DockPanel { LastChildFill = true };

            // Header
            var headerBdr = new Border { Background = Background, Padding = new Thickness(16, 12, 16, 12), BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1) }; DockPanel.SetDock(headerBdr, Dock.Top);
            var hsp = new StackPanel();
            hsp.Children.Add(new TextBlock { Text = $"Vincular \"{Tr(folderName, 25)}\"", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary });
            hsp.Children.Add(new TextBlock { Text = $"{orders.Count} ordenes disponibles", FontSize = 11, Foreground = TextMuted, Margin = new Thickness(0, 2, 0, 0) });
            headerBdr.Child = hsp; root.Children.Add(headerBdr);

            // Search
            var searchBdr = new Border { Background = Brushes.White, Padding = new Thickness(12, 8, 12, 8), BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1) }; DockPanel.SetDock(searchBdr, Dock.Top);
            var searchGrid = new Grid();
            searchGrid.Children.Add(new TextBlock { Text = "\uE721", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = TextLight, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
            var searchPh = new TextBlock { Text = "Buscar OC, cliente...", FontSize = 12, Foreground = TextLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(22, 0, 0, 0), IsHitTestVisible = false };
            searchGrid.Children.Add(searchPh);
            var sb = new TextBox { FontSize = 12, Foreground = TextPrimary, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(22, 4, 8, 4) };
            sb.TextChanged += (s, e) => searchPh.Visibility = string.IsNullOrEmpty(sb.Text) ? Visibility.Visible : Visibility.Collapsed;
            searchGrid.Children.Add(sb);
            searchBdr.Child = searchGrid; root.Children.Add(searchBdr);

            // Order list
            var listSP = new StackPanel();
            var listScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = listSP, Padding = new Thickness(4) };
            root.Children.Add(listScroll);

            var items = orders.Select(o =>
            {
                var client = o.ClientId.HasValue && cNames.ContainsKey(o.ClientId.Value) ? cNames[o.ClientId.Value] : "";
                return new { Order = o, Po = o.Po ?? "Sin OC", Client = client, Desc = o.Description ?? "", SearchText = $"{o.Po} {client} {o.Description}".ToLowerInvariant() };
            }).ToList();

            void RenderItems(string filter = "")
            {
                listSP.Children.Clear();
                var filtered = string.IsNullOrEmpty(filter) ? items : items.Where(i => i.SearchText.Contains(filter)).ToList();
                var idx = 0;
                foreach (var item in filtered)
                {
                    var isStripe = idx++ % 2 == 1;
                    var row = new Border { Padding = new Thickness(12, 8, 12, 8), CornerRadius = new CornerRadius(6), Cursor = Cursors.Hand, Background = isStripe ? Background : Brushes.Transparent };
                    var rg = new Grid(); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var badge = new Border { Background = new SolidColorBrush(Color.FromRgb(0xDE, 0xEB, 0xFF)), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 3, 6, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                    badge.Child = new TextBlock { Text = item.Po, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Primary }; Grid.SetColumn(badge, 0); rg.Children.Add(badge);
                    var infoSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    if (!string.IsNullOrEmpty(item.Client)) infoSp.Children.Add(new TextBlock { Text = Tr(item.Client, 30), FontSize = 12, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis });
                    if (!string.IsNullOrEmpty(item.Desc)) infoSp.Children.Add(new TextBlock { Text = Tr(item.Desc, 40), FontSize = 11, Foreground = TextMuted, TextTrimming = TextTrimming.CharacterEllipsis });
                    Grid.SetColumn(infoSp, 1); rg.Children.Add(infoSp);
                    row.Child = rg;
                    var capturedItem = item; var baseBg = isStripe ? Background : Brushes.Transparent;
                    row.MouseEnter += (s, e) => { row.Background = ActiveBg; };
                    row.MouseLeave += (s, e) => { row.Background = baseBg; };
                    row.MouseLeftButtonDown += (s, e) =>
                    {
                        tcs.TrySetResult(capturedItem.Order);
                        popup.IsOpen = false;
                    };
                    listSP.Children.Add(row);
                }
                if (filtered.Count == 0)
                    listSP.Children.Add(new TextBlock { Text = "Sin resultados", FontSize = 12, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
            }

            RenderItems();
            sb.TextChanged += (s, e) => RenderItems(sb.Text?.Trim().ToLowerInvariant() ?? "");
            popup.Closed += (s, e) => tcs.TrySetResult(null);
            card.Child = root; popup.Child = card;
            popup.IsOpen = true;
            sb.Focus();
            return tcs.Task;
        }

        // ===============================================
        // EVENT HANDLERS
        // ===============================================
        void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }

        // P10: Search with debounce - scoped to current folder (global when at root)
        async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
            await Task.Delay(400);
            if ((SearchBox.Text?.Trim().ToLowerInvariant() ?? "") != q) return;
            if (string.IsNullOrEmpty(q)) { RenderContent(); SectionTitle.Text = _breadcrumb.LastOrDefault()?.Name ?? "IMA Drive"; SectionSubtitle.Text = ""; UpdateSearchPlaceholder(); return; }

            try
            {
                // Scoped search: current folder + descendants. Null = global (root).
                var (folderResults, fileResults) = await SupabaseService.Instance.SearchDriveInFolder(_currentFolderId, q, _cts.Token);
                var total = folderResults.Count + fileResults.Count;

                // Build folder path lookup for grouping
                Dictionary<int, (string Name, int? Parent_id)> nameMap;
                try
                {
                    var tree = await SupabaseService.Instance.GetDriveFolderTree(_cts.Token);
                    nameMap = new Dictionary<int, (string, int?)>();
                    foreach (var t in tree)
                        nameMap.TryAdd(t.Id, (t.Name, t.Parent_id));
                }
                catch { nameMap = new Dictionary<int, (string, int?)>(); }

                string BuildPath(int folderId)
                {
                    var parts = new List<string>();
                    var cur = folderId;
                    int safety = 20;
                    while (nameMap.TryGetValue(cur, out var info) && safety-- > 0)
                    {
                        parts.Insert(0, info.Name);
                        if (!info.Parent_id.HasValue) break;
                        cur = info.Parent_id.Value;
                    }
                    // Skip root folder name (e.g., "IMA MECATRONICA")
                    return parts.Count > 1 ? string.Join("  >  ", parts.Skip(1)) : (parts.FirstOrDefault() ?? "Raiz");
                }

                // Group: folders by parent_id, files by folder_id
                var groups = new Dictionary<int, (string path, List<DriveFolderDb> folders, List<DriveFileDb> files)>();
                foreach (var f in folderResults)
                {
                    var gid = f.ParentId ?? 0;
                    if (!groups.ContainsKey(gid)) groups[gid] = (BuildPath(gid), new(), new());
                    groups[gid].folders.Add(f);
                }
                foreach (var f in fileResults)
                {
                    var gid = f.FolderId;
                    if (!groups.ContainsKey(gid)) groups[gid] = (BuildPath(gid), new(), new());
                    groups[gid].files.Add(f);
                }

                // Render grouped results (no inner ScrollViewer — outer XAML ScrollViewer handles it)
                var sp = new StackPanel();
                foreach (var kvp in groups.OrderBy(g => g.Value.path))
                {
                    var gid = kvp.Key;
                    var (path, folders, files) = kvp.Value;

                    // Group header — clickable to navigate
                    var header = new Border { Background = Background, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 4, 0, 4), Cursor = Cursors.Hand };
                    var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
                    headerRow.Children.Add(new TextBlock { Text = "\uE8B7", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = Primary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                    headerRow.Children.Add(new TextBlock { Text = path, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center });
                    var countBadge = new Border { Background = new SolidColorBrush(Color.FromRgb(0xDE, 0xEB, 0xFF)), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    countBadge.Child = new TextBlock { Text = $"{folders.Count + files.Count}", FontSize = 11, Foreground = Primary, FontWeight = FontWeights.SemiBold };
                    headerRow.Children.Add(countBadge);
                    header.Child = headerRow;
                    var capturedGid = gid;
                    header.MouseEnter += (s, me2) => header.Background = ActiveBg;
                    header.MouseLeave += (s, me2) => header.Background = Background;
                    header.MouseLeftButtonDown += (s, me2) => { if (capturedGid > 0) _ = SafeLoad(() => NavTo(capturedGid)); };
                    sp.Children.Add(header);

                    // Items in this group (alternating stripes)
                    var rowIdx = 0;
                    foreach (var f in folders)
                    {
                        var capturedFolder = f;
                        var row = MkSearchResultRow("\uED41", f.Name, "Carpeta", null, Primary, rowIdx++ % 2 == 1);
                        row.MouseLeftButtonDown += (s, me2) => _ = SafeLoad(() => NavTo(capturedFolder.Id));
                        sp.Children.Add(row);
                    }
                    foreach (var f in files)
                    {
                        var capturedFile = f;
                        var (cH, bgH) = GFC(f.FileName); var fC = CH(cH);
                        var sizeText = Services.Drive.DriveService.FormatFileSize(f.FileSize);
                        var row = MkSearchResultRow(FIcon(f.FileName), f.FileName, sizeText, RelT(f.UploadedAt), new SolidColorBrush(fC), rowIdx++ % 2 == 1);
                        row.MouseLeftButtonDown += (s, me2) => { _ = SafeLoad(() => NavTo(capturedFile.FolderId)); };
                        sp.Children.Add(row);
                    }

                    // Separator line between groups
                    sp.Children.Add(new Border { Height = 2, Background = BorderColor, Margin = new Thickness(0, 8, 0, 8) });
                }

                ContentHost.Content = sp;
                var scopeName = _breadcrumb.LastOrDefault()?.Name;
                SectionTitle.Text = scopeName != null
                    ? $"Resultados: \"{q}\" en {scopeName}"
                    : $"Resultados: \"{q}\"";
                SectionSubtitle.Text = $"{folderResults.Count} carpeta(s), {fileResults.Count} archivo(s) en {groups.Count} ubicacion(es)";
                StatusText.Text = $"{total} resultado(s)";
                EmptyState.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DriveV2] Search error: {ex.Message}\n{ex.StackTrace}");
                StatusText.Text = "Error en busqueda";
                ShowToast($"Error buscando: {ex.Message}", "error", 4000);
            }
        }

        /// <summary>Search result row: icon + name + meta, hover highlight, hand cursor</summary>
        Border MkSearchResultRow(string icon, string name, string meta, string? date, Brush iconColor, bool stripe = false)
        {
            var baseBg = stripe ? Background : Brushes.Transparent;
            var row = new Border { Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand, Background = baseBg };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconTb = new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = iconColor, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(iconTb, 0); g.Children.Add(iconTb);

            var nameTb = new TextBlock { Text = name, FontSize = 13, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = name, Margin = new Thickness(4, 0, 12, 0) };
            Grid.SetColumn(nameTb, 1); g.Children.Add(nameTb);

            var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            metaPanel.Children.Add(new TextBlock { Text = meta, FontSize = 11, Foreground = TextMuted });
            if (date != null)
            {
                metaPanel.Children.Add(new Ellipse { Width = 3, Height = 3, Fill = SlateLight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
                metaPanel.Children.Add(new TextBlock { Text = date, FontSize = 11, Foreground = TextLight });
            }
            Grid.SetColumn(metaPanel, 2); g.Children.Add(metaPanel);

            row.Child = g;
            row.MouseEnter += (s, _) => { row.Background = ActiveBg; nameTb.Foreground = Primary; };
            row.MouseLeave += (s, _) => { row.Background = baseBg; nameTb.Foreground = TextPrimary; };
            return row;
        }

        void UpdateSearchPlaceholder()
        {
            var folderName = _breadcrumb.LastOrDefault()?.Name;
            SearchPlaceholder.Text = folderName != null
                ? $"Buscar en {folderName}..."
                : "Buscar archivos, carpetas y ordenes...";
        }

        void GridView_Click(object sender, RoutedEventArgs e) { _viewMode = _persistedViewMode = "grid"; UpdateViewToggle(); ClearMultiSelect(); RenderContent(); }
        void ListView_Click(object sender, RoutedEventArgs e) { _viewMode = _persistedViewMode = "list"; UpdateViewToggle(); ClearMultiSelect(); RenderContent(); }

        // ===============================================
        // MULTI-SELECT
        // ===============================================
        void ToggleFileSelect(DriveFileDb file)
        {
            if (_selectedFileIds.Contains(file.Id)) _selectedFileIds.Remove(file.Id);
            else _selectedFileIds.Add(file.Id);
            _selectedFile = _selectedFileIds.Count == 1 ? _currentFiles.FirstOrDefault(f => f.Id == _selectedFileIds.First()) : null;
            UpdateMultiSelectBar();
            RenderContent();
            if (_selectedFile != null) ShowDetail(_selectedFile);
            else HideDetail();
        }

        void UpdateMultiSelectBar()
        {
            if (_selectedFileIds.Count > 0)
            {
                MultiSelectBar.Visibility = Visibility.Visible;
                MultiSelectText.Text = $"{_selectedFileIds.Count} archivo{(_selectedFileIds.Count != 1 ? "s" : "")} seleccionado{(_selectedFileIds.Count != 1 ? "s" : "")}";
            }
            else MultiSelectBar.Visibility = Visibility.Collapsed;
        }

        void ClearMultiSelect() { _selectedFileIds.Clear(); _selectedFile = null; MultiSelectBar.Visibility = Visibility.Collapsed; }
        void MultiSelectClear_Click(object sender, RoutedEventArgs e) { ClearMultiSelect(); HideDetail(); RenderContent(); }

        async void MultiSelectDownload_Click(object sender, RoutedEventArgs e)
        {
            var files = _currentFiles.Where(f => _selectedFileIds.Contains(f.Id)).ToList();
            if (files.Count == 0) return;
            if (files.Count == 1) { await DlFile(files[0]); return; }
            // Pick destination folder via OpenFileDialog trick (select folder via fake file)
            var dlg = new OpenFileDialog { Title = $"Seleccionar carpeta destino para {files.Count} archivos", FileName = "Seleccionar esta carpeta", Filter = "Carpeta|*.", CheckFileExists = false, CheckPathExists = true };
            if (dlg.ShowDialog() != true) return;
            var folder = System.IO.Path.GetDirectoryName(dlg.FileName) ?? "";
            if (string.IsNullOrEmpty(folder)) return;
            int ok = 0, fail = 0;
            foreach (var file in files)
            {
                try
                {
                    var dest = System.IO.Path.Combine(folder, file.FileName);
                    StatusText.Text = $"Descargando {file.FileName}... ({ok + fail + 1}/{files.Count})";
                    if (await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, dest, _cts.Token)) ok++;
                    else fail++;
                }
                catch { fail++; }
            }
            StatusText.Text = fail > 0 ? $"{ok} descargado(s), {fail} fallido(s)" : $"{ok} archivo(s) descargado(s)";
        }

        async void MultiSelectDelete_Click(object sender, RoutedEventArgs e)
        {
            var files = _currentFiles.Where(f => _selectedFileIds.Contains(f.Id)).ToList();
            if (files.Count == 0) return;
            if (!Confirm($"Eliminar {files.Count} archivo(s)?")) return;
            int ok = 0;
            foreach (var file in files)
            {
                try
                {
                    if (await SupabaseService.Instance.DeleteDriveFile(file.Id, _cts.Token))
                    { ok++; if (_globalStorageBytes > 0) _globalStorageBytes -= file.FileSize ?? 0; }
                }
                catch (Exception ex) { Debug.WriteLine($"[DriveV2] Multi-delete err: {ex.Message}"); }
            }
            StatusText.Text = $"{ok} archivo(s) eliminado(s)";
            UpdateStorageUI(); ClearMultiSelect(); HideDetail(); InvalidateStats(); await SafeLoad(() => LoadFolder());
        }
        async void BackToFolders_Click(object sender, RoutedEventArgs e) { if (_breadcrumb.Count >= 2) await SafeLoad(() => NavTo(_breadcrumb[^2].Id)); else await SafeLoad(() => NavigateToRoot()); }
        void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
        void DetailClose_Click(object sender, RoutedEventArgs e) { HideDetail(); RenderContent(); }
        async void DetailDownload_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) await DlFile(_selectedFile); }
        // Copiar enlace removed - no requirement for it
        async void DetailDelete_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) await DelFile(_selectedFile); }
        void Window_DragEnter(object sender, DragEventArgs e) { if (_currentFolderId.HasValue && e.Data.GetDataPresent(DataFormats.FileDrop)) DragDropOverlay.Visibility = Visibility.Visible; }
        void Window_DragLeave(object sender, DragEventArgs e) => DragDropOverlay.Visibility = Visibility.Collapsed;
        void Window_DragOver(object sender, DragEventArgs e) => e.Handled = true;
        async void Window_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed; e.Handled = true;
            if (!_currentFolderId.HasValue || !e.Data.GetDataPresent(DataFormats.FileDrop) || !SupabaseService.Instance.IsDriveStorageConfigured) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length == 0) return;
            await UploadFiles(files);
        }

        // Upload with ghost cards in-place (no side panel)
        async Task UploadFiles(string[] filePaths)
        {
            if (!_currentFolderId.HasValue) return;
            // CRITICAL: Capture folder ID at start - user may navigate away during upload
            var targetFolderId = _currentFolderId.Value;
            Debug.WriteLine($"[DriveV2] UploadFiles: {filePaths.Length} files to folder {targetFolderId}");

            // Insert ghost elements into current view
            var ghostElements = new Dictionary<string, UIElement>();
            if (_viewMode == "list")
            {
                // Ensure list structure exists (may be missing if folder was empty)
                StackPanel? stk = null;
                if (ContentHost.Content is Border listWrap && listWrap.Child is StackPanel existingStk)
                    stk = existingStk;
                else
                {
                    // Create minimal list structure with header
                    RenderList(_activeFilter != null ? ApplyFileFilter(_currentFiles, _activeFilter) : _currentFiles);
                    if (ContentHost.Content is Border newWrap && newWrap.Child is StackPanel newStk)
                        stk = newStk;
                }
                if (stk != null)
                {
                    foreach (var fp in filePaths)
                    {
                        var fn = System.IO.Path.GetFileName(fp);
                        long fSz = 0; try { fSz = new System.IO.FileInfo(fp).Length; } catch { }
                        var ghostRow = MkGhostListRow(fn, fSz);
                        stk.Children.Add(ghostRow);
                        ghostElements[fp] = ghostRow;
                    }
                }
                Debug.WriteLine($"[DriveV2] Ghost list rows inserted: {ghostElements.Count}");
            }
            else
            {
                // Grid: find or create the file WrapPanel inside the StackPanel structure
                WrapPanel? wp = null;
                if (ContentHost.Content is StackPanel gridStk)
                    wp = gridStk.Children.OfType<WrapPanel>().LastOrDefault(); // last WrapPanel is the files section
                else if (ContentHost.Content is WrapPanel directWp)
                    wp = directWp;
                if (wp == null)
                {
                    RenderWrap(SortFiles(_activeFilter != null ? ApplyFileFilter(_currentFiles, _activeFilter) : _currentFiles));
                    if (ContentHost.Content is StackPanel newStk)
                        wp = newStk.Children.OfType<WrapPanel>().LastOrDefault();
                    else if (ContentHost.Content is WrapPanel newWp)
                        wp = newWp;
                }
                if (wp != null)
                {
                    foreach (var fp in filePaths)
                    {
                        var fn = System.IO.Path.GetFileName(fp);
                        long fSz = 0; try { fSz = new System.IO.FileInfo(fp).Length; } catch { }
                        var ghost = MkGhostFileCard(fn, fSz);
                        wp.Children.Add(ghost);
                        ghostElements[fp] = ghost;
                    }
                }
                Debug.WriteLine($"[DriveV2] Ghost grid cards inserted: {ghostElements.Count}");
            }
            // Update subtitle count
            var ghostCount = _currentFiles.Count + filePaths.Length;
            SectionSubtitle.Text = $"{_currentFolders.Count} carpeta{(_currentFolders.Count != 1 ? "s" : "")} - {ghostCount} archivo{(ghostCount != 1 ? "s" : "")}";
            StatusText.Text = $"Subiendo {filePaths.Length} archivo(s)...";
            EmptyState.Visibility = Visibility.Collapsed;

            // Parallel upload with captured targetFolderId
            int ok = 0, fail = 0;
            var semaphore = new SemaphoreSlim(3);
            var uploadTasks = filePaths.Select(async fp =>
            {
                await semaphore.WaitAsync(_cts.Token);
                try
                {
                    var fn = System.IO.Path.GetFileName(fp);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ghostElements.TryGetValue(fp, out var ghost))
                            SetGhostState(ghost, "uploading");
                    });
                    Debug.WriteLine($"[DriveV2] Uploading: {fn} -> folder {targetFolderId}");
                    await SupabaseService.Instance.UploadDriveFile(fp, targetFolderId, _currentUser.Id, _cts.Token);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ghostElements.TryGetValue(fp, out var ghost))
                            SetGhostState(ghost, "done");
                    });
                    Debug.WriteLine($"[DriveV2] Uploaded OK: {fn}");
                    Interlocked.Increment(ref ok);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DriveV2] Upload ERR: {System.IO.Path.GetFileName(fp)} - {ex.Message}");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ghostElements.TryGetValue(fp, out var ghost))
                            SetGhostState(ghost, "error");
                    });
                    Interlocked.Increment(ref fail);
                }
                finally { semaphore.Release(); }
            });
            await Task.WhenAll(uploadTasks);
            StatusText.Text = fail > 0 ? $"{ok} subido(s), {fail} fallido(s)" : $"{ok} archivo(s) subido(s)";
            InvalidateStats();
            if (_globalStorageBytes >= 0)
                foreach (var fp in filePaths)
                    try { _globalStorageBytes += new System.IO.FileInfo(fp).Length; } catch { }
            UpdateStorageUI();
            // Only reload if still viewing the target folder
            if (_currentFolderId == targetFolderId)
                await SafeLoad(() => LoadFolder());
        }

        /// <summary>Ghost file card for grid view</summary>
        Border MkGhostFileCard(string fileName, long fileSize)
        {
            var (cH, bH) = GFC(fileName); var fC = CH(cH); var fB = new SolidColorBrush(fC); var bgB = BH(bH);
            var ext = System.IO.Path.GetExtension(fileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = Primary, BorderThickness = new Thickness(2), Width = 220, Margin = new Thickness(6), ClipToBounds = true, Opacity = 0.7, Tag = "ghost" };
            var mg = new Grid(); mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // Preview area with progress overlay
            var prev = new Border { Background = bgB, CornerRadius = new CornerRadius(10, 10, 0, 0) };
            var pg = new Grid();
            pg.Children.Add(new TextBlock { Text = FIcon(fileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 48, Foreground = fB, Opacity = 0.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var badge = new Border { Background = fB, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0) };
            badge.Child = new TextBlock { Text = ext, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }; pg.Children.Add(badge);
            pg.Children.Add(new ProgressBar { Height = 4, IsIndeterminate = true, Foreground = Primary, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Bottom, Tag = "ghostPb" });
            prev.Child = pg; Grid.SetRow(prev, 0); mg.Children.Add(prev);
            // Info
            var ip = new StackPanel { Margin = new Thickness(16) };
            ip.Children.Add(new TextBlock { Text = fileName, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8), ToolTip = fileName });
            var sg = new Grid();
            sg.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(fileSize), FontSize = 11, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Left });
            sg.Children.Add(new TextBlock { Text = "En cola...", FontSize = 11, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Right, Tag = "ghostStatus" });
            ip.Children.Add(sg); Grid.SetRow(ip, 1); mg.Children.Add(ip);
            card.Child = mg;
            return card;
        }

        /// <summary>Ghost file row for list view</summary>
        Border MkGhostListRow(string fileName, long fileSize)
        {
            var (cH, _) = GFC(fileName); var fC = CH(cH); var fB = new SolidColorBrush(fC);
            var ext = System.IO.Path.GetExtension(fileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            var rg = new Grid { Height = 48, Background = new SolidColorBrush(Color.FromArgb(15, 0x1D, 0x4E, 0xD8)) };
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
            var icB = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(25, fC.R, fC.G, fC.B)), Margin = new Thickness(0, 0, 10, 0) };
            icB.Child = new TextBlock { Text = FIcon(fileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = fB, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            np.Children.Add(icB);
            np.Children.Add(new TextBlock { Text = fileName, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 400, Opacity = 0.7 });
            Grid.SetColumn(np, 0); rg.Children.Add(np);
            var extBadge = new Border { Background = new SolidColorBrush(Color.FromArgb(25, fC.R, fC.G, fC.B)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            extBadge.Child = new TextBlock { Text = ext, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = fB }; Grid.SetColumn(extBadge, 1); rg.Children.Add(extBadge);
            AddCol(rg, 2, Services.Drive.DriveService.FormatFileSize(fileSize));
            var statusTb = new TextBlock { Text = "En cola...", FontSize = 12, Foreground = Primary, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Tag = "ghostStatus" };
            Grid.SetColumn(statusTb, 3); rg.Children.Add(statusTb);
            AddCol(rg, 4, "ahora");
            // Progress bar at bottom
            var pb = new ProgressBar { Height = 3, IsIndeterminate = true, Foreground = Primary, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Bottom, Tag = "ghostPb" };
            Grid.SetRow(pb, 0); rg.Children.Add(pb);
            var rb = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1), Child = rg, Tag = "ghost", Opacity = 0.8 };
            return rb;
        }

        /// <summary>Update ghost element visual state (works for both grid cards and list rows)</summary>
        void SetGhostState(UIElement element, string state)
        {
            // Find all tagged elements recursively
            var statusTb = FindByTag<TextBlock>(element, "ghostStatus");
            var progressBar = FindByTag<ProgressBar>(element, "ghostPb");
            var border = element as Border;

            switch (state)
            {
                case "uploading":
                    if (border != null) border.Opacity = 0.85;
                    if (statusTb != null) { statusTb.Text = "Subiendo..."; statusTb.Foreground = Primary; }
                    break;
                case "done":
                    if (border != null) { border.Opacity = 1.0; border.BorderBrush = GreenOk; }
                    if (progressBar != null) { progressBar.IsIndeterminate = false; progressBar.Value = 100; progressBar.Foreground = GreenOk; }
                    if (statusTb != null) { statusTb.Text = "\u2713 Listo"; statusTb.Foreground = GreenOk; }
                    break;
                case "error":
                    if (border != null) { border.Opacity = 0.6; border.BorderBrush = Destructive; }
                    if (progressBar != null) { progressBar.IsIndeterminate = false; progressBar.Value = 100; progressBar.Foreground = Destructive; }
                    if (statusTb != null) { statusTb.Text = "Error"; statusTb.Foreground = Destructive; }
                    break;
            }
        }

        static T? FindByTag<T>(DependencyObject parent, string tag) where T : FrameworkElement
        {
            if (parent is T fe && fe.Tag as string == tag) return fe;
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindByTag<T>(child, tag);
                if (result != null) return result;
            }
            // Also check logical tree for non-visual elements
            if (parent is Panel panel)
                foreach (UIElement child in panel.Children)
                { var r = FindByTag<T>(child, tag); if (r != null) return r; }
            if (parent is Border b && b.Child != null)
            { var r = FindByTag<T>(b.Child, tag); if (r != null) return r; }
            if (parent is ContentControl cc && cc.Content is DependencyObject co)
            { var r = FindByTag<T>(co, tag); if (r != null) return r; }
            return null;
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Don't navigate if inline folder textbox is handling Escape
                if (_isCreatingFolder) { base.OnKeyDown(e); return; }
                if (_selectedFileIds.Count > 0) { ClearMultiSelect(); HideDetail(); RenderContent(); }
                else if (_selectedFile != null) { HideDetail(); RenderContent(); }
                else if (_breadcrumb.Count > 1) BackToFolders_Click(this, new RoutedEventArgs());
                else Close();
            }
            base.OnKeyDown(e);
        }
        async void OnMouseNav(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.XButton1) { e.Handled = true; await SafeLoad(() => NavBack()); } else if (e.ChangedButton == MouseButton.XButton2) { e.Handled = true; await SafeLoad(() => NavFwd()); } }

        // ===============================================
        // HELPERS
        // ===============================================
        async Task SafeLoad(Func<Task> a) { try { await a(); } catch (OperationCanceledException) { } catch (Exception ex) { Debug.WriteLine($"[DriveV2] ERR: {ex.Message}"); StatusText.Text = $"Error: {ex.Message}"; } }
        async Task<string> ResolveUser(int? uid) { if (!uid.HasValue) return "-"; if (_userNameCache.TryGetValue(uid.Value, out var c)) return c; try { var u = await SupabaseService.Instance.GetUserById(uid.Value); var n = u?.FullName ?? u?.Username ?? $"#{uid}"; _userNameCache[uid.Value] = n; return n; } catch { return $"#{uid}"; } }
        string OrderTxt(int? oid) { if (!oid.HasValue) return "-"; if (!_orderInfoCache.TryGetValue(oid.Value, out var oi)) return $"#{oid}"; return !string.IsNullOrEmpty(oi.Client) ? $"{oi.Po} | {Tr(oi.Client, 16)}" : oi.Po; }
        private static readonly System.Windows.Media.Imaging.BitmapImage _folderPinBmp = new(new Uri("pack://application:,,,/ico-ima/folder_pin.png"));
        static UIElement MkFolderIco(double sz, Brush fill) => new System.Windows.Controls.Image { Source = _folderPinBmp, Width = sz, Height = sz, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        // P7: Modern prompt dialog
        static string Prompt(string title, string label, string def)
        {
            var w = new Window { Title = title, Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent };
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 24, ShadowDepth = 8, Opacity = 0.12 }, Margin = new Thickness(16) };
            var p = new StackPanel { Margin = new Thickness(24) };
            p.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)), Margin = new Thickness(0, 0, 0, 16) });
            p.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), Margin = new Thickness(0, 0, 0, 8) });
            var tbBdr = new Border { CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true };
            var tb = new TextBox { Text = def, FontSize = 14, Padding = new Thickness(12, 10, 12, 10), BorderThickness = new Thickness(0) }; tb.SelectAll(); tbBdr.Child = tb; p.Children.Add(tbBdr);
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) }; string? res = null;
            var cancel = new Button { Content = "Cancelar", Width = 90, Padding = new Thickness(0, 8, 0, 8), Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)), Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), FontWeight = FontWeights.Medium, Cursor = Cursors.Hand, IsCancel = true };
            var ok = new Button { Content = "Aceptar", Width = 90, Padding = new Thickness(0, 8, 0, 8), Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) };
            ok.Click += (s, e) => { res = tb.Text; w.Close(); };
            bp.Children.Add(cancel); bp.Children.Add(ok); p.Children.Add(bp);
            card.Child = p; w.Content = card; w.Loaded += (s, e) => tb.Focus(); w.MouseLeftButtonDown += (s, e) => { try { w.DragMove(); } catch { } }; w.ShowDialog(); return res ?? "";
        }

        // ===============================================
        // TOAST NOTIFICATIONS (replaces MessageBox)
        // ===============================================
        private System.Windows.Threading.DispatcherTimer? _toastTimer;

        void ShowToast(string message, string type = "info", int durationMs = 3000)
        {
            Dispatcher.Invoke(() =>
            {
                // Icon + color by type
                var (icon, bg) = type switch
                {
                    "success" => ("\uE73E", Color.FromRgb(0x16, 0x65, 0x34)),  // green
                    "error" => ("\uEA39", Color.FromRgb(0x99, 0x1B, 0x1B)),
                    "warning" => ("\uE7BA", Color.FromRgb(0x92, 0x40, 0x0E)),  // amber
                    _ => ("\uE946", Color.FromRgb(0x0F, 0x17, 0x2A))  // info dark
                };
                ToastIcon.Text = icon;
                ToastText.Text = message;
                ToastInner.Background = new SolidColorBrush(bg);
                ToastPanel.Visibility = Visibility.Visible;
                ToastPanel.Opacity = 0;

                // Fade in
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                ToastPanel.BeginAnimation(OpacityProperty, fadeIn);

                // Auto-dismiss
                _toastTimer?.Stop();
                _toastTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer.Stop();
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                    fadeOut.Completed += (s2, e2) => ToastPanel.Visibility = Visibility.Collapsed;
                    ToastPanel.BeginAnimation(OpacityProperty, fadeOut);
                };
                _toastTimer.Start();
            });
        }

        /// <summary>Inline confirmation dialog (replaces MessageBox.YesNo). Returns true if confirmed.</summary>
        bool Confirm(string message, string title = "Confirmar")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        static string Tr(string? v, int m) => string.IsNullOrEmpty(v) ? "" : v.Length <= m ? v : v[..m] + "...";
        static MenuItem MI(string h, RoutedEventHandler hnd) { var m = new MenuItem { Header = h }; m.Click += hnd; return m; }
        static Color CH(string h) { h = h.TrimStart('#'); return Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16)); }
        static SolidColorBrush BH(string h) => new(CH(h));

        // ===============================================
        // BENCHMARK
        // ===============================================
        record BenchmarkPhaseResult(long P1_DataMs, long P2_OrdersMs, long P3_RenderMs, long TotalMs,
            int FolderCount, int FileCount, int OrderQueriesCount);

        record BenchmarkEntry(string Scenario, string FolderName, int? FolderId, int Iteration,
            long P1_DataMs, long P2_OrdersMs, long P3_RenderMs, long TotalMs,
            int Folders, int Files, int OrderQueries, bool CacheWarm);

        void ClearAllCaches()
        {
            _statsCache.Clear();
            _orderInfoCache.Clear();
            _userNameCache.Clear();
            _folderCache.Clear();
            // Also clear ServiceCache (clients, etc.)
            var cacheField = typeof(Services.Core.BaseSupabaseService)
                .GetField("Cache", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var cache = cacheField?.GetValue(null) as Services.Core.ServiceCache;
            cache?.Clear();
        }

        async void RunBenchmark_Click(object sender, MouseButtonEventArgs e)
        {
            const int ITERATIONS = 3;
            const int MAX_DEPTH = 3;

            if (!Confirm($"Ejecutar benchmark de rendimiento:\n\n" +
                $"- {ITERATIONS} iteraciones por escenario\n" +
                $"- Profundidad maxima: {MAX_DEPTH} niveles\n" +
                $"- Escenarios: cold, warm, cache-hit\n" +
                $"- Recorre TODO el arbol de carpetas\n\n" +
                $"Al terminar copia el reporte al portapapeles.\nContinuar?"))
                return;

            _benchmarkActive = true;
            var results = new List<BenchmarkEntry>();
            var totalSw = Stopwatch.StartNew();

            try
            {
                // Phase 0: Discover full tree structure
                ClearAllCaches();
                StatusText.Text = "Benchmark: descubriendo arbol de carpetas...";
                var rootFolders = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token);
                var rootId = rootFolders.FirstOrDefault()?.Id;
                if (rootId == null) { ShowToast("No hay carpeta raiz", "error"); return; }

                var allFolders = new List<(int id, string name, int depth, string path)>();
                await DiscoverTree(rootId.Value, "IMA MECATRONICA", 0, MAX_DEPTH, allFolders);
                StatusText.Text = $"Benchmark: {allFolders.Count} carpetas encontradas, {ITERATIONS} iteraciones...";

                for (int i = 0; i < ITERATIONS; i++)
                {
                    // === COLD: every folder (cache cleared) ===
                    foreach (var (fId, fName, depth, path) in allFolders)
                    {
                        ClearAllCaches();
                        await Task.Delay(30);
                        _lastPhaseResult = null;
                        await NavTo(fId, hist: false);
                        if (_lastPhaseResult != null)
                            results.Add(new BenchmarkEntry($"L{depth} cold", fName, fId, i + 1,
                                _lastPhaseResult.P1_DataMs, _lastPhaseResult.P2_OrdersMs, _lastPhaseResult.P3_RenderMs,
                                _lastPhaseResult.TotalMs, _lastPhaseResult.FolderCount, _lastPhaseResult.FileCount,
                                _lastPhaseResult.OrderQueriesCount, false));

                        // WARM (network): same folder, caches populated but _folderCache skipped by benchmark
                        _lastPhaseResult = null;
                        await NavTo(fId, hist: false);
                        if (_lastPhaseResult != null)
                            results.Add(new BenchmarkEntry($"L{depth} warm", fName, fId, i + 1,
                                _lastPhaseResult.P1_DataMs, _lastPhaseResult.P2_OrdersMs, _lastPhaseResult.P3_RenderMs,
                                _lastPhaseResult.TotalMs, _lastPhaseResult.FolderCount, _lastPhaseResult.FileCount,
                                _lastPhaseResult.OrderQueriesCount, true));
                    }

                    // === CACHE HIT: first populate cache for ALL folders, then measure ===
                    // Step 1: Navigate all folders to populate _folderCache (not measured)
                    _benchmarkActive = false;
                    foreach (var (fId, _, _, _) in allFolders)
                        await NavTo(fId, hist: false);

                    // Step 2: Now measure cache hits (all folders should be in _folderCache)
                    var cacheResults = new List<(int fId, string name, long ms)>();
                    foreach (var (fId, fName, depth, path) in allFolders)
                    {
                        var csw = Stopwatch.StartNew();
                        await NavTo(fId, hist: false);
                        csw.Stop();
                        cacheResults.Add((fId, fName, csw.ElapsedMilliseconds));
                    }
                    _benchmarkActive = true;

                    foreach (var (fId, fName, ms) in cacheResults)
                    {
                        var depth = allFolders.First(f => f.id == fId).depth;
                        results.Add(new BenchmarkEntry($"L{depth} cache-hit", fName, fId, i + 1,
                            0, 0, ms, ms, 0, 0, 0, true));
                    }

                    // === BACK/FORWARD stress: navigate forward then rapid back ===
                    _benchmarkActive = false;
                    var navOrder = allFolders.Take(Math.Min(10, allFolders.Count)).ToList();
                    foreach (var (fId, _, _, _) in navOrder)
                        await NavTo(fId, hist: true);

                    // Rapid back navigation (should all be cache hits)
                    var backSw = Stopwatch.StartNew();
                    int backCount = 0;
                    while (_backHistory.Count > 0)
                    {
                        await NavTo(_backHistory.Pop(), hist: false);
                        backCount++;
                    }
                    backSw.Stop();
                    _benchmarkActive = true;
                    if (backCount > 0)
                        results.Add(new BenchmarkEntry("Back nav (avg)", $"{backCount}x back", null, i + 1,
                            0, 0, backSw.ElapsedMilliseconds / backCount, backSw.ElapsedMilliseconds / backCount,
                            backCount, 0, 0, true));

                    StatusText.Text = $"Benchmark: iteracion {i + 1}/{ITERATIONS} completada ({allFolders.Count} carpetas)";
                }

                totalSw.Stop();

                // Generate report
                var report = GenerateBenchmarkReport(results, ITERATIONS, allFolders.Count, totalSw.ElapsedMilliseconds);
                Clipboard.SetText(report);
                StatusText.Text = $"Benchmark completado en {totalSw.ElapsedMilliseconds:N0}ms - Reporte copiado al portapapeles";

                var coldAvg = results.Where(r => r.Scenario.Contains("cold")).Select(r => r.TotalMs).DefaultIfEmpty(0).Average();
                var cacheAvg = results.Where(r => r.Scenario.Contains("cache-hit")).Select(r => r.TotalMs).DefaultIfEmpty(0).Average();

                ShowToast($"Benchmark completado: {allFolders.Count} carpetas, cold={coldAvg:F0}ms, cache={cacheAvg:F0}ms. Reporte copiado.", "success", 5000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Benchmark] ERR: {ex.Message}");
                StatusText.Text = $"Benchmark error: {ex.Message}";
                ShowToast($"Error en benchmark: {ex.Message}", "error", 5000);
            }
            finally
            {
                _benchmarkActive = false;
                ClearAllCaches();
                await SafeLoad(() => NavigateToRoot());
            }
        }

        async Task DiscoverTree(int parentId, string parentName, int depth, int maxDepth,
            List<(int id, string name, int depth, string path)> result)
        {
            result.Add((parentId, parentName, depth, parentName));
            if (depth >= maxDepth) return;
            var children = await SupabaseService.Instance.GetDriveChildFolders(parentId, _cts.Token);
            foreach (var c in children)
                await DiscoverTree(c.Id, c.Name, depth + 1, maxDepth, result);
        }

        string GenerateBenchmarkReport(List<BenchmarkEntry> results, int iterations, int folderCount, long totalMs)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Drive V2 - Benchmark de Rendimiento");
            sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Iteraciones: {iterations}");
            sb.AppendLine($"Carpetas testeadas: {folderCount} (arbol completo)");
            sb.AppendLine($"Total mediciones: {results.Count}");
            sb.AppendLine($"Tiempo total benchmark: {totalMs:N0}ms");
            sb.AppendLine();

            // Summary table by scenario
            sb.AppendLine("## Resumen por Escenario (promedios en ms)");
            sb.AppendLine();
            sb.AppendLine("| Escenario | N | P1 Data | P2 Orders | P3/Total | **TOTAL** | Carpetas | Archivos |");
            sb.AppendLine("|-----------|---|---------|-----------|----------|-----------|----------|----------|");

            foreach (var grp in results.GroupBy(r => r.Scenario).OrderBy(g => g.Key))
            {
                var items = grp.ToList();
                sb.AppendLine($"| {grp.Key} | {items.Count} " +
                    $"| {items.Average(r => r.P1_DataMs):F0} " +
                    $"| {items.Average(r => r.P2_OrdersMs):F0} " +
                    $"| {items.Average(r => r.P3_RenderMs):F0} " +
                    $"| **{items.Average(r => r.TotalMs):F0}** " +
                    $"| {items.Average(r => r.Folders):F0} " +
                    $"| {items.Average(r => r.Files):F0} |");
            }

            sb.AppendLine();

            // Cold vs Warm vs Cache-hit
            sb.AppendLine("## Comparativa: Cold vs Warm vs Cache-hit");
            sb.AppendLine();
            var cold = results.Where(r => r.Scenario.Contains("cold")).ToList();
            var warm = results.Where(r => r.Scenario.Contains("warm")).ToList();
            var cached = results.Where(r => r.Scenario.Contains("cache-hit")).ToList();
            var back = results.Where(r => r.Scenario.Contains("Back")).ToList();

            if (cold.Count > 0)
            {
                var coldAvg = cold.Average(r => r.TotalMs);
                var warmAvg = warm.Count > 0 ? warm.Average(r => r.TotalMs) : 0;
                var cacheAvg = cached.Count > 0 ? cached.Average(r => r.TotalMs) : 0;
                var backAvg = back.Count > 0 ? back.Average(r => r.TotalMs) : 0;

                sb.AppendLine("| Tipo | Promedio (ms) | vs Cold |");
                sb.AppendLine("|------|---------------|---------|");
                sb.AppendLine($"| Cold (sin cache, HTTP) | **{coldAvg:F0}** | - |");
                if (warm.Count > 0) sb.AppendLine($"| Warm (caches parciales) | **{warmAvg:F0}** | {(coldAvg > 0 ? (1 - warmAvg / coldAvg) * 100 : 0):F1}% |");
                if (cached.Count > 0) sb.AppendLine($"| Cache-hit (instantaneo) | **{cacheAvg:F0}** | {(coldAvg > 0 ? (1 - cacheAvg / coldAvg) * 100 : 0):F1}% |");
                if (back.Count > 0) sb.AppendLine($"| Back nav (promedio) | **{backAvg:F0}** | {(coldAvg > 0 ? (1 - backAvg / coldAvg) * 100 : 0):F1}% |");
            }

            sb.AppendLine();

            // Phase breakdown (cold only)
            if (cold.Count > 0)
            {
                sb.AppendLine("## Desglose por Fase (cold)");
                sb.AppendLine();
                var p1 = cold.Average(r => r.P1_DataMs);
                var p2 = cold.Average(r => r.P2_OrdersMs);
                var p3 = cold.Average(r => r.P3_RenderMs);
                var tot = cold.Average(r => r.TotalMs);
                sb.AppendLine($"| Fase | Promedio | % del total |");
                sb.AppendLine($"|------|---------|-------------|");
                sb.AppendLine($"| P1 Data (BD+RPC) | {p1:F0}ms | {(tot > 0 ? p1 / tot * 100 : 0):F1}% |");
                sb.AppendLine($"| P2 Orders (batch) | {p2:F0}ms | {(tot > 0 ? p2 / tot * 100 : 0):F1}% |");
                sb.AppendLine($"| P3 Render (UI) | {p3:F0}ms | {(tot > 0 ? p3 / tot * 100 : 0):F1}% |");
                sb.AppendLine($"| Overhead (breadcrumb+nav) | {tot - p1 - p2 - p3:F0}ms | {(tot > 0 ? (tot - p1 - p2 - p3) / tot * 100 : 0):F1}% |");
                sb.AppendLine();
            }

            // Per-level summary
            sb.AppendLine("## Rendimiento por Nivel de Profundidad (cold)");
            sb.AppendLine();
            sb.AppendLine("| Nivel | Carpetas | P1 avg | TOTAL avg | Min | Max |");
            sb.AppendLine("|-------|----------|--------|-----------|-----|-----|");
            foreach (var grp in cold.GroupBy(r => r.Scenario).OrderBy(g => g.Key))
            {
                var items = grp.ToList();
                sb.AppendLine($"| {grp.Key} | {items.Count / iterations} " +
                    $"| {items.Average(r => r.P1_DataMs):F0} " +
                    $"| **{items.Average(r => r.TotalMs):F0}** " +
                    $"| {items.Min(r => r.TotalMs)} " +
                    $"| {items.Max(r => r.TotalMs)} |");
            }
            sb.AppendLine();

            // Per-folder detail (cold only, top 20 slowest)
            sb.AppendLine("## Top 20 Carpetas mas Lentas (cold, promedio)");
            sb.AppendLine();
            sb.AppendLine("| Carpeta | P1 Data | P2 Orders | P3 Render | TOTAL | Carpetas | Archivos |");
            sb.AppendLine("|---------|---------|-----------|-----------|-------|----------|----------|");

            foreach (var grp in cold.GroupBy(r => r.FolderName)
                .Select(g => new { Name = g.Key, Items = g.ToList(), Avg = g.Average(r => r.TotalMs) })
                .OrderByDescending(g => g.Avg).Take(20))
            {
                sb.AppendLine($"| {Tr(grp.Name, 25)} " +
                    $"| {grp.Items.Average(r => r.P1_DataMs):F0} " +
                    $"| {grp.Items.Average(r => r.P2_OrdersMs):F0} " +
                    $"| {grp.Items.Average(r => r.P3_RenderMs):F0} " +
                    $"| **{grp.Avg:F0}** " +
                    $"| {grp.Items.Average(r => r.Folders):F0} " +
                    $"| {grp.Items.Average(r => r.Files):F0} |");
            }

            sb.AppendLine();

            // Percentiles
            if (cold.Count >= 5)
            {
                sb.AppendLine("## Percentiles (cold, TOTAL ms)");
                sb.AppendLine();
                var sorted = cold.Select(r => r.TotalMs).OrderBy(x => x).ToList();
                sb.AppendLine($"- P50 (mediana): **{sorted[sorted.Count / 2]}ms**");
                sb.AppendLine($"- P75: **{sorted[(int)(sorted.Count * 0.75)]}ms**");
                sb.AppendLine($"- P90: **{sorted[(int)(sorted.Count * 0.90)]}ms**");
                sb.AppendLine($"- P95: **{sorted[(int)(sorted.Count * 0.95)]}ms**");
                sb.AppendLine($"- P99: **{sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.99))]}ms**");
                sb.AppendLine();
            }

            // Raw data (abbreviated - only first iteration)
            sb.AppendLine("## Datos Crudos (iteracion 1)");
            sb.AppendLine();
            sb.AppendLine("| # | Escenario | Carpeta | P1 | P2 | P3 | Total | F | A | OQ |");
            sb.AppendLine("|---|-----------|---------|----|----|-------|-------|---|---|----|");
            int idx = 0;
            foreach (var r in results.Where(r => r.Iteration == 1))
            {
                idx++;
                sb.AppendLine($"| {idx} | {r.Scenario} | {Tr(r.FolderName, 18)} " +
                    $"| {r.P1_DataMs} | {r.P2_OrdersMs} | {r.P3_RenderMs} | **{r.TotalMs}** " +
                    $"| {r.Folders} | {r.Files} | {r.OrderQueries} |");
            }

            return sb.ToString();
        }
    }

    static class DriveV2Ext { public static T Also<T>(this T o, Action<T> a) { a(o); return o; } }
}
