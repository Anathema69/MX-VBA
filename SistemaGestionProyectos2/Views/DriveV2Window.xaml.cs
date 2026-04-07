using Microsoft.Win32;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.IO.Compression;
using SistemaGestionProyectos2.Models.DTOs;
using SistemaGestionProyectos2.Services.Drive;
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
        private readonly HashSet<int> _selectedFileIds = new(); // Multi-select
        private readonly List<Border> _navItems = new();
        private readonly List<Border> _filterItems = new(); // BUG-3: filter sidebar items
        private StackPanel? _cadSubPanel; // MEJORA-4: collapsible CAD sub-filters
        private bool _isDragging; // Prevents visual tree rebuild during active drag-drop

        // Caches
        private readonly Dictionary<int, string> _userNameCache = new();
        private readonly Dictionary<int, (int fileCount, int subCount, long totalSize)> _statsCache = new();
        private readonly Dictionary<int, (string Po, string Client, string Detail)> _orderInfoCache = new();

        // V3-B: Recientes & Actividad
        private bool _recentShowAll = false; // false = mis recientes, true = todos
        private bool _activityExpanded = true;

        // V3-C: Clipboard for Cut/Copy/Paste
        private enum ClipOp { Cut, Copy }
        private List<DriveFileDb>? _clipFiles;
        private ClipOp _clipOp;

        // V3-A: Preview & Thumbnails
        private readonly Dictionary<int, BitmapImage> _previewCache = new();
        private readonly LinkedList<int> _previewLru = new();
        private const int MaxPreviewCache = 20;
        private readonly SemaphoreSlim _thumbnailSemaphore = new(5);
        private int _overlayCurrentIndex = -1;
        private List<DriveFileDb> _overlayImageFiles = new();
        private static readonly string ThumbnailCacheDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IMA-Drive", "thumbs");

        // V3-E: Open-in-Place sync states
        private readonly Dictionary<int, SyncState> _syncStates = new();

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
        private static readonly SolidColorBrush Destructive = Fr(0xEF, 0x44, 0x44);
        // Semantic tokens (Tailwind)
        private static readonly SolidColorBrush Success = Fr(0x10, 0xB9, 0x81);
        private static readonly SolidColorBrush SuccessBg = Fr(0xF0, 0xFD, 0xF4);
        private static readonly SolidColorBrush Warning = Fr(0xF5, 0x9E, 0x0B);
        private static readonly SolidColorBrush WarningBg = Fr(0xFF, 0xFB, 0xEB);
        private static readonly SolidColorBrush Danger = Fr(0xEF, 0x44, 0x44);
        private static readonly SolidColorBrush DangerBg = Fr(0xFE, 0xF2, 0xF2);
        private static readonly SolidColorBrush Info = Fr(0x3B, 0x82, 0xF6);
        private static readonly SolidColorBrush InfoBg = Fr(0xEF, 0xF6, 0xFF);
        static SolidColorBrush Fr(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

        private static readonly string[] FolderColors = { "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#06B6D4" };
        private static readonly Geometry FolderGeo = Geometry.Parse("M10,4 H4 C2.9,4 2,4.9 2,6 V18 C2,19.1 2.9,20 4,20 H20 C21.1,20 22,19.1 22,18 V8 C22,6.9 21.1,6 20,6 H12 L10,4 Z");

        private static readonly Dictionary<string, (string c, string bg)> FileCfg = new()
        {
            ["pdf"] = ("#EF4444", "#FEF2F2"),
            // CAD Planos (morado)
            ["dwg"] = ("#8B5CF6", "#F5F3FF"), ["dxf"] = ("#8B5CF6", "#F5F3FF"),
            // CAD Modelos 3D (morado)
            ["step"] = ("#8B5CF6", "#F5F3FF"), ["stp"] = ("#8B5CF6", "#F5F3FF"), ["igs"] = ("#8B5CF6", "#F5F3FF"),
            // CAD Piezas (morado)
            ["ipt"] = ("#8B5CF6", "#F5F3FF"), ["sldprt"] = ("#8B5CF6", "#F5F3FF"),
            // CAD Ensambles (teal — diferenciado de piezas)
            ["iam"] = ("#0891B2", "#ECFEFF"), ["sldasm"] = ("#0891B2", "#ECFEFF"),
            // CNC Mastercam (naranja ingenieria)
            ["mcam"] = ("#EA580C", "#FFF7ED"), ["mcx-5"] = ("#EA580C", "#FFF7ED"),
            ["mcx-7"] = ("#EA580C", "#FFF7ED"), ["mcx-9"] = ("#EA580C", "#FFF7ED"),
            // Office
            ["xlsx"] = ("#10B981", "#F0FDF4"), ["xls"] = ("#10B981", "#F0FDF4"), ["csv"] = ("#10B981", "#F0FDF4"),
            ["docx"] = ("#3B82F6", "#EFF6FF"), ["doc"] = ("#3B82F6", "#EFF6FF"),
            ["pptx"] = ("#F59E0B", "#FFFBEB"), ["ppt"] = ("#F59E0B", "#FFFBEB"),
            // Media
            ["mp4"] = ("#EC4899", "#FDF2F8"), ["zip"] = ("#F59E0B", "#FFFBEB"), ["rar"] = ("#F59E0B", "#FFFBEB"),
            ["jpg"] = ("#10B981", "#F0FDF4"), ["jpeg"] = ("#10B981", "#F0FDF4"), ["jfif"] = ("#10B981", "#F0FDF4"),
            ["png"] = ("#10B981", "#F0FDF4"), ["gif"] = ("#10B981", "#F0FDF4"),
            ["bmp"] = ("#10B981", "#F0FDF4"), ["webp"] = ("#10B981", "#F0FDF4"),
            // Texto
            ["txt"] = ("#64748B", "#F8FAFC"), ["log"] = ("#64748B", "#F8FAFC"),
            ["html"] = ("#F59E0B", "#FFFBEB"), ["xml"] = ("#F59E0B", "#FFFBEB"), ["json"] = ("#F59E0B", "#FFFBEB"),
        };
        static (string c, string bg) GFC(string fn) { var e = System.IO.Path.GetExtension(fn)?.TrimStart('.').ToLowerInvariant() ?? ""; return FileCfg.TryGetValue(e, out var v) ? v : ("#64748B", "#F8FAFC"); }

        // P6: Safe Segoe MDL2 icons — mapped from real DB extensions (Mar 2026)
        static string FIcon(string fn) { var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant(); return e switch {
            ".jpg" or ".jpeg" or ".jfif" or ".png" or ".gif" or ".bmp" or ".webp" => "\uE91B",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "\uE714",
            ".zip" or ".rar" or ".7z" => "\uE8C8",
            ".pdf" => "\uEA90",
            ".doc" or ".docx" => "\uE8A5",
            ".xls" or ".xlsx" or ".csv" => "\uE80B",
            ".ppt" or ".pptx" => "\uE8A5",
            // CAD Piezas + Planos + Modelos 3D
            ".dwg" or ".dxf" or ".step" or ".stp" or ".igs" or ".ipt" or ".sldprt" => "\uE8FD",
            // CAD Ensambles (icono diferenciado)
            ".iam" or ".sldasm" => "\uE912",
            // CNC Mastercam (wrench/repair icon)
            ".mcam" or ".mcx-5" or ".mcx-7" or ".mcx-9" => "\uE90F",
            _ => "\uE8A5" }; }
        static string FType(string fn) { var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant(); return e switch {
            ".pdf" => "Documento PDF", ".doc" or ".docx" => "Documento Word",
            ".xls" or ".xlsx" => "Hoja de calculo Excel", ".ppt" or ".pptx" => "Presentacion PowerPoint",
            ".jpg" or ".jpeg" or ".jfif" => "Imagen JPEG", ".png" => "Imagen PNG", ".gif" => "Imagen GIF",
            ".mp4" => "Video MP4", ".zip" => "Archivo ZIP", ".rar" => "Archivo RAR",
            ".txt" => "Archivo de texto", ".csv" => "Archivo CSV", ".log" => "Archivo de registro",
            // CAD
            ".dwg" => "Plano AutoCAD", ".dxf" => "Plano DXF",
            ".step" or ".stp" => "Modelo 3D STEP", ".igs" => "Modelo 3D IGES",
            ".ipt" => "Pieza Inventor", ".iam" => "Ensamble Inventor",
            ".sldprt" => "Pieza SolidWorks", ".sldasm" => "Ensamble SolidWorks",
            // CNC
            ".mcam" => "Programa Mastercam", ".mcx-5" => "Programa Mastercam 5",
            ".mcx-7" => "Programa Mastercam 7", ".mcx-9" => "Programa Mastercam 9",
            _ => $"Archivo {e?.TrimStart('.').ToUpperInvariant()}" }; }
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
            // Register drag handlers with handledEventsToo=true so they ALWAYS fire
            // regardless of child elements or visual tree state (fixes intermittent drag-drop)
            AddHandler(DragEnterEvent, new DragEventHandler(Window_DragEnter), handledEventsToo: true);
            AddHandler(DragOverEvent, new DragEventHandler(Window_DragOver), handledEventsToo: true);
            AddHandler(DragLeaveEvent, new DragEventHandler(Window_DragLeave), handledEventsToo: true);
            AddHandler(DropEvent, new DragEventHandler(Window_Drop), handledEventsToo: true);
            Loaded += async (s, e) =>
            {
                InitSidebar(); UpdateViewToggle(); _ = LoadGlobalStorage(); _ = LoadSidebarActivity();
                // V3-E: Initialize FileWatcher
                FileWatcherService.Instance.CurrentUserId = _currentUser?.Id ?? 0;
                FileWatcherService.Instance.Initialize();
                FileWatcherService.Instance.FileAutoUploaded += OnFileAutoUploaded;
                FileWatcherService.Instance.FileSyncStateChanged += OnFileSyncStateChanged;
                UpdateLocalCacheUI();
                // F2: Cleanup old thumbnails fire-and-forget
                _ = Task.Run(() => CleanupThumbnailCache());
                await SafeLoad(() => _navigateToFolderId.HasValue ? NavTo(_navigateToFolderId.Value, hist: false) : NavigateToRoot());
                // F1: Prefetch top folders fire-and-forget (after initial load)
                if (_folderCache.Count <= 1) _ = PrefetchTopFolders();
            };
        }

        /// <summary>Open Drive navigating directly to a specific folder</summary>
        public DriveV2Window(UserSession user, int folderId) : this(user)
        {
            _navigateToFolderId = folderId;
            _currentFolderId = folderId; // Set early so drag-drop works before Loaded completes
        }
        private int? _navigateToFolderId;

        /// <summary>Open Drive in selection mode for linking a folder to an order</summary>
        public DriveV2Window(UserSession user, int orderId, string orderPo) : this(user)
        {
            _isSelectionMode = true; _selectionOrderId = orderId; _selectionOrderPo = orderPo;
            SelectionBanner.Visibility = Visibility.Visible;
            SelectionBannerText.Text = $"Seleccione una carpeta para vincular a {orderPo}";
        }

        protected override void OnClosed(EventArgs e)
        {
            // V3-E: Unsubscribe from FileWatcher events (don't dispose — singleton survives window)
            FileWatcherService.Instance.FileAutoUploaded -= OnFileAutoUploaded;
            FileWatcherService.Instance.FileSyncStateChanged -= OnFileSyncStateChanged;
            _cts?.Cancel(); _cts?.Dispose(); base.OnClosed(e);
        }

        // ===============================================
        // SIDEBAR
        // ===============================================
        private void InitSidebar()
        {
            foreach (var (id, ico, lbl) in new[] { ("all", "\uE80F", "Todos los archivos"), ("recent", "\uE823", "Recientes"), ("starred", "\uE734", "Destacados"), ("trash", "\uE74D", "Papelera") })
            { var it = MkNav(id, ico, lbl); NavPanel.Children.Add(it); _navItems.Add(it); }
            SetNav("all");
            foreach (var (id, clr, lbl) in new[] { ("pdf", "#EF4444", "PDFs"), ("img", "#10B981", "Imagenes") })
            { var fi = MkFilter(id, clr, lbl, "0"); _filterItems.Add(fi); FilterPanel.Children.Add(fi); }

            // MEJORA-4: CAD parent with collapsible sub-filters
            var cadParent = MkFilter("cad", "#8B5CF6", "CAD (todos)", "0");
            _filterItems.Add(cadParent); FilterPanel.Children.Add(cadParent);
            var cadSubPanel = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
            _cadSubPanel = cadSubPanel;
            // MEJORA-4: Sub-filtros CAD with PNG icons from ico-ima/
            foreach (var (id, clr, lbl, ico) in new[] {
                ("cad_asm", "#0891B2", "Ensambles", "gear.png"), ("cad_part", "#8B5CF6", "Piezas", "ruler.png"),
                ("cad_dwg", "#8B5CF6", "Planos", "ruler.png"), ("cad_3d", "#8B5CF6", "Modelos 3D", "ruler.png"),
                ("cad_cnc", "#EA580C", "CNC", "wrench.png") })
            { var fi = MkCadSubFilter(id, clr, lbl, ico); _filterItems.Add(fi); cadSubPanel.Children.Add(fi); }
            FilterPanel.Children.Add(cadSubPanel);

            foreach (var (id, clr, lbl) in new[] { ("xls", "#10B981", "Hojas de calculo"), ("vid", "#EC4899", "Videos") })
            { var fi = MkFilter(id, clr, lbl, "0"); _filterItems.Add(fi); FilterPanel.Children.Add(fi); }

            // Dev tools: solo visibles para usuario "caaj"
            if (_currentUser?.Username?.ToLowerInvariant() == "caaj")
            {
                // Diagnose orphans button
                var diagBtn = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 24, 0, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)) };
                var dsp = new StackPanel { Orientation = Orientation.Horizontal };
                dsp.Children.Add(new TextBlock { Text = "\uE9CE", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                dsp.Children.Add(new TextBlock { Text = "Diagnosticar", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)), VerticalAlignment = VerticalAlignment.Center });
                diagBtn.Child = dsp;
                diagBtn.MouseEnter += (s, e) => diagBtn.Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xE6, 0x8A));
                diagBtn.MouseLeave += (s, e) => diagBtn.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
                diagBtn.MouseLeftButtonDown += async (s, e) => await RunDiagnoseOrphans();
                NavPanel.Children.Add(diagBtn);

                // Stress/DragDrop tests button
                var testBtn = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 4, 0, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromRgb(0xED, 0xE9, 0xFE)) };
                var tsp = new StackPanel { Orientation = Orientation.Horizontal };
                tsp.Children.Add(new TextBlock { Text = "\uE9D9", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
                tsp.Children.Add(new TextBlock { Text = "Tests", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)), VerticalAlignment = VerticalAlignment.Center });
                testBtn.Child = tsp;
                testBtn.MouseEnter += (s, e) => testBtn.Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xD6, 0xFE));
                testBtn.MouseLeave += (s, e) => testBtn.Background = new SolidColorBrush(Color.FromRgb(0xED, 0xE9, 0xFE));
                testBtn.MouseLeftButtonDown += (s, e) => new StressTestWindow().Show();
                NavPanel.Children.Add(testBtn);
            }
        }

        Border MkNav(string id, string ico, string lbl)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = id };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = ico, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = TextSecondary });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Foreground = TextSecondary });
            b.Child = sp;
            b.MouseLeftButtonDown += async (s, e) => { if (id == "all") { SetNav(id); ClearMultiSelect(); _activeFilter = null; UpdateFilterHighlight(); await SafeLoad(() => NavigateToRoot()); } else if (id == "recent") { SetNav(id); ClearMultiSelect(); _activeFilter = null; UpdateFilterHighlight(); await SafeLoad(() => LoadRecentsInContent()); } };
            b.MouseEnter += (s, e) => { if (b.Tag as string != _activeNav) b.Background = HoverBg; };
            b.MouseLeave += (s, e) => { if (b.Tag as string != _activeNav) b.Background = Brushes.Transparent; };
            return b;
        }

        void SetNav(string id) { _activeNav = id; foreach (var it in _navItems) { var a = it.Tag as string == id; it.Background = a ? ActiveBg : Brushes.Transparent; if (it.Child is StackPanel sp) foreach (var c in sp.Children.OfType<TextBlock>()) c.Foreground = a ? Primary : TextSecondary; } }

        // ===============================================
        // V3-B: RECIENTES & ACTIVIDAD (SIDEBAR)
        // ===============================================

        async Task LoadSidebarActivity()
        {
            try
            {
                await LoadActivityFeed();

                // Actividad solo visible para roles con visibilidad amplia
                var role = _currentUser.Role?.ToLowerInvariant() ?? "";
                if (role is not "direccion" and not "administracion" and not "coordinacion")
                    ActivitySection.Visibility = Visibility.Collapsed;
            }
            catch { /* sidebar load failure is non-critical */ }
        }

        async Task LoadRecentsInContent()
        {
            _currentFolderId = null;
            _currentFolders.Clear();
            _currentFiles.Clear();
            _breadcrumb.Clear();
            _selectedFileIds.Clear();
            ClearMultiSelect();

            BackToFoldersBtn.Visibility = Visibility.Collapsed;
            SectionTitle.Text = "Archivos recientes";
            ShowSkeletonLoading();

            try
            {
                List<DriveFileDb> files;
                if (_recentShowAll)
                    files = await SupabaseService.Instance.GetDriveRecentFiles(50, _cts.Token);
                else
                {
                    var all = await SupabaseService.Instance.GetDriveRecentFiles(50, _cts.Token);
                    files = all.Where(f => f.UploadedBy == _currentUser.Id).Take(50).ToList();
                }

                // Build header with toggle
                var headerSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
                var toggleLabel = new TextBlock { Text = _recentShowAll ? "Todos" : "Mis archivos", FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                var toggleBorder = new Border { Width = 36, Height = 18, CornerRadius = new CornerRadius(9), Background = _recentShowAll ? Success : Primary, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
                var toggleDot = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Brushes.White, HorizontalAlignment = _recentShowAll ? HorizontalAlignment.Left : HorizontalAlignment.Right, Margin = new Thickness(2) };
                toggleBorder.Child = toggleDot;
                toggleBorder.MouseLeftButtonDown += async (s, e) =>
                {
                    _recentShowAll = !_recentShowAll;
                    await SafeLoad(() => LoadRecentsInContent());
                };
                headerSp.Children.Add(toggleLabel);
                headerSp.Children.Add(toggleBorder);

                SectionSubtitle.Text = $"{files.Count} archivo{(files.Count != 1 ? "s" : "")}";
                StatusText.Text = $"{files.Count} elemento{(files.Count != 1 ? "s" : "")}";

                if (files.Count == 0)
                {
                    ContentHost.Content = headerSp;
                    EmptyStateTitle.Text = "Sin archivos recientes";
                    EmptyStateSubtitle.Text = "Los archivos que subas o modifiques apareceran aqui";
                    EmptyStateActions.Visibility = Visibility.Collapsed;
                    EmptyState.Visibility = Visibility.Visible;
                }
                else
                {
                    var stk = new StackPanel();
                    stk.Children.Add(headerSp);

                    if (_viewMode == "list")
                    {
                        // Render as list
                        _currentFiles = files;
                        var wrap = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), ClipToBounds = true };
                        var listStk = new StackPanel();
                        foreach (var f in files) listStk.Children.Add(MkFileListRow(f));
                        wrap.Child = listStk;
                        stk.Children.Add(wrap);
                    }
                    else
                    {
                        // Render as grid
                        _currentFiles = files;
                        var filew = new WrapPanel();
                        foreach (var f in files) { var c = MkFileCard(f); c.Width = 200; c.Margin = new Thickness(6); filew.Children.Add(c); }
                        stk.Children.Add(filew);
                    }
                    ContentHost.Content = stk;
                }

                RenderBreadcrumb();
                UpdateSearchPlaceholder();
            }
            catch (Exception ex) { StatusText.Text = "Error"; SectionSubtitle.Text = ex.Message; ContentHost.Content = null; }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        Border MkRecentFileItem(DriveFileDb file)
        {
            var (cH, _) = GFC(file.FileName); var fC = CH(cH);
            var row = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            var ico = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromArgb(25, fC.R, fC.G, fC.B)), Margin = new Thickness(0, 0, 8, 0) };
            ico.Child = new TextBlock { Text = FIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Foreground = new SolidColorBrush(fC), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(ico);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 150 };
            textPanel.Children.Add(new TextBlock { Text = Tr(file.FileName, 25), FontSize = 12, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis });
            textPanel.Children.Add(new TextBlock { Text = RelT(file.UploadedAt), FontSize = 10, Foreground = TextLight });
            sp.Children.Add(textPanel);

            row.Child = sp;
            row.ToolTip = $"{file.FileName}\n{Services.Drive.DriveService.FormatFileSize(file.FileSize)}\n{file.UploadedAt?.ToString("dd/MM/yyyy HH:mm")}";

            row.MouseEnter += (s, e) => row.Background = HoverBg;
            row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonDown += async (s, e) =>
            {
                // Navegar a la carpeta del archivo y seleccionarlo
                await SafeLoad(async () =>
                {
                    await NavTo(file.FolderId);
                    var found = _currentFiles.FirstOrDefault(f => f.Id == file.Id);
                    if (found != null)
                    {
                        _selectedFileIds.Clear();
                        _selectedFileIds.Add(found.Id);
                        RenderContent();
                    }
                });
            };

            return row;
        }

        async Task LoadActivityFeed()
        {
            ActivityPanel.Children.Clear();
            try
            {
                var activities = await SupabaseService.Instance.GetDriveRecentActivity(10, ct: _cts.Token);
                if (activities.Count == 0)
                {
                    ActivityPanel.Children.Add(new TextBlock { Text = "Sin actividad reciente", FontSize = 12, Foreground = TextLight, Margin = new Thickness(12, 4, 0, 0), FontStyle = FontStyles.Italic });
                    return;
                }

                foreach (var act in activities)
                    ActivityPanel.Children.Add(MkActivityItem(act));
            }
            catch { ActivityPanel.Children.Add(new TextBlock { Text = "Error al cargar", FontSize = 12, Foreground = TextLight, Margin = new Thickness(12, 4, 0, 0) }); }
        }

        Border MkActivityItem(Models.Database.DriveActivityDb act)
        {
            var actionText = act.Action switch
            {
                "upload" => "subio",
                "download" => "descargo",
                "rename" => "renombro",
                "delete" => "elimino",
                "move" => "movio",
                "copy" => "copio",
                _ => act.Action
            };

            var row = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 1, 0, 1), Cursor = act.Action == "delete" ? Cursors.Arrow : Cursors.Hand, Background = Brushes.Transparent };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            // Iniciales del usuario en circulo
            var initials = "?";
            if (act.UserId.HasValue && _userNameCache.TryGetValue(act.UserId.Value, out var name))
                initials = string.Concat(name.Split(' ').Where(w => w.Length > 0).Select(w => w[0])).ToUpperInvariant();
            else if (act.UserId.HasValue)
                _ = Task.Run(async () => { var n = await ResolveUser(act.UserId); Dispatcher.Invoke(() => LoadActivityFeed()); }); // lazy resolve

            var avatar = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Primary, Margin = new Thickness(0, 0, 8, 0) };
            avatar.Child = new TextBlock { Text = initials.Length > 2 ? initials[..2] : initials, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(avatar);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 155 };
            var actionTb = new TextBlock { FontSize = 11, Foreground = TextMuted, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
            actionTb.Inlines.Add(new System.Windows.Documents.Run { Text = actionText, Foreground = TextSecondary, FontWeight = FontWeights.Medium });
            actionTb.Inlines.Add(new System.Windows.Documents.Run { Text = $" {Tr(act.TargetName ?? "archivo", 20)}", Foreground = TextPrimary });
            if (act.Action == "delete") actionTb.TextDecorations = TextDecorations.Strikethrough;
            textPanel.Children.Add(actionTb);
            textPanel.Children.Add(new TextBlock { Text = RelT(act.CreatedAt), FontSize = 10, Foreground = TextLight });
            sp.Children.Add(textPanel);

            row.Child = sp;

            if (act.Action != "delete")
            {
                row.MouseEnter += (s, e) => row.Background = HoverBg;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonDown += async (s, e) =>
                {
                    if (act.FolderId.HasValue)
                        await SafeLoad(() => NavTo(act.FolderId.Value));
                };
            }

            return row;
        }

        void ActivityHeader_Click(object sender, MouseButtonEventArgs e)
        {
            _activityExpanded = !_activityExpanded;
            ActivityPanel.Visibility = _activityExpanded ? Visibility.Visible : Visibility.Collapsed;
            var targetAngle = _activityExpanded ? 90.0 : 0.0;
            var anim = new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
            ActivityChevronRotation.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        /// <summary>Refresh sidebar activity + recents content if active</summary>
        void RefreshSidebarRecents()
        {
            _ = LoadSidebarActivity();
            if (_activeNav == "recent") _ = SafeLoad(() => LoadRecentsInContent());
        }

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

        // MEJORA-4: CAD sub-filter with PNG icon from ico-ima/
        Border MkCadSubFilter(string filterId, string clr, string lbl, string icoFile)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = filterId };
            var g = new Grid(); var sp = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
            var img = new Image { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Opacity = 0.7 };
            img.Source = new BitmapImage(new Uri($"pack://application:,,,/ico-ima/{icoFile}", UriKind.Absolute));
            sp.Children.Add(img);
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 12.5, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(sp);
            var cntTb = new TextBlock { Text = "0", FontSize = 11, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            g.Children.Add(cntTb);
            g.IsHitTestVisible = false;
            b.Child = g;
            b.MouseLeftButtonDown += (s, e) =>
            {
                if (_activeFilter == filterId) _activeFilter = null;
                else _activeFilter = filterId;
                UpdateFilterHighlight();
                RenderContent();
                var tot = _activeFilter != null ? ApplyFileFilter(_currentFiles, _activeFilter).Count : _currentFiles.Count;
                StatusText.Text = _activeFilter != null ? $"Filtro: {lbl} ({tot} archivo(s))" : $"{_currentFolders.Count + _currentFiles.Count} elemento(s)";
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
            ["cad"] = new[] { ".dwg", ".dxf", ".step", ".stp", ".igs", ".sldprt", ".sldasm", ".ipt", ".iam", ".mcam", ".mcx-5", ".mcx-7", ".mcx-9" },
            // MEJORA-4: Sub-filtros CAD
            ["cad_asm"] = new[] { ".iam", ".sldasm" },
            ["cad_part"] = new[] { ".ipt", ".sldprt" },
            ["cad_dwg"] = new[] { ".dwg", ".dxf" },
            ["cad_3d"] = new[] { ".step", ".stp", ".igs" },
            ["cad_cnc"] = new[] { ".mcam", ".mcx-5", ".mcx-7", ".mcx-9" },
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
            var hasFiles = _currentFiles.Count > 0;
            Debug.WriteLine($"[DriveV2] UpdateFilterCounts: {_currentFiles.Count} files, hasFiles={hasFiles}");

            // Update ALL filter items (including sub-filters) by their Tag
            foreach (var fi in _filterItems)
            {
                var filterId = fi.Tag as string;
                if (filterId == null || !FilterExtensions.ContainsKey(filterId)) continue;
                var count = hasFiles ? ApplyFileFilter(_currentFiles, filterId).Count : 0;
                // Update count text
                if (fi.Child is Grid g)
                {
                    var ct = g.Children.OfType<TextBlock>().FirstOrDefault(t => t.HorizontalAlignment == HorizontalAlignment.Right);
                    if (ct != null) ct.Text = count.ToString();
                }
                // Disable/enable filter visually
                fi.Opacity = (hasFiles && count > 0) ? 1.0 : 0.4;
                fi.Cursor = (hasFiles && count > 0) ? Cursors.Hand : Cursors.Arrow;
                fi.IsHitTestVisible = hasFiles && count > 0;
            }

            // MEJORA-4: Show/hide CAD sub-panel based on whether any CAD files exist
            if (_cadSubPanel != null)
            {
                var cadCount = hasFiles ? ApplyFileFilter(_currentFiles, "cad").Count : 0;
                _cadSubPanel.Visibility = cadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
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
            _currentFolderId = fId; _selectedFileIds.Clear();
            // Clear search box on navigation (e.g. clicking a search result)
            if (!string.IsNullOrEmpty(SearchBox.Text)) { SearchBox.Text = ""; SearchPlaceholder.Visibility = Visibility.Visible; }
            // BUG-3: Reset filter on navigation
            if (_activeFilter != null) { _activeFilter = null; UpdateFilterHighlight(); }

            // Stale-while-revalidate: if we have cached data, render it INSTANTLY then refresh in background
            if (_folderCache.TryGetValue(fId, out var snap))
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

            ShowSkeletonLoading();

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

                // Save to navigation cache
                if (_currentFolderId.HasValue)
                    _folderCache[_currentFolderId.Value] = new FolderSnapshot(
                        _currentFolders, _currentFiles, _breadcrumb, DateTime.Now);

                // BUG-4: Storage UI already driven by _globalStorageBytes (loaded once at startup)
            }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] LoadFolderFull ERR: {ex.Message}\n{ex.StackTrace}"); StatusText.Text = "Error"; SectionTitle.Text = "Error"; SectionSubtitle.Text = ex.Message; ContentHost.Content = null; }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                Debug.WriteLine($"[DriveV2] === LoadFolder TOTAL: {sw0.ElapsedMilliseconds}ms ===");
            }
        }

        /// <summary>Reload current folder (convenience wrapper after CRUD operations)</summary>
        async Task LoadFolder() { if (_currentFolderId.HasValue) await LoadFolderFull(_currentFolderId.Value); }

        /// <summary>Render current folder state to UI (used by both LoadFolderFull and cache-hit path)</summary>
        void RenderFolderUI()
        {
            // Clear all selection state when changing folders
            ClearMultiSelect();
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
            EmptyStateTitle.Text = "Carpeta vacia";
            EmptyStateSubtitle.Text = "Crea una carpeta o sube un archivo para comenzar";
            EmptyStateActions.Visibility = Visibility.Visible;
            EmptyState.Visibility = tot == 0 ? Visibility.Visible : Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Collapsed;
            UpdateFilterCounts();
            UpdateSearchPlaceholder();
        }

        // ===============================================
        // SKELETON LOADING (P1)
        // ===============================================
        void ShowSkeletonLoading()
        {
            EmptyState.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Collapsed;
            if (_viewMode == "list") ShowSkeletonList(); else ShowSkeletonGrid();
        }

        void ShowSkeletonGrid()
        {
            var wrap = new WrapPanel();
            for (int i = 0; i < 8; i++) wrap.Children.Add(MkGhostCard());
            ContentHost.Content = wrap;
        }

        void ShowSkeletonList()
        {
            var stk = new StackPanel();
            var wrap = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), ClipToBounds = true };
            var inner = new StackPanel();
            for (int i = 0; i < 8; i++) inner.Children.Add(MkGhostRow());
            wrap.Child = inner;
            stk.Children.Add(wrap);
            ContentHost.Content = stk;
        }

        Border MkGhostCard()
        {
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), Width = 200, Margin = new Thickness(6), ClipToBounds = true };
            var stk = new StackPanel();
            // Thumbnail placeholder
            var thumb = new Border { Height = 120, Background = BorderLight };
            stk.Children.Add(thumb);
            // Content area
            var content = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };
            // Name bar
            var namebar = new Border { Height = 14, Width = 130, CornerRadius = new CornerRadius(4), Background = BorderLight, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
            content.Children.Add(namebar);
            // Meta bar
            var metabar = new Border { Height = 10, Width = 80, CornerRadius = new CornerRadius(4), Background = BorderLight, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
            content.Children.Add(metabar);
            // Date bar
            var datebar = new Border { Height = 10, Width = 60, CornerRadius = new CornerRadius(4), Background = BorderLight, HorizontalAlignment = HorizontalAlignment.Left };
            content.Children.Add(datebar);
            stk.Children.Add(content);
            card.Child = stk;
            // Pulse animation (opacity oscillation)
            var pulse = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(800)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            card.BeginAnimation(OpacityProperty, pulse);
            return card;
        }

        Grid MkGhostRow()
        {
            var row = new Grid { Height = 48 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
            // Icon placeholder
            sp.Children.Add(new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = BorderLight, Margin = new Thickness(0, 0, 10, 0) });
            // Name bar
            sp.Children.Add(new Border { Height = 12, Width = 160, CornerRadius = new CornerRadius(4), Background = BorderLight, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(sp, 0); row.Children.Add(sp);
            // Type bar
            var tb = new Border { Height = 10, Width = 50, CornerRadius = new CornerRadius(4), Background = BorderLight, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(tb, 1); row.Children.Add(tb);
            // Size bar
            var sb = new Border { Height = 10, Width = 40, CornerRadius = new CornerRadius(4), Background = BorderLight, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(sb, 2); row.Children.Add(sb);
            // Date bar
            var db = new Border { Height = 10, Width = 80, CornerRadius = new CornerRadius(4), Background = BorderLight, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(db, 3); row.Children.Add(db);
            // Bottom border
            row.Children.Add(new Border { Height = 1, Background = BorderLight, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Stretch });
            // Pulse
            var pulse = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(800)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            row.BeginAnimation(OpacityProperty, pulse);
            return row;
        }

        /// <summary>Apply staggered fade-in to rendered cards/rows</summary>
        void ApplyStaggeredFadeIn(Panel container, int maxStagger = 12)
        {
            int i = 0;
            foreach (UIElement child in container.Children)
            {
                if (child is FrameworkElement fe)
                {
                    fe.Opacity = 0;
                    fe.RenderTransform = new TranslateTransform(0, 12);
                    fe.RenderTransformOrigin = new Point(0.5, 0.5);
                    int delay = Math.Min(i, maxStagger) * 30;
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
                    var captured = fe;
                    timer.Tick += (s, e) =>
                    {
                        ((System.Windows.Threading.DispatcherTimer)s!).Stop();
                        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                        var slideIn = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                        captured.BeginAnimation(OpacityProperty, fadeIn);
                        captured.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
                    };
                    timer.Start();
                    i++;
                }
            }
        }

        /// <summary>Animate scale transform on a FrameworkElement</summary>
        static void AnimateScale(FrameworkElement el, double to, int ms)
        {
            if (el.RenderTransform is not ScaleTransform st) return;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
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
        void UpdateStorageUI(long addedBytes) { if (_globalStorageBytes >= 0) _globalStorageBytes += addedBytes; UpdateStorageUI(); }

        void UpdateLocalCacheUI()
        {
            var openSize = FileWatcherService.Instance.GetLocalCacheSize();
            // F3: Include thumbnail cache size
            long thumbSize = 0;
            try { if (Directory.Exists(ThumbnailCacheDir)) thumbSize = new DirectoryInfo(ThumbnailCacheDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); } catch { }
            var totalSize = openSize + thumbSize;
            if (totalSize > 0)
            {
                LocalCacheLabel.Text = $"Cache local: {Services.Drive.DriveService.FormatFileSize(totalSize)}";
                ClearCacheBtn.Visibility = Visibility.Visible;
            }
            else
            {
                LocalCacheLabel.Text = "";
                ClearCacheBtn.Visibility = Visibility.Collapsed;
            }
        }

        void ClearCache_Click(object sender, MouseButtonEventArgs e)
        {
            if (FileWatcherService.Instance.HasPendingSyncs())
            {
                ShowToast("No se puede limpiar: hay archivos sincronizando", "warning");
                return;
            }
            FileWatcherService.Instance.ClearLocalCache();
            UpdateLocalCacheUI();
            ShowToast("Cache local limpiado", "success");
        }

        void InvalidateStats(int? fId = null)
        {
            if (fId.HasValue) { _statsCache.Remove(fId.Value); _folderCache.Remove(fId.Value); }
            else { _statsCache.Clear(); _folderCache.Clear(); }
            if (_currentFolderId.HasValue) { _statsCache.Remove(_currentFolderId.Value); _folderCache.Remove(_currentFolderId.Value); }
        }

        // F1: Prefetch top 2 levels of folders for instant navigation
        async Task PrefetchTopFolders()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var roots = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token);
                var prefetched = 0;
                foreach (var root in roots)
                {
                    if (_cts.IsCancellationRequested) return;
                    if (_folderCache.ContainsKey(root.Id)) continue;
                    var bcT = SupabaseService.Instance.GetDriveBreadcrumb(root.Id, _cts.Token);
                    var fT = SupabaseService.Instance.GetDriveChildFolders(root.Id, _cts.Token);
                    var fiT = SupabaseService.Instance.GetDriveFilesByFolder(root.Id, _cts.Token);
                    await Task.WhenAll(bcT, fT, fiT);
                    _folderCache[root.Id] = new FolderSnapshot(fT.Result, fiT.Result, bcT.Result, DateTime.Now);
                    prefetched++;
                    // Level 2: children of root
                    foreach (var child in fT.Result)
                    {
                        if (_cts.IsCancellationRequested) return;
                        if (_folderCache.ContainsKey(child.Id)) continue;
                        var bc2 = SupabaseService.Instance.GetDriveBreadcrumb(child.Id, _cts.Token);
                        var f2 = SupabaseService.Instance.GetDriveChildFolders(child.Id, _cts.Token);
                        var fi2 = SupabaseService.Instance.GetDriveFilesByFolder(child.Id, _cts.Token);
                        await Task.WhenAll(bc2, f2, fi2);
                        _folderCache[child.Id] = new FolderSnapshot(f2.Result, fi2.Result, bc2.Result, DateTime.Now);
                        prefetched++;
                    }
                }
                Debug.WriteLine($"[DriveV2] Prefetch complete: {prefetched} folders cached in {sw.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] Prefetch error: {ex.Message}"); }
        }

        // F2: Cleanup old/large thumbnail cache
        void CleanupThumbnailCache()
        {
            try
            {
                if (!Directory.Exists(ThumbnailCacheDir)) return;
                var dir = new DirectoryInfo(ThumbnailCacheDir);
                var files = dir.GetFiles("*", SearchOption.AllDirectories);
                var totalSize = files.Sum(f => f.Length);
                var cutoff = DateTime.Now.AddDays(-30);
                long freed = 0;

                // Delete thumbnails older than 30 days
                foreach (var f in files.Where(f => f.LastAccessTime < cutoff))
                {
                    freed += f.Length; f.Delete();
                }

                // If still over 200MB, delete oldest by access time
                const long maxBytes = 200 * 1024 * 1024;
                if (totalSize - freed > maxBytes)
                {
                    var remaining = dir.GetFiles("*", SearchOption.AllDirectories)
                        .OrderBy(f => f.LastAccessTime).ToList();
                    var current = remaining.Sum(f => f.Length);
                    foreach (var f in remaining)
                    {
                        if (current <= maxBytes) break;
                        current -= f.Length; freed += f.Length; f.Delete();
                    }
                }

                if (freed > 0) Debug.WriteLine($"[DriveV2] Thumbnail cleanup: freed {Services.Drive.DriveService.FormatFileSize(freed)}");
            }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] Thumbnail cleanup error: {ex.Message}"); }
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

            // P1D: Staggered fade-in on WrapPanels (cards)
            if (ContentHost.Content is StackPanel renderStk)
            {
                foreach (var child in renderStk.Children.OfType<WrapPanel>())
                    ApplyStaggeredFadeIn(child);
            }
            else if (ContentHost.Content is WrapPanel wp)
            {
                ApplyStaggeredFadeIn(wp);
            }
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
            rb.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavTo(folder.Id)))); m.Items.Add(MI("Renombrar", (_, _) => RenFolder(folder))); m.Items.Add(MI("Mover a...", (_, _) => _ = MoveFolderTo(folder))); m.Items.Add(MI("Descargar como ZIP", (_, _) => _ = DownloadFolderAsZip(folder))); m.Items.Add(new Separator()); m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkOrder(folder))); if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await Unlink(folder))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFolder(folder)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = rb; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
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
            // V3-E: Sync badge for list row
            var syncState = FileWatcherService.Instance.GetSyncState(file.Id);
            if (syncState != SyncState.None)
            {
                var syncBadge = MkSyncBadge(syncState);
                syncBadge.Margin = new Thickness(6, 0, 0, 0);
                syncBadge.VerticalAlignment = VerticalAlignment.Center;
                np.Children.Add(syncBadge);
            }
            rb.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { _ = OpenFileInPlace(file); return; }
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ToggleFileSelect(file); }
                else { ClearMultiSelect(); _selectedFileIds.Add(file.Id); UpdateMultiSelectBar(); RenderContent(); }
            };
            rb.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Abrir", async (_, _) => await OpenFileInPlace(file))); m.Items.Add(MI("Descargar", async (_, _) => await DlFile(file))); m.Items.Add(MI("Renombrar", (_, _) => RenFile(file))); m.Items.Add(new Separator()); m.Items.Add(MI("Mover a...", (_, _) => _ = MoveFilesTo(new[] { file }))); m.Items.Add(MI("Copiar a...", (_, _) => _ = CopyFileTo(file))); m.Items.Add(MI("Duplicar", (_, _) => _ = DuplicateFile(file))); if (Services.Drive.DriveService.IsImageFile(file.FileName)) { m.Items.Add(new Separator()); m.Items.Add(MI("Ver imagen", (_, _) => OpenImageOverlay(file))); } m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFile(file)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = rb; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
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
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = new ScaleTransform(1, 1);
            card.MouseEnter += (s, e) =>
            {
                if (!blockedInSelection)
                {
                    card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 6, Opacity = 0.10 };
                    nameT.Foreground = Primary; moreBtn.Visibility = Visibility.Visible;
                    AnimateScale(card, 1.015, 150);
                }
            };
            card.MouseLeave += (s, e) =>
            {
                card.Effect = null; nameT.Foreground = TextPrimary; moreBtn.Visibility = Visibility.Collapsed;
                AnimateScale(card, 1.0, 200);
            };
            // MEJORA-9: Double-click to open folder (configurable: change ClickCount == 2 to ClickCount == 1 for single-click navigation)
            card.MouseLeftButtonDown += (s, e) => { if (blockedInSelection) return; if (e.ClickCount == 2) _ = SafeLoad(() => NavTo(folder.Id)); };
            card.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavTo(folder.Id)))); m.Items.Add(MI("Renombrar", (_, _) => RenFolder(folder))); m.Items.Add(MI("Mover a...", (_, _) => _ = MoveFolderTo(folder))); m.Items.Add(MI("Descargar como ZIP", (_, _) => _ = DownloadFolderAsZip(folder))); m.Items.Add(new Separator()); m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkOrder(folder))); if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await Unlink(folder))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFolder(folder)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = card; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
            return card;
        }

        // ===============================================
        // FILE CARD
        // ===============================================
        Border MkFileCard(DriveFileDb file)
        {
            var (cH, bH) = GFC(file.FileName); var fC = CH(cH); var fB = new SolidColorBrush(fC); var bgB = BH(bH);
            var sel = _selectedFileIds.Contains(file.Id);
            var isCut = _clipFiles != null && _clipOp == ClipOp.Cut && _clipFiles.Any(f => f.Id == file.Id);
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = sel ? Primary : BorderColor, BorderThickness = new Thickness(2), Cursor = Cursors.Hand, ClipToBounds = true, Opacity = isCut ? 0.45 : 1.0 };
            if (sel) card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 12, ShadowDepth = 2, Opacity = 0.15 };
            var mg = new Grid(); mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var prev = new Border { Background = bgB, CornerRadius = new CornerRadius(10, 10, 0, 0) }; var pg = new Grid();
            // V3-A + MEJORA-5: Show thumbnail for images and CAD files (if locally cached), icon for others
            var isImage = Services.Drive.DriveService.IsImageFile(file.FileName);
            var isCad = Services.Drive.DriveService.IsCadFile(file.FileName);
            if (isImage || isCad)
            {
                var thumbImg = new Image { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
                RenderOptions.SetBitmapScalingMode(thumbImg, BitmapScalingMode.HighQuality);
                pg.Children.Add(thumbImg);
                // Fallback icon (shown until thumbnail loads, or permanently if no thumbnail available)
                var fallbackIcon = new TextBlock { Text = FIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 48, Foreground = fB, Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                pg.Children.Add(fallbackIcon);
                // Load thumbnail async
                var fileRef = file; // capture for closure
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (isImage)
                        {
                            await LoadThumbnailAsync(fileRef, thumbImg, _cts.Token);
                        }
                        else // CAD: try Shell thumbnail from local cache
                        {
                            await LoadCadThumbnailAsync(fileRef, thumbImg, _cts.Token);
                        }
                        Dispatcher.Invoke(() => { if (thumbImg.Source != null) { thumbImg.Opacity = 1; fallbackIcon.Visibility = Visibility.Collapsed; } });
                    }
                    catch { /* non-critical */ }
                });
            }
            else
            {
                pg.Children.Add(new TextBlock { Text = FIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 48, Foreground = fB, Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            }
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
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = new ScaleTransform(1, 1);
            card.MouseEnter += (s, e) =>
            {
                selCircle.Visibility = Visibility.Visible;
                if (!_selectedFileIds.Contains(file.Id))
                {
                    card.BorderBrush = SlateLight;
                    card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 6, Opacity = 0.10 };
                }
                nameT.Foreground = Primary;
                AnimateScale(card, 1.015, 150);
            };
            card.MouseLeave += (s, e) =>
            {
                if (!_selectedFileIds.Contains(file.Id)) { selCircle.Visibility = Visibility.Collapsed; card.BorderBrush = BorderColor; card.Effect = null; }
                nameT.Foreground = TextPrimary;
                AnimateScale(card, 1.0, 200);
            };
            // V3-E: Sync badge overlay for grid card
            var syncState = FileWatcherService.Instance.GetSyncState(file.Id);
            if (syncState != SyncState.None)
            {
                var syncBadge = MkSyncBadge(syncState);
                syncBadge.HorizontalAlignment = HorizontalAlignment.Left;
                syncBadge.VerticalAlignment = VerticalAlignment.Top;
                syncBadge.Margin = new Thickness(10, 38, 0, 0);
                pg.Children.Add(syncBadge);
            }
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { _ = OpenFileInPlace(file); return; }
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ToggleFileSelect(file); }
                else { ClearMultiSelect(); _selectedFileIds.Add(file.Id); UpdateMultiSelectBar(); RenderContent(); }
            };
            card.MouseRightButtonDown += (s, e) => { e.Handled = true; var m = new ContextMenu(); m.Items.Add(MI("Abrir", async (_, _) => await OpenFileInPlace(file))); m.Items.Add(MI("Descargar", async (_, _) => await DlFile(file))); m.Items.Add(MI("Renombrar", (_, _) => RenFile(file))); m.Items.Add(new Separator()); m.Items.Add(MI("Mover a...", (_, _) => _ = MoveFilesTo(new[] { file }))); m.Items.Add(MI("Copiar a...", (_, _) => _ = CopyFileTo(file))); m.Items.Add(MI("Duplicar", (_, _) => _ = DuplicateFile(file))); if (Services.Drive.DriveService.IsImageFile(file.FileName)) { m.Items.Add(new Separator()); m.Items.Add(MI("Ver imagen", (_, _) => OpenImageOverlay(file))); } m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFile(file)); del.Foreground = Destructive; m.Items.Add(del); m.PlacementTarget = card; m.Placement = PlacementMode.MousePoint; m.IsOpen = true; };
            return card;
        }

        // Detail panel removed — download/delete via context menu, preview via image overlay

        async Task OpenFileWithSystemApp(DriveFileDb file)
        {
            try
            {
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "IMA-Drive", "preview");
                Directory.CreateDirectory(tempDir);
                var localPath = System.IO.Path.Combine(tempDir, $"{file.Id}_{file.FileName}");

                StatusText.Text = $"Descargando {file.FileName}...";
                var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, localPath, _cts.Token);
                if (!ok) { ShowToast($"Error al descargar {file.FileName}", "error"); return; }

                Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
                StatusText.Text = $"{file.FileName} abierto";
                ShowToast($"{file.FileName} abierto", "info");
            }
            catch (Exception ex)
            {
                ShowToast($"No se pudo abrir: {ex.Message}", "error");
            }
        }

        // ===============================================
        // V3-E: OPEN-IN-PLACE + AUTO-SYNC
        // ===============================================

        // MEJORA-6: Show/hide context download overlay with progress
        void ShowContextOverlay(string title, string status, int percent = -1)
        {
            ContextDownloadOverlay.Visibility = Visibility.Visible;
            CtxDownloadTitle.Text = title;
            CtxDownloadStatus.Text = status;
            if (percent >= 0) { CtxProgressBar.IsIndeterminate = false; CtxProgressBar.Value = percent; CtxDownloadPercent.Text = $"{percent}%"; }
            else { CtxProgressBar.IsIndeterminate = true; CtxDownloadPercent.Text = ""; }
            var sb = (System.Windows.Media.Animation.Storyboard)FindResource("CtxSpinnerStoryboard");
            sb.Begin(this, true);
        }
        void HideContextOverlay()
        {
            ContextDownloadOverlay.Visibility = Visibility.Collapsed;
            var sb = (System.Windows.Media.Animation.Storyboard)FindResource("CtxSpinnerStoryboard");
            sb.Stop(this);
        }
        private volatile bool _syncCancelled; // checked by parallel tasks
        void CtxCancel_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[DriveV2] CANCEL clicked by user");
            _syncCancelled = true;
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            HideContextOverlay();
            ShowToast("Cancelando sincronizacion...", "warning");
        }

        async Task OpenFileInPlace(DriveFileDb file)
        {
            if (!SupabaseService.Instance.IsDriveStorageConfigured) { ShowToast("R2 Storage no configurado", "warning"); return; }

            try
            {
                // Show overlay for file download
                ShowContextOverlay($"Abriendo {file.FileName}", "Descargando archivo...");
                var localPath = await FileWatcherService.Instance.OpenFile(file, _cts.Token);
                if (localPath == null) { HideContextOverlay(); ShowToast($"Error al descargar {file.FileName}", "error"); return; }

                // MEJORA-6: If assembly file, download all sibling files to same directory
                if (Services.Drive.DriveService.IsAssemblyFile(file.FileName))
                {
                    var contextDir = System.IO.Path.GetDirectoryName(localPath)!;
                    var siblings = _currentFiles.Where(f => f.Id != file.Id).ToList();
                    if (siblings.Count > 0)
                    {
                        int done = 0;
                        ShowContextOverlay("Preparando ensamble", $"Descargando 0 de {siblings.Count} archivos...", 0);
                        var dlCount = await FileWatcherService.Instance.DownloadContext(siblings, contextDir, _cts.Token,
                            onProgress: (completed, total) =>
                            {
                                done = completed;
                                Dispatcher.Invoke(() =>
                                {
                                    var pct = (int)(completed * 100.0 / total);
                                    ShowContextOverlay("Preparando ensamble",
                                        $"Descargando {completed} de {total} archivos...", pct);
                                });
                            });
                        if (dlCount > 0)
                            Debug.WriteLine($"[DriveV2] Assembly context: {dlCount}/{siblings.Count} files downloaded to {contextDir}");
                    }
                }

                // Update overlay while app opens
                ShowContextOverlay($"Abriendo {file.FileName}", "Iniciando aplicacion...");

                try
                {
                    var proc = Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
                    if (proc != null)
                    {
                        await Task.Delay(500);
                        if (proc.HasExited && proc.ExitCode != 0)
                            throw new System.ComponentModel.Win32Exception("Programa asociado no encontrado");
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL \"{localPath}\"") { UseShellExecute = false });
                }

                HideContextOverlay();
                StatusText.Text = $"{file.FileName} abierto - cambios se sincronizan automaticamente";
                ShowToast($"{file.FileName} abierto", "info");
                UpdateSyncStatusBar();
                RenderContent();
            }
            catch (Exception ex)
            {
                HideContextOverlay();
                ShowToast($"No se pudo abrir: {ex.Message}", "error");
            }
        }

        Border MkSyncBadge(SyncState state)
        {
            var (icon, bg, fg, tip) = state switch
            {
                SyncState.Opened => ("\uE70F", Color.FromRgb(0x10, 0xB9, 0x81), Brushes.White, "Abierto localmente"),
                SyncState.Syncing => ("\uE895", Color.FromRgb(0x3B, 0x82, 0xF6), Brushes.White, "Sincronizando..."),
                SyncState.Synced => ("\uE73E", Color.FromRgb(0x10, 0xB9, 0x81), Brushes.White, "Sincronizado"),
                SyncState.Error => ("\uEA39", Color.FromRgb(0xEF, 0x44, 0x44), Brushes.White, "Error de sincronizacion"),
                SyncState.Conflict => ("\uE7BA", Color.FromRgb(0xF5, 0x9E, 0x0B), Brushes.White, "Conflicto detectado"),
                _ => ("", Colors.Transparent, Brushes.Transparent, "")
            };

            var badge = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(bg),
                ToolTip = tip
            };
            badge.Child = new TextBlock
            {
                Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10, Foreground = fg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return badge;
        }

        void OnFileAutoUploaded(string fileName, string status)
        {
            // Use InvokeAsync to avoid blocking the UI thread during OLE drag-drop
            Dispatcher.InvokeAsync(async () =>
            {
                switch (status)
                {
                    case "success":
                        ShowToast($"{fileName} sincronizado", "success");
                        // Auto-refresh current folder (defer if dragging to avoid visual tree rebuild)
                        if (_currentFolderId.HasValue && !_isDragging)
                        {
                            InvalidateStats();
                            await SafeLoad(() => LoadFolder());
                        }
                        break;
                    case "error":
                        ShowToast($"Error al sincronizar {fileName}", "error");
                        break;
                    case "conflict":
                        var entry = GetWatchedEntry(fileName);
                        if (entry != null) HandleConflict(entry, fileName);
                        break;
                }
                UpdateSyncStatusBar();
            });
        }

        void OnFileSyncStateChanged(int fileId, SyncState state)
        {
            // Use InvokeAsync to avoid blocking the UI thread during OLE drag-drop
            Dispatcher.InvokeAsync(() =>
            {
                _syncStates[fileId] = state;
                UpdateSyncStatusBar();
                // Only re-render if this file is visible in current folder (skip during drag to avoid visual tree rebuild)
                if (!_isDragging && _currentFiles.Any(f => f.Id == fileId))
                    RenderContent();
            });
        }

        WatchedFileEntry? GetWatchedEntry(string fileName)
        {
            // Find by fileName in the current files list, then check FileWatcher
            var file = _currentFiles.FirstOrDefault(f => f.FileName == fileName);
            if (file == null) return null;
            // Reconstruct a WatchedFileEntry for conflict handling
            return new WatchedFileEntry { FileId = file.Id, FolderId = file.FolderId, StoragePath = file.StoragePath };
        }

        // V3-E: Auto-resolve conflicts (local always wins)
        void HandleConflict(WatchedFileEntry entry, string fileName)
        {
            _ = Task.Run(async () =>
            {
                var ok = await FileWatcherService.Instance.ForceReupload(entry.FileId);
                Dispatcher.Invoke(() => ShowToast(ok ? $"{fileName} sincronizado (conflicto auto-resuelto)" : $"Error al sincronizar {fileName}", ok ? "success" : "error"));
            });
        }

        void UpdateSyncStatusBar()
        {
            var pending = FileWatcherService.Instance.GetPendingStates();
            if (pending.Count == 0)
            {
                SyncStatusBar.Visibility = Visibility.Collapsed;
                return;
            }

            SyncStatusBar.Visibility = Visibility.Visible;
            var syncing = pending.Count(p => p.State == SyncState.Syncing);
            var errors = pending.Count(p => p.State == SyncState.Error);
            var conflicts = pending.Count(p => p.State == SyncState.Conflict);

            if (syncing > 0)
            {
                SyncStatusIcon.Text = "\uE895";
                SyncStatusText.Text = $"Sincronizando {syncing} archivo{(syncing > 1 ? "s" : "")}...";
                SyncRetryBtn.Visibility = Visibility.Collapsed;
            }
            else if (errors > 0)
            {
                SyncStatusIcon.Text = "\uEA39";
                SyncStatusText.Text = $"{errors} archivo{(errors > 1 ? "s" : "")} con error de sincronizacion";
                SyncRetryBtn.Visibility = Visibility.Visible;
            }
            else if (conflicts > 0)
            {
                SyncStatusIcon.Text = "\uE895";
                SyncStatusText.Text = "Resolviendo conflictos...";
                SyncRetryBtn.Visibility = Visibility.Collapsed;
            }
        }

        // ===============================================
        // V3-A: IMAGE OVERLAY (FULLSCREEN)
        // ===============================================

        void OpenImageOverlay(DriveFileDb file)
        {
            _overlayImageFiles = _currentFiles
                .Where(f => Services.Drive.DriveService.IsImageFile(f.FileName))
                .ToList();
            _overlayCurrentIndex = _overlayImageFiles.FindIndex(f => f.Id == file.Id);
            if (_overlayCurrentIndex < 0) return;

            ShowOverlayImage(_overlayCurrentIndex);
            ImageOverlay.Visibility = Visibility.Visible;
            ImageOverlay.Opacity = 0;
            // Fade-in + scale entrance animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            ImageOverlay.BeginAnimation(OpacityProperty, fadeIn);
            OverlayImage.RenderTransformOrigin = new Point(0.5, 0.5);
            OverlayImage.RenderTransform = new ScaleTransform(0.9, 0.9);
            var scaleAnim = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            OverlayImage.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            OverlayImage.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            ImageOverlay.Focus();
            UpdateOverlayNav();
        }

        void ShowOverlayImage(int index)
        {
            if (index < 0 || index >= _overlayImageFiles.Count) return;
            _overlayCurrentIndex = index;
            var file = _overlayImageFiles[index];
            OverlayFileName.Text = file.FileName;
            OverlayCounter.Text = $"{index + 1} de {_overlayImageFiles.Count}";
            UpdateOverlayNav();

            if (_previewCache.TryGetValue(file.Id, out var cached))
            {
                OverlayImage.Source = cached;
            }
            else
            {
                OverlayImage.Source = null;
                var capturedIndex = index;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var bytes = await SupabaseService.Instance.DownloadDriveFile(file.Id, _cts.Token);
                        if (bytes == null) return;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = new MemoryStream(bytes);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        CachePreview(file.Id, bmp);
                        Dispatcher.Invoke(() => { if (_overlayCurrentIndex == capturedIndex) OverlayImage.Source = bmp; });
                    }
                    catch { /* non-critical */ }
                });
            }
        }

        void UpdateOverlayNav()
        {
            OverlayPrevBtn.Visibility = _overlayCurrentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            OverlayNextBtn.Visibility = _overlayCurrentIndex < _overlayImageFiles.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        void CloseImageOverlay()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (s, e) => ImageOverlay.Visibility = Visibility.Collapsed;
            ImageOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }
        void ImageOverlay_CloseBtn(object sender, RoutedEventArgs e) => CloseImageOverlay();
        void ImageOverlay_BgClick(object sender, MouseButtonEventArgs e) => CloseImageOverlay();
        void ImageOverlay_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { CloseImageOverlay(); e.Handled = true; }
            else if (e.Key == Key.Left && _overlayCurrentIndex > 0) { ShowOverlayImage(_overlayCurrentIndex - 1); e.Handled = true; }
            else if (e.Key == Key.Right && _overlayCurrentIndex < _overlayImageFiles.Count - 1) { ShowOverlayImage(_overlayCurrentIndex + 1); e.Handled = true; }
        }
        void OverlayPrev_Click(object sender, RoutedEventArgs e) { if (_overlayCurrentIndex > 0) ShowOverlayImage(_overlayCurrentIndex - 1); }
        void OverlayNext_Click(object sender, RoutedEventArgs e) { if (_overlayCurrentIndex < _overlayImageFiles.Count - 1) ShowOverlayImage(_overlayCurrentIndex + 1); }

        void CachePreview(int fileId, BitmapImage img)
        {
            if (_previewCache.ContainsKey(fileId))
            {
                var node = _previewLru.Find(fileId);
                if (node != null) { _previewLru.Remove(node); _previewLru.AddFirst(fileId); }
                _previewCache[fileId] = img;
                return;
            }
            while (_previewCache.Count >= MaxPreviewCache && _previewLru.Count > 0)
            {
                var oldest = _previewLru.Last!.Value;
                _previewLru.RemoveLast();
                _previewCache.Remove(oldest);
            }
            _previewLru.AddFirst(fileId);
            _previewCache[fileId] = img;
        }

        // ===============================================
        // V3-A: THUMBNAILS IN GRID
        // ===============================================

        async Task LoadThumbnailAsync(DriveFileDb file, Image targetImage, CancellationToken ct)
        {
            await _thumbnailSemaphore.WaitAsync(ct);
            try
            {
                // Check disk cache
                Directory.CreateDirectory(ThumbnailCacheDir);
                var thumbPath = System.IO.Path.Combine(ThumbnailCacheDir, $"{file.Id}.jpg");
                BitmapImage? bmp = null;

                if (File.Exists(thumbPath))
                {
                    bmp = await Task.Run(() =>
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(thumbPath, UriKind.Absolute);
                        bi.DecodePixelWidth = 200;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }, ct);
                }
                else
                {
                    // Download and create thumbnail
                    var bytes = await SupabaseService.Instance.DownloadDriveFile(file.Id, ct);
                    if (bytes == null || bytes.Length == 0) return;

                    bmp = await Task.Run(() =>
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = new MemoryStream(bytes);
                        bi.DecodePixelWidth = 200;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();

                        // Save to disk cache as JPEG
                        try
                        {
                            var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
                            encoder.Frames.Add(BitmapFrame.Create(bi));
                            using var fs = new FileStream(thumbPath, FileMode.Create);
                            encoder.Save(fs);
                        }
                        catch { /* cache write failure is non-critical */ }

                        return bi;
                    }, ct);
                }

                if (bmp != null && !ct.IsCancellationRequested)
                    Dispatcher.Invoke(() => targetImage.Source = bmp);
            }
            catch (OperationCanceledException) { }
            catch { /* thumbnail load failure is non-critical */ }
            finally { _thumbnailSemaphore.Release(); }
        }

        // MEJORA-5: Load CAD thumbnail via Windows Shell (if file is locally cached)
        async Task LoadCadThumbnailAsync(DriveFileDb file, Image targetImage, CancellationToken ct)
        {
            await _thumbnailSemaphore.WaitAsync(ct);
            try
            {
                Directory.CreateDirectory(ThumbnailCacheDir);
                var thumbPath = System.IO.Path.Combine(ThumbnailCacheDir, $"{file.Id}.jpg");

                // Check disk cache first (same as image thumbnails)
                if (File.Exists(thumbPath))
                {
                    var bmp = await Task.Run(() =>
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(thumbPath, UriKind.Absolute);
                        bi.DecodePixelWidth = 200;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }, ct);
                    if (bmp != null && !ct.IsCancellationRequested)
                        Dispatcher.Invoke(() => targetImage.Source = bmp);
                    return;
                }

                // Check if file has a local copy (from open-in-place or context download)
                var localPath = FileWatcherService.Instance.GetCachedLocalPath(file.Id);
                if (localPath == null) return; // Not cached locally, can't extract shell thumbnail

                // Extract thumbnail via Windows Shell (STA thread, COM interop)
                var shellBmp = await Helpers.ShellThumbnailHelper.GetThumbnailAsync(localPath, 200);
                if (shellBmp == null || ct.IsCancellationRequested) return;

                // Save to disk cache as JPEG for future loads
                await Task.Run(() =>
                {
                    try
                    {
                        var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
                        encoder.Frames.Add(BitmapFrame.Create(shellBmp));
                        using var fs = new FileStream(thumbPath, FileMode.Create);
                        encoder.Save(fs);
                    }
                    catch { /* cache write failure is non-critical */ }
                }, ct);

                if (!ct.IsCancellationRequested)
                    Dispatcher.Invoke(() => targetImage.Source = shellBmp);
            }
            catch (OperationCanceledException) { }
            catch { /* CAD thumbnail failure is non-critical */ }
            finally { _thumbnailSemaphore.Release(); }
        }

        // ===============================================
        // V3-C: MOVE / COPY / DUPLICATE / CLIPBOARD
        // ===============================================

        async Task<int?> ShowFolderPickerAsync(string title, string action, int? excludeFolderId = null)
        {
            // Load folders async BEFORE opening dialog
            var allFolders = await SupabaseService.Instance.GetAllDriveFoldersFlat(_cts.Token);

            var w = new Window
            {
                Title = title, Width = 440, Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, WindowStyle = WindowStyle.None,
                AllowsTransparency = true, Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize
            };

            var card = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(12),
                BorderBrush = BorderColor, BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 24, ShadowDepth = 8, Opacity = 0.12 },
                Margin = new Thickness(16)
            };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new Border { Padding = new Thickness(20, 16, 20, 16), BorderBrush = BorderColor, BorderThickness = new Thickness(0, 0, 0, 1) };
            var hg = new Grid();
            hg.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center });
            var closeBtn = new Button { Content = new TextBlock { Text = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11, Foreground = TextMuted }, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(6) };
            closeBtn.Click += (s, e) => w.Close();
            hg.Children.Add(closeBtn);
            header.Child = hg;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // TreeView (no wrapping ScrollViewer — TreeView has its own)
            var tv = new TreeView { BorderThickness = new Thickness(0), Margin = new Thickness(8), Background = Brushes.White };
            Grid.SetRow(tv, 1);
            root.Children.Add(tv);

            // Build tree from flat folder list
            int? selectedId = null;
            var lookup = allFolders.ToLookup(f => f.ParentId);

            TreeViewItem BuildNode(DriveFolderDb folder)
            {
                var isCurrentOrExcluded = folder.Id == excludeFolderId || folder.Id == _currentFolderId;
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var ico = new Border { Width = 20, Height = 20, Margin = new Thickness(0, 0, 8, 0) };
                ico.Child = MkFolderIco(14, isCurrentOrExcluded ? TextLight : Primary);
                sp.Children.Add(ico);
                var nameTb = new TextBlock { Text = folder.Name, FontSize = 13, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
                nameTb.Foreground = isCurrentOrExcluded ? TextLight : TextPrimary;
                sp.Children.Add(nameTb);
                if (folder.LinkedOrderId.HasValue)
                {
                    var badge = new Border { Background = ActiveBg, CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    badge.Child = new TextBlock { Text = "VINCULADA", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Primary };
                    sp.Children.Add(badge);
                }
                if (isCurrentOrExcluded)
                {
                    var curBadge = new Border { Background = HoverBg, CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    curBadge.Child = new TextBlock { Text = "ACTUAL", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = TextLight };
                    sp.Children.Add(curBadge);
                }

                var item = new TreeViewItem { Header = sp, Tag = folder.Id, IsExpanded = folder.ParentId == null, Padding = new Thickness(4, 4, 4, 4) };
                // Don't disable — just prevent selection (so children remain expandable)
                if (isCurrentOrExcluded)
                    item.Selected += (s, e) => { e.Handled = true; item.IsSelected = false; selectedId = null; };

                foreach (var child in lookup[folder.Id].OrderBy(f => f.Name))
                    item.Items.Add(BuildNode(child));
                return item;
            }

            foreach (var rootFolder in lookup[null].OrderBy(f => f.Name))
                tv.Items.Add(BuildNode(rootFolder));

            tv.SelectedItemChanged += (s, e) =>
            {
                if (tv.SelectedItem is TreeViewItem si && si.IsEnabled)
                    selectedId = si.Tag as int?;
            };

            // Footer
            var footer = new Border { Padding = new Thickness(20, 12, 20, 12), BorderBrush = BorderColor, BorderThickness = new Thickness(0, 1, 0, 0) };
            var fp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "Cancelar", Padding = new Thickness(16, 8, 16, 8), Background = HoverBg, Foreground = TextSecondary, BorderBrush = BorderColor, BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FontWeight = FontWeights.Medium };
            cancelBtn.Click += (s, e) => { selectedId = null; w.Close(); };
            var okBtn = new Button { Content = action, Padding = new Thickness(16, 8, 16, 8), Background = Primary, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0) };
            okBtn.Click += (s, e) => { if (selectedId.HasValue) w.DialogResult = true; };
            fp.Children.Add(cancelBtn);
            fp.Children.Add(okBtn);
            footer.Child = fp;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            card.Child = root;
            w.Content = card;
            w.MouseLeftButtonDown += (s, e) => { try { w.DragMove(); } catch { } };

            return w.ShowDialog() == true ? selectedId : null;
        }

        async Task MoveFilesTo(IEnumerable<DriveFileDb> files)
        {
            var fileList = files.ToList();
            var targetId = await ShowFolderPickerAsync(
                fileList.Count == 1 ? $"Mover '{Tr(fileList[0].FileName, 30)}'" : $"Mover {fileList.Count} archivos",
                "Mover aqui");
            if (!targetId.HasValue) return;
            await SafeLoad(async () =>
            {
                int moved = 0;
                foreach (var f in fileList)
                    if (await SupabaseService.Instance.MoveDriveFile(f.Id, targetId.Value, _cts.Token, _currentUser.Id)) moved++;
                ShowToast(moved == 1 ? $"{fileList[0].FileName} movido" : $"{moved} archivos movidos", "info");
                InvalidateStats(); RefreshSidebarRecents(); await LoadFolder();
            });
        }

        async Task MoveFolderTo(DriveFolderDb folder)
        {
            var targetId = await ShowFolderPickerAsync($"Mover '{Tr(folder.Name, 30)}'", "Mover aqui", excludeFolderId: folder.Id);
            if (!targetId.HasValue) return;
            await SafeLoad(async () =>
            {
                var (canMove, reason) = await SupabaseService.Instance.ValidateDriveFolderMove(folder.Id, targetId.Value, _cts.Token);
                if (!canMove) { ShowToast(reason ?? "No se puede mover", "warning"); return; }
                if (await SupabaseService.Instance.MoveDriveFolder(folder.Id, targetId.Value, _cts.Token))
                {
                    ShowToast($"Carpeta '{folder.Name}' movida", "info");
                    InvalidateStats(); RefreshSidebarRecents(); await LoadFolder();
                }
                else ShowToast("Error al mover carpeta", "error");
            });
        }

        async Task CopyFileTo(DriveFileDb file)
        {
            var targetId = await ShowFolderPickerAsync($"Copiar '{Tr(file.FileName, 30)}'", "Copiar aqui");
            if (!targetId.HasValue) return;
            await SafeLoad(async () =>
            {
                var copy = await SupabaseService.Instance.CopyDriveFile(file.Id, targetId.Value, _cts.Token);
                if (copy != null)
                {
                    UpdateStorageUI(file.FileSize ?? 0);
                    ShowToast($"{file.FileName} copiado", "info");
                    InvalidateStats(); RefreshSidebarRecents(); await LoadFolder();
                }
                else ShowToast("Error al copiar", "error");
            });
        }

        async Task DuplicateFile(DriveFileDb file)
        {
            await SafeLoad(async () =>
            {
                var copy = await SupabaseService.Instance.DuplicateDriveFile(file.Id, _cts.Token);
                if (copy != null)
                {
                    UpdateStorageUI(file.FileSize ?? 0);
                    ShowToast($"Duplicado: {copy.FileName}", "info");
                    InvalidateStats(); RefreshSidebarRecents(); await LoadFolder();
                }
                else ShowToast("Error al duplicar", "error");
            });
        }

        // Clipboard: Ctrl+X / Ctrl+C / Ctrl+V
        void ClipCut()
        {
            var files = GetSelectedFiles();
            if (files.Count == 0) return;
            _clipFiles = files; _clipOp = ClipOp.Cut;
            ShowToast($"{files.Count} archivo{(files.Count > 1 ? "s" : "")} cortado{(files.Count > 1 ? "s" : "")}", "info");
            RenderContent(); // re-render to show cut visual
        }

        void ClipCopy()
        {
            var files = GetSelectedFiles();
            if (files.Count == 0) return;
            _clipFiles = files; _clipOp = ClipOp.Copy;
            ShowToast($"{files.Count} archivo{(files.Count > 1 ? "s" : "")} copiado{(files.Count > 1 ? "s" : "")}", "info");
        }

        async Task ClipPaste()
        {
            if (_clipFiles == null || _clipFiles.Count == 0 || !_currentFolderId.HasValue) return;
            await SafeLoad(async () =>
            {
                int ok = 0;
                foreach (var f in _clipFiles)
                {
                    bool success;
                    if (_clipOp == ClipOp.Cut)
                        success = await SupabaseService.Instance.MoveDriveFile(f.Id, _currentFolderId.Value, _cts.Token, _currentUser.Id);
                    else
                    {
                        var copy = await SupabaseService.Instance.CopyDriveFile(f.Id, _currentFolderId.Value, _cts.Token);
                        success = copy != null;
                        if (success) UpdateStorageUI(f.FileSize ?? 0);
                    }
                    if (success) ok++;
                }
                var verb = _clipOp == ClipOp.Cut ? "movido" : "pegado";
                ShowToast($"{ok} archivo{(ok > 1 ? "s" : "")} {verb}{(ok > 1 ? "s" : "")}", "info");
                if (_clipOp == ClipOp.Cut) _clipFiles = null; // clear after cut-paste
                InvalidateStats(); await LoadFolder();
            });
        }

        List<DriveFileDb> GetSelectedFiles()
        {
            if (_selectedFileIds.Count > 0)
                return _currentFiles.Where(f => _selectedFileIds.Contains(f.Id)).ToList();
            return new();
        }

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
            if (nameBlock == null) { Debug.WriteLine($"[DriveV2] RenFile: TextBlock '{tag}' not found, fallback to prompt"); var n = Prompt("Renombrar", "Nuevo nombre:", f.FileName); if (string.IsNullOrWhiteSpace(n) || n == f.FileName) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFile(f.Id, n, _cts.Token, _currentUser.Id)) await LoadFolder(); }); return; }

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
                _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFile(f.Id, fullName, _cts.Token, _currentUser.Id)) await LoadFolder(); });
            }

            tb.KeyDown += (s, ke) =>
            {
                if (ke.Key == Key.Enter) { ke.Handled = true; Commit(); }
                else if (ke.Key == Key.Escape) { ke.Handled = true; committed = true; _isCreatingFolder = false; parent.Children.Remove(editPanel); nameBlock.Visibility = Visibility.Visible; }
            };
            tb.LostFocus += (s, le) => Commit();
        }
        async Task DelFolder(DriveFolderDb f)
        {
            Debug.WriteLine($"[DriveV2] DelFolder START: id={f.Id} name={f.Name}");
            var confirmed = Confirm($"Eliminar \"{f.Name}\" y todo su contenido?", "Eliminar carpeta", destructive: true);
            if (!confirmed) return;

            ShowContextOverlay("Eliminando carpeta", $"Preparando \"{f.Name}\"...");
            try
            {
                var ok = await SupabaseService.Instance.DeleteDriveFolder(f.Id, _cts.Token);
                HideContextOverlay();
                Debug.WriteLine($"[DriveV2] DelFolder service result: {ok}");
                if (ok)
                {
                    _statsCache.Remove(f.Id); InvalidateStats();
                    ShowToast($"\"{f.Name}\" eliminada", "success");
                    await LoadFolder();
                }
                else
                {
                    ShowToast($"No se pudo eliminar \"{f.Name}\"", "error");
                }
            }
            catch (Exception ex)
            {
                HideContextOverlay();
                Debug.WriteLine($"[DriveV2] DelFolder EXCEPTION: {ex.Message}");
                ShowToast($"Error al eliminar: {ex.Message}", "error");
            }
        }
        async Task DelFile(DriveFileDb f, bool skipConfirm = false)
        {
            if (!skipConfirm && !Confirm($"Eliminar \"{f.FileName}\"?", "Eliminar archivo", destructive: true)) return;
            ShowContextOverlay("Eliminando", $"\"{f.FileName}\"...");
            try
            {
                if (await SupabaseService.Instance.DeleteDriveFile(f.Id, _cts.Token, _currentUser.Id))
                {
                    _selectedFileIds.Remove(f.Id);
                    if (_globalStorageBytes > 0) { _globalStorageBytes -= f.FileSize ?? 0; UpdateStorageUI(); }
                    InvalidateStats(); RefreshSidebarRecents();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] DelFile error: {ex.Message}"); }
            finally { if (!skipConfirm) { HideContextOverlay(); ShowToast($"\"{f.FileName}\" eliminado", "success"); await LoadFolder(); } }
        }

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
                if (total == 0)
                {
                    EmptyStateTitle.Text = "Sin resultados";
                    EmptyStateSubtitle.Text = $"No se encontraron archivos o carpetas para \"{q}\"";
                    EmptyStateActions.Visibility = Visibility.Collapsed;
                    EmptyState.Visibility = Visibility.Visible;
                }
                else EmptyState.Visibility = Visibility.Collapsed;
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
            UpdateMultiSelectBar();
            RenderContent();
        }

        void UpdateMultiSelectBar()
        {
            // Solo mostrar barra de acciones con 2+ archivos seleccionados (Ctrl+Click)
            // Un solo clic solo abre el panel de detalle, sin saturar con la barra
            if (_selectedFileIds.Count > 1)
            {
                MultiSelectBar.Visibility = Visibility.Visible;
                MultiSelectText.Text = $"{_selectedFileIds.Count} archivos seleccionados";
            }
            else MultiSelectBar.Visibility = Visibility.Collapsed;
        }

        void ClearMultiSelect() { _selectedFileIds.Clear(); MultiSelectBar.Visibility = Visibility.Collapsed; }
        void MultiSelectClear_Click(object sender, RoutedEventArgs e) { ClearMultiSelect(); RenderContent(); }

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
            if (!Confirm($"Eliminar {files.Count} archivo(s)?", "Eliminar archivos", destructive: true)) return;
            int ok = 0, done = 0;
            ShowContextOverlay("Eliminando archivos", $"0 de {files.Count}...", 0);
            foreach (var file in files)
            {
                try
                {
                    if (await SupabaseService.Instance.DeleteDriveFile(file.Id, _cts.Token, _currentUser.Id))
                    { ok++; if (_globalStorageBytes > 0) _globalStorageBytes -= file.FileSize ?? 0; }
                }
                catch (Exception ex) { Debug.WriteLine($"[DriveV2] Multi-delete err: {ex.Message}"); }
                done++;
                ShowContextOverlay("Eliminando archivos", $"{done} de {files.Count}...", (int)(done * 100.0 / files.Count));
            }
            HideContextOverlay();
            ShowToast($"{ok} archivo(s) eliminado(s)", "success");
            UpdateStorageUI(); ClearMultiSelect(); InvalidateStats(); RefreshSidebarRecents(); await SafeLoad(() => LoadFolder());
        }
        async void BackToFolders_Click(object sender, RoutedEventArgs e) { if (_breadcrumb.Count >= 2) await SafeLoad(() => NavTo(_breadcrumb[^2].Id)); else await SafeLoad(() => NavigateToRoot()); }
        async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Services.Core.BaseSupabaseService.InvalidateAllCaches();
            _folderCache.Clear();
            _statsCache.Clear();
            await SafeLoad(() => LoadFolder());
        }
        void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
        // DetailClose_Click, DetailDownload_Click, DetailDelete_Click removed — panel eliminated
        void SyncRetry_Click(object sender, RoutedEventArgs e)
        {
            // Retry all errored files
            var errors = FileWatcherService.Instance.GetPendingStates()
                .Where(p => p.State == SyncState.Error)
                .Select(p => p.FileId).ToList();
            foreach (var fid in errors)
                _ = FileWatcherService.Instance.ForceReupload(fid);
            ShowToast($"Reintentando {errors.Count} archivo{(errors.Count > 1 ? "s" : "")}...", "info");
        }
        void Window_DragEnter(object sender, DragEventArgs e)
        {
            Debug.WriteLine($"[DragDrop] DragEnter: hasFolderId={_currentFolderId.HasValue}, hasFileDrop={e.Data.GetDataPresent(DataFormats.FileDrop)}, effects={e.Effects}");
            _isDragging = true;
            // Always accept copy - even if folder not loaded yet, DragOver will keep cursor correct
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            if (_currentFolderId.HasValue && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DragDropOverlay.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                DragDropOverlay.BeginAnimation(OpacityProperty, fadeIn);
                var scaleAnim = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                DragDropScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                DragDropScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
            e.Handled = true;
        }
        void Window_DragLeave(object sender, DragEventArgs e)
        {
            Debug.WriteLine("[DragDrop] DragLeave");
            _isDragging = false;
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s2, e2) => DragDropOverlay.Visibility = Visibility.Collapsed;
            DragDropOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }
        void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
        async void Window_Drop(object sender, DragEventArgs e)
        {
            Debug.WriteLine($"[DragDrop] DROP event fired! hasFolderId={_currentFolderId.HasValue}");
            _isDragging = false;
            DragDropOverlay.Visibility = Visibility.Collapsed; e.Handled = true;
            if (!_currentFolderId.HasValue || !e.Data.GetDataPresent(DataFormats.FileDrop) || !SupabaseService.Instance.IsDriveStorageConfigured)
            {
                Debug.WriteLine($"[DragDrop] DROP rejected: folder={_currentFolderId.HasValue}, fileDrop={e.Data.GetDataPresent(DataFormats.FileDrop)}, r2={SupabaseService.Instance.IsDriveStorageConfigured}");
                return;
            }
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            Debug.WriteLine($"[DragDrop] Paths received: {paths.Length} - {string.Join(", ", paths.Take(5))}");
            if (paths.Length == 0) return;

            // Separate files from folders
            var files = paths.Where(p => File.Exists(p)).ToArray();
            var folders = paths.Where(p => Directory.Exists(p)).ToArray();
            Debug.WriteLine($"[DragDrop] Files={files.Length}, Folders={folders.Length}");

            // If only files dropped (no folders) → existing upload flow
            if (folders.Length == 0 && files.Length > 0)
            {
                await UploadFiles(files);
                return;
            }

            // If folder(s) dropped → folder sync flow
            if (folders.Length > 0)
            {
                await SyncDroppedFolders(folders);
            }
        }

        // ===============================================
        // FOLDER SYNC (drag-drop carpeta completa)
        // ===============================================

        // Data structure for sync plan
        record SyncEntry(string LocalPath, int DriveFolderId, DriveFileDb? Existing);

        async Task SyncDroppedFolders(string[] folderPaths)
        {
            if (!_currentFolderId.HasValue) return;
            var driveParentId = _currentFolderId.Value;

            ShowContextOverlay("Analizando carpeta", "Cargando estructura de Drive...");
            try
            {
                // 1. Load ALL Drive folders AND files in 2 parallel queries (not N)
                var foldersTask = SupabaseService.Instance.GetAllDriveFoldersFlat(_cts.Token);
                var filesTask = SupabaseService.Instance.GetAllDriveFilesFlat(_cts.Token);
                await Task.WhenAll(foldersTask, filesTask);

                var allDriveFolders = foldersTask.Result;
                var allDriveFiles = filesTask.Result;

                // Build in-memory lookups
                var childrenByParent = allDriveFolders.GroupBy(f => f.ParentId ?? -1)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var filesByFolder = allDriveFiles.GroupBy(f => f.FolderId)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(f => f.FileName, f => f, StringComparer.OrdinalIgnoreCase));

                // 2. Scan local folders — pure in-memory matching (no HTTP calls)
                ShowContextOverlay("Analizando carpeta", "Comparando archivos...");
                var newFolders = new List<(string localPath, int driveParentId, string name)>();
                var newFiles = new List<SyncEntry>();
                var existingFiles = new List<SyncEntry>();
                int junkSkipped = 0;

                await Task.Run(() =>
                {
                    foreach (var folderPath in folderPaths)
                        ScanLocalFolder(folderPath, driveParentId, childrenByParent, filesByFolder,
                            newFolders, newFiles, existingFiles, ref junkSkipped);
                });

                var totalNew = newFiles.Count;
                var totalExisting = existingFiles.Count;

                if (totalNew == 0 && totalExisting == 0)
                {
                    HideContextOverlay();
                    ShowToast("No hay archivos para sincronizar", "info");
                    return;
                }

                // 3. Only ask user if there are duplicates — otherwise sync immediately
                bool overwrite = false;
                if (totalExisting > 0)
                {
                    HideContextOverlay();
                    var action = await ShowSyncDialog(newFolders.Count, totalNew, totalExisting, junkSkipped);
                    if (action == "cancel") return;
                    overwrite = action == "overwrite";
                }

                // 4. Execute sync
                _syncCancelled = false;
                await ExecuteFolderSync(newFolders, newFiles, existingFiles, overwrite);

                // 5. Refresh current view
                if (!_syncCancelled)
                {
                    InvalidateStats();
                    _folderCache.Clear();
                    await SafeLoad(() => LoadFolder());
                    ShowToast("Sincronizacion completada", "success");
                }
                else
                {
                    // Still refresh to show what was partially synced
                    InvalidateStats();
                    _folderCache.Clear();
                    await SafeLoad(() => LoadFolder());
                }
            }
            catch (Exception ex)
            {
                HideContextOverlay();
                if (!_syncCancelled)
                    ShowToast($"Error en sincronizacion: {ex.Message}", "error");
            }
        }

        void ScanLocalFolder(
            string localPath, int driveParentId,
            Dictionary<int, List<DriveFolderDb>> childrenByParent,
            Dictionary<int, Dictionary<string, DriveFileDb>> filesByFolder,
            List<(string, int, string)> newFolders,
            List<SyncEntry> newFiles,
            List<SyncEntry> existingFiles,
            ref int junkSkipped)
        {
            var folderName = System.IO.Path.GetFileName(localPath);

            // Find matching Drive folder under the parent (in-memory)
            DriveFolderDb? matchingDriveFolder = null;
            if (childrenByParent.TryGetValue(driveParentId, out var siblings))
                matchingDriveFolder = siblings.FirstOrDefault(f =>
                    string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase));

            int currentDriveFolderId;
            if (matchingDriveFolder != null)
            {
                currentDriveFolderId = matchingDriveFolder.Id;
            }
            else
            {
                currentDriveFolderId = -(newFolders.Count + 1);
                newFolders.Add((localPath, driveParentId, folderName));
            }

            // Get existing files from pre-loaded in-memory lookup (0 HTTP calls)
            var driveFileNames = (matchingDriveFolder != null && filesByFolder.TryGetValue(matchingDriveFolder.Id, out var cached))
                ? cached : new Dictionary<string, DriveFileDb>(StringComparer.OrdinalIgnoreCase);

            // Scan files in this local folder
            try
            {
                foreach (var filePath in Directory.GetFiles(localPath))
                {
                    var fileName = System.IO.Path.GetFileName(filePath);
                    if (IsJunkFile(fileName)) { junkSkipped++; continue; }

                    if (matchingDriveFolder != null && driveFileNames.TryGetValue(fileName, out var existing))
                    {
                        existingFiles.Add(new SyncEntry(filePath, matchingDriveFolder.Id, existing));
                    }
                    else if (matchingDriveFolder != null)
                    {
                        newFiles.Add(new SyncEntry(filePath, matchingDriveFolder.Id, null));
                    }
                    else
                    {
                        // Folder doesn't exist yet — files will be uploaded after folder creation
                        // Use localPath as key to resolve folder ID later
                        newFiles.Add(new SyncEntry(filePath, currentDriveFolderId, null));
                    }
                }
            }
            catch (UnauthorizedAccessException) { /* Skip folders we can't read */ }

            // Recurse into subdirectories
            try
            {
                foreach (var subDir in Directory.GetDirectories(localPath))
                {
                    var subName = System.IO.Path.GetFileName(subDir);
                    if (subName.StartsWith(".") || subName.StartsWith("~$")) continue; // Skip hidden/temp dirs
                    ScanLocalFolder(subDir, currentDriveFolderId, childrenByParent, filesByFolder,
                        newFolders, newFiles, existingFiles, ref junkSkipped);
                }
            }
            catch (UnauthorizedAccessException) { /* Skip folders we can't read */ }
        }

        Task<string> ShowSyncDialog(int newFolders, int newFiles, int existingFiles, int junkSkipped)
        {
            var tcs = new TaskCompletionSource<string>();
            var dlg = new Window
            {
                Width = 440, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, Background = Brushes.Transparent,
                AllowsTransparency = true
            };
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 32, ShadowDepth = 12, Opacity = 0.15 }, Margin = new Thickness(16) };
            var stack = new StackPanel { Margin = new Thickness(28) };

            // Icon + Title
            var iconBorder = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(26), Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
            iconBorder.Child = new TextBlock { Text = "\uE895", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22, Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(iconBorder);
            stack.Children.Add(new TextBlock { Text = "Archivos existentes detectados", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });

            // Summary pills
            var pillsPanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            if (newFolders > 0) pillsPanel.Children.Add(MkSyncPill("\uE8B7", $"{newFolders} carpetas nuevas", "#10B981", "#F0FDF4"));
            if (newFiles > 0) pillsPanel.Children.Add(MkSyncPill("\uE8E5", $"{newFiles} archivos nuevos", "#3B82F6", "#EFF6FF"));
            if (existingFiles > 0) pillsPanel.Children.Add(MkSyncPill("\uE7BA", $"{existingFiles} ya existen", "#F59E0B", "#FFFBEB"));
            if (junkSkipped > 0) pillsPanel.Children.Add(MkSyncPill("\uE711", $"{junkSkipped} omitidos", "#94A3B8", "#F8FAFC"));
            stack.Children.Add(pillsPanel);

            // Separator
            stack.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), Height = 1, Margin = new Thickness(0, 12, 0, 16) });

            // Question
            stack.Children.Add(new TextBlock { Text = "¿Que deseas hacer con los archivos que ya existen?", FontSize = 13.5, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 16) });

            // Buttons row
            var buttonsPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };

            // Overwrite button (warning style with border)
            var overwriteBtn = MkStyledBtn($"Sobrescribir {existingFiles} existente(s)",
                WarningBg, BH("#92400E"), BH("#FEF3C7"), BH("#FDE68A"), BH("#FDE68A"));
            overwriteBtn.Click += (_, _) => { tcs.SetResult("overwrite"); dlg.Close(); };
            buttonsPanel.Children.Add(overwriteBtn);

            // Skip button (primary style)
            var skipBtn = MkStyledBtn(newFiles > 0 ? $"Solo subir {newFiles} nuevo(s)" : "Omitir todos",
                Primary, Brushes.White as SolidColorBrush ?? Fr(0xFF, 0xFF, 0xFF), BH("#1E40AF"), BH("#1E3A8A"));
            skipBtn.FontWeight = FontWeights.SemiBold;
            skipBtn.Click += (_, _) => { tcs.SetResult("skip"); dlg.Close(); };
            buttonsPanel.Children.Add(skipBtn);

            // Cancel button (ghost style)
            var cancelBtn = MkStyledBtn("Cancelar",
                new SolidColorBrush(Colors.Transparent), TextLight, HoverBg, BorderLight);
            cancelBtn.Click += (_, _) => { tcs.SetResult("cancel"); dlg.Close(); };
            buttonsPanel.Children.Add(cancelBtn);

            stack.Children.Add(buttonsPanel);

            card.Child = stack; dlg.Content = card;
            dlg.Closed += (_, _) => { if (!tcs.Task.IsCompleted) tcs.SetResult("cancel"); };
            dlg.MouseLeftButtonDown += (s, e) => { try { dlg.DragMove(); } catch { } };
            dlg.ShowDialog();
            return tcs.Task;
        }

        Border MkSyncPill(string icon, string text, string color, string bgColor)
        {
            var pill = new Border { CornerRadius = new CornerRadius(8), Background = BH(bgColor), Padding = new Thickness(10, 6, 12, 6), Margin = new Thickness(3) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Foreground = BH(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = BH(color), VerticalAlignment = VerticalAlignment.Center });
            pill.Child = sp;
            return pill;
        }

        async Task ExecuteFolderSync(
            List<(string localPath, int driveParentId, string name)> foldersToCreate,
            List<SyncEntry> newFiles,
            List<SyncEntry> existingFiles,
            bool overwrite)
        {
            _syncCancelled = false;
            var tempIdMap = new Dictionary<int, int>();
            int totalOps = foldersToCreate.Count + newFiles.Count + (overwrite ? existingFiles.Count : 0);
            int completed = 0;

            // Phase 1: Create folders — PARALLEL BY LEVEL
            if (foldersToCreate.Count > 0 && !_syncCancelled)
            {
                ShowContextOverlay("Sincronizando", $"Creando {foldersToCreate.Count} carpeta(s)...", 0);
                var folderWithTempId = foldersToCreate.Select((f, i) => (f.localPath, f.driveParentId, f.name, tempId: -(i + 1))).ToList();
                var remaining = new List<(string localPath, int driveParentId, string name, int tempId)>(folderWithTempId);
                var folderSemaphore = new SemaphoreSlim(10);

                while (remaining.Count > 0 && !_syncCancelled)
                {
                    var ready = remaining.Where(f =>
                        f.driveParentId > 0 || tempIdMap.ContainsKey(f.driveParentId)).ToList();
                    if (ready.Count == 0) ready = remaining.Take(1).ToList();
                    foreach (var r in ready) remaining.Remove(r);

                    var levelTasks = ready.Select(async folder =>
                    {
                        if (_syncCancelled) return;
                        await folderSemaphore.WaitAsync();
                        try
                        {
                            if (_syncCancelled) return;
                            var resolvedParentId = folder.driveParentId < 0
                                ? tempIdMap.GetValueOrDefault(folder.driveParentId, _currentFolderId!.Value)
                                : folder.driveParentId;
                            var created = await SupabaseService.Instance.CreateDriveFolder(
                                folder.name, resolvedParentId, _currentUser.Id, CancellationToken.None);
                            if (created != null) { lock (tempIdMap) { tempIdMap[folder.tempId] = created.Id; } }
                            var c = Interlocked.Increment(ref completed);
                            Dispatcher.BeginInvoke(() => { if (!_syncCancelled) ShowContextOverlay("Creando carpetas",
                                $"{c} de {foldersToCreate.Count} carpetas...", (int)(c * 100.0 / totalOps)); });
                        }
                        catch { /* swallow */ }
                        finally { folderSemaphore.Release(); }
                    });
                    await Task.WhenAll(levelTasks);
                }
            }

            if (_syncCancelled) return;

            // Phase 2: Build file upload list
            var filesToUpload = newFiles.Select(f =>
            {
                var folderId = f.DriveFolderId < 0 ? tempIdMap.GetValueOrDefault(f.DriveFolderId, _currentFolderId!.Value) : f.DriveFolderId;
                return (f.LocalPath, FolderId: folderId);
            }).ToList();

            // Phase 3: If overwrite, delete old files first
            if (overwrite && existingFiles.Count > 0 && !_syncCancelled)
            {
                Dispatcher.BeginInvoke(() => ShowContextOverlay("Sincronizando", "Eliminando versiones anteriores...", -1));
                var delSemaphore = new SemaphoreSlim(5);
                var delTasks = existingFiles.Where(f => f.Existing != null).Select(async f =>
                {
                    if (_syncCancelled) return;
                    await delSemaphore.WaitAsync();
                    try { if (!_syncCancelled) await SupabaseService.Instance.DeleteDriveFile(f.Existing!.Id, CancellationToken.None, _currentUser.Id); }
                    catch { }
                    finally { delSemaphore.Release(); }
                });
                await Task.WhenAll(delTasks);
                filesToUpload.AddRange(existingFiles.Select(f => (f.LocalPath, f.DriveFolderId)));
            }

            // Phase 4: Upload files in parallel
            if (filesToUpload.Count > 0 && !_syncCancelled)
            {
                var semaphore = new SemaphoreSlim(5);
                int uploaded = 0, failed = 0;

                // Process in batches to allow cancel between batches
                var batch = new List<Task>();
                foreach (var item in filesToUpload)
                {
                    if (_syncCancelled) break;
                    var localItem = item;
                    batch.Add(Task.Run(async () =>
                    {
                        if (_syncCancelled) return;
                        await semaphore.WaitAsync();
                        try
                        {
                            if (_syncCancelled) return;
                            await SupabaseService.Instance.UploadDriveFile(localItem.LocalPath, localItem.FolderId, _currentUser.Id, CancellationToken.None);
                            Interlocked.Increment(ref uploaded);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Sync] Upload failed: {System.IO.Path.GetFileName(localItem.LocalPath)} - {ex.Message}");
                            Interlocked.Increment(ref failed);
                        }
                        finally
                        {
                            semaphore.Release();
                            var c = Interlocked.Increment(ref completed);
                            Dispatcher.BeginInvoke(() => { if (!_syncCancelled) ShowContextOverlay("Subiendo archivos",
                                $"{uploaded + failed} de {filesToUpload.Count} archivos...",
                                (int)(c * 100.0 / totalOps)); });
                        }
                    }));
                }
                await Task.WhenAll(batch);

                if (failed > 0 && !_syncCancelled)
                    ShowToast($"{uploaded} subido(s), {failed} fallido(s)", "warning");
            }

            if (!_syncCancelled) HideContextOverlay();
        }

        // Upload with ghost cards in-place (no side panel)
        // MEJORA-2: Filter junk files that should never be uploaded to Drive
        static bool IsJunkFile(string fileName)
        {
            if (fileName.StartsWith("~$")) return true;                     // Office/SolidWorks lock files
            if (fileName.StartsWith(".", StringComparison.Ordinal)) return true; // Hidden system files
            var ext = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".db" or ".lck" or ".tmp" or ".bak";              // Thumbs.db, lock files, temps
        }

        async Task UploadFiles(string[] filePaths)
        {
            if (!_currentFolderId.HasValue) return;

            // MEJORA-2: Filter out junk files before uploading
            var cleanPaths = filePaths.Where(fp => !IsJunkFile(System.IO.Path.GetFileName(fp))).ToArray();
            var skipped = filePaths.Length - cleanPaths.Length;
            if (skipped > 0)
                ShowToast($"{skipped} archivo(s) omitido(s) (temporales/basura)", "warning");
            if (cleanPaths.Length == 0) { ShowToast("No hay archivos validos para subir", "warning"); return; }
            filePaths = cleanPaths;

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
                    if (border != null) { border.Opacity = 1.0; border.BorderBrush = Success; }
                    if (progressBar != null) { progressBar.IsIndeterminate = false; progressBar.Value = 100; progressBar.Foreground = Success; }
                    if (statusTb != null) { statusTb.Text = "\u2713 Listo"; statusTb.Foreground = Success; }
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
            if (ImageOverlay.Visibility == Visibility.Visible) { base.OnKeyDown(e); return; }
            if (_isCreatingFolder) { base.OnKeyDown(e); return; }

            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (e.Key == Key.Escape)
            {
                if (_clipFiles != null) { _clipFiles = null; ShowToast("Operacion cancelada", "info"); RenderContent(); }
                else if (_selectedFileIds.Count > 0) { ClearMultiSelect(); RenderContent(); }
                else if (_breadcrumb.Count > 1) BackToFolders_Click(this, new RoutedEventArgs());
                else Close();
            }
            // V3-C: Clipboard shortcuts
            else if (ctrl && e.Key == Key.X) { ClipCut(); e.Handled = true; }
            else if (ctrl && e.Key == Key.C) { ClipCopy(); e.Handled = true; }
            else if (ctrl && e.Key == Key.V) { _ = ClipPaste(); e.Handled = true; }
            // G1: Explorer-style keyboard shortcuts
            else if (e.Key == Key.F2) { e.Handled = true; KeyRename(); }
            else if (e.Key == Key.Delete) { e.Handled = true; _ = KeyDelete(); }
            else if (e.Key == Key.F5) { e.Handled = true; RefreshButton_Click(this, new RoutedEventArgs()); }
            else if (ctrl && e.Key == Key.N) { e.Handled = true; NewFolder_Click(this, new RoutedEventArgs()); }
            else if (ctrl && e.Key == Key.U) { e.Handled = true; Upload_Click(this, new RoutedEventArgs()); }
            else if (ctrl && e.Key == Key.F) { e.Handled = true; SearchBox.Focus(); }
            else if (ctrl && e.Key == Key.A) { e.Handled = true; _selectedFileIds.Clear(); foreach (var f in _currentFiles) _selectedFileIds.Add(f.Id); UpdateMultiSelectBar(); RenderContent(); }
            else if (e.Key == Key.Back) { e.Handled = true; BackToFolders_Click(this, new RoutedEventArgs()); }
            else if (e.Key == Key.Enter) { e.Handled = true; KeyOpen(); }
            base.OnKeyDown(e);
        }

        void KeyRename()
        {
            if (_selectedFileIds.Count == 1) { var f = _currentFiles.FirstOrDefault(x => x.Id == _selectedFileIds.First()); if (f != null) RenFile(f); }
            else if (_selectedFileIds.Count == 0 && _currentFolders.Count == 1) RenFolder(_currentFolders[0]);
        }

        async Task KeyDelete()
        {
            if (_selectedFileIds.Count > 0)
            {
                var files = _currentFiles.Where(f => _selectedFileIds.Contains(f.Id)).ToList();
                if (files.Count == 0) return;
                if (files.Count == 1) { await DelFile(files[0]); await LoadFolder(); return; }
                if (!Confirm($"Eliminar {files.Count} archivos seleccionados?", "Eliminar archivos", destructive: true)) return;
                int done = 0;
                ShowContextOverlay("Eliminando archivos", $"0 de {files.Count}...", 0);
                foreach (var f in files)
                {
                    await DelFile(f, skipConfirm: true);
                    done++;
                    ShowContextOverlay("Eliminando archivos", $"{done} de {files.Count}...", (int)(done * 100.0 / files.Count));
                }
                HideContextOverlay();
                ShowToast($"{done} archivo(s) eliminado(s)", "success");
                await LoadFolder();
            }
        }

        void KeyOpen()
        {
            if (_selectedFileIds.Count == 1)
            {
                var f = _currentFiles.FirstOrDefault(x => x.Id == _selectedFileIds.First());
                if (f != null) _ = OpenFileInPlace(f);
            }
            else if (_selectedFileIds.Count == 0 && _currentFolders.Count > 0)
            {
                // If no files selected, open first folder
            }
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
        // V3-D: DOWNLOAD FOLDER AS ZIP
        // ===============================================

        async Task DownloadFolderAsZip(DriveFolderDb folder)
        {
            await SafeLoad(async () =>
            {
                // Collect all files recursively
                ShowToast($"Preparando ZIP de \"{folder.Name}\"...", "info", 5000);
                var files = await SupabaseService.Instance.CollectDriveFilesRecursive(folder.Id, _cts.Token);

                if (files.Count == 0) { ShowToast("La carpeta esta vacia", "warning"); return; }
                if (files.Count > 50) { if (!Confirm($"Esta carpeta contiene {files.Count} archivos. Continuar?")) return; }

                var totalSize = files.Sum(f => f.FileSize ?? 0);
                if (totalSize > 500 * 1024 * 1024) { if (!Confirm($"El tamano total es {Services.Drive.DriveService.FormatFileSize(totalSize)}. Continuar?")) return; }

                // SaveFileDialog
                var sfd = new SaveFileDialog
                {
                    FileName = $"{folder.Name}.zip",
                    Filter = "Archivo ZIP|*.zip",
                    Title = "Guardar carpeta como ZIP"
                };
                if (sfd.ShowDialog() != true) return;

                var zipPath = sfd.FileName;
                var completed = 0;
                var failed = 0;
                var semaphore = new SemaphoreSlim(3);

                using (var zipStream = new FileStream(zipPath, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    // Build folder path map for proper ZIP structure
                    var folderPaths = new Dictionary<int, string> { { folder.Id, "" } };

                    async Task BuildFolderMap(int parentId, string parentPath)
                    {
                        var subs = await SupabaseService.Instance.GetDriveChildFolders(parentId, _cts.Token);
                        foreach (var sub in subs)
                        {
                            var subPath = string.IsNullOrEmpty(parentPath) ? sub.Name : $"{parentPath}/{sub.Name}";
                            folderPaths[sub.Id] = subPath;
                            await BuildFolderMap(sub.Id, subPath);
                        }
                    }
                    await BuildFolderMap(folder.Id, "");

                    // Download files in parallel and add to ZIP
                    var tasks = files.Select(async file =>
                    {
                        await semaphore.WaitAsync(_cts.Token);
                        try
                        {
                            var folderPath = folderPaths.ContainsKey(file.FolderId)
                                ? folderPaths[file.FolderId] : "";
                            var entryName = string.IsNullOrEmpty(folderPath) ? file.FileName : $"{folderPath}/{file.FileName}";

                            using var ms = new MemoryStream();
                            var ok = await SupabaseService.Instance.DownloadDriveFileToStream(file.StoragePath, ms, _cts.Token);
                            if (ok)
                            {
                                ms.Position = 0;
                                lock (archive)
                                {
                                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                                    using var entryStream = entry.Open();
                                    ms.CopyTo(entryStream);
                                }
                                Interlocked.Increment(ref completed);
                            }
                            else Interlocked.Increment(ref failed);
                        }
                        finally { semaphore.Release(); }
                    });
                    await Task.WhenAll(tasks);
                }

                var msg = failed > 0
                    ? $"ZIP descargado: {completed} de {files.Count} archivos"
                    : $"ZIP descargado: {folder.Name}.zip ({Services.Drive.DriveService.FormatFileSize(totalSize)}, {files.Count} archivos)";
                ShowToast(msg, failed > 0 ? "warning" : "success", 4000);
            });
        }

        // ===============================================
        // TOAST NOTIFICATIONS (replaces MessageBox)
        // ===============================================
        private System.Windows.Threading.DispatcherTimer? _toastTimer;

        void ShowToast(string message, string type = "info", int durationMs = 3000)
        {
            Dispatcher.Invoke(() =>
            {
                // Icon + accent color by type (light pill style)
                var (icon, accent) = type switch
                {
                    "success" => ("\uE73E", Color.FromRgb(0x10, 0xB9, 0x81)),  // emerald-500
                    "error" => ("\uEA39", Color.FromRgb(0xEF, 0x44, 0x44)),    // red-500
                    "warning" => ("\uE7BA", Color.FromRgb(0xF5, 0x9E, 0x0B)),  // amber-500
                    _ => ("\uE946", Color.FromRgb(0x3B, 0x82, 0xF6))           // blue-500
                };
                var accentBrush = new SolidColorBrush(accent);
                ToastIcon.Text = icon;
                ToastIcon.Foreground = accentBrush;
                ToastText.Text = message;
                ToastAccent.Background = accentBrush;
                ToastPanel.Visibility = Visibility.Visible;
                ToastPanel.Opacity = 0;

                // Slide-down + fade in
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var slideIn = new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                ToastPanel.BeginAnimation(OpacityProperty, fadeIn);
                ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);

                // Auto-dismiss
                _toastTimer?.Stop();
                _toastTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer.Stop();
                    DismissToast();
                };
                _toastTimer.Start();
            });
        }

        void DismissToast()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            var slideOut = new DoubleAnimation(0, -10, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (s2, e2) => ToastPanel.Visibility = Visibility.Collapsed;
            ToastPanel.BeginAnimation(OpacityProperty, fadeOut);
            ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);
        }

        void ToastClose_Click(object sender, MouseButtonEventArgs e)
        {
            _toastTimer?.Stop();
            DismissToast();
        }

        /// <summary>Custom confirmation dialog matching app design. Returns true if confirmed.</summary>
        bool Confirm(string message, string title = "Confirmar", bool destructive = false)
        {
            bool result = false;
            var w = new Window
            {
                Width = 420, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(0x66, 0x0F, 0x17, 0x2A)) // Backdrop dim
            };

            var card = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 24, ShadowDepth = 8, Opacity = 0.12 },
                Margin = new Thickness(16),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.95, 0.95)
            };
            var p = new StackPanel { Margin = new Thickness(28) };

            // Icon
            var iconColor = destructive ? "#EF4444" : "#F59E0B";
            var iconBg = destructive ? "#FEF2F2" : "#FFFBEB";
            var iconChar = destructive ? "\uE74D" : "\uE7BA";
            var iconBorder = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(24), Background = BH(iconBg), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
            iconBorder.Child = new TextBlock { Text = iconChar, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 20, Foreground = BH(iconColor), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            p.Children.Add(iconBorder);

            // Title
            p.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) });
            // Message
            p.Children.Add(new TextBlock { Text = message, FontSize = 13.5, Foreground = TextMuted, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });

            // Buttons (styled with CornerRadius + hover)
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var cancel = MkStyledBtn("Cancelar", BorderLight, TextSecondary, new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), SlateLight);
            cancel.MinWidth = 100; cancel.Margin = new Thickness(0);
            cancel.Click += (s, e) => w.Close();

            var okBg = destructive ? Danger : Primary;
            var okHover = destructive ? Fr(0xDC, 0x26, 0x26) : Fr(0x1E, 0x40, 0xAF);
            var okPressed = destructive ? Fr(0xB9, 0x1C, 0x1C) : Fr(0x1E, 0x3A, 0x8A);
            var ok = MkStyledBtn(destructive ? "Eliminar" : "Confirmar", okBg, Fr(0xFF, 0xFF, 0xFF), okHover, okPressed);
            ok.FontWeight = FontWeights.SemiBold; ok.MinWidth = 100; ok.Margin = new Thickness(10, 0, 0, 0);
            ok.Click += (s, e) => { result = true; w.Close(); };

            bp.Children.Add(cancel); bp.Children.Add(ok); p.Children.Add(bp);
            card.Child = p; w.Content = card;

            w.MouseLeftButtonDown += (s, e) => { try { w.DragMove(); } catch { } };
            w.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) w.Close(); };

            // Entrance animation: scale 0.95->1.0 + opacity 0->1
            w.Loaded += (s, e) =>
            {
                w.Activate(); w.Focus();
                var scaleX = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var scaleY = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                card.BeginAnimation(OpacityProperty, fadeIn);
            };

            w.ShowDialog();
            return result;
        }

        static string Tr(string? v, int m) => string.IsNullOrEmpty(v) ? "" : v.Length <= m ? v : v[..m] + "...";
        static MenuItem MI(string h, RoutedEventHandler hnd) { var m = new MenuItem { Header = h }; m.Click += hnd; return m; }
        static Color CH(string h) { h = h.TrimStart('#'); return Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16)); }
        static SolidColorBrush BH(string h) => new(CH(h));

        /// <summary>Creates a styled button with CornerRadius and hover/pressed states for use in code-behind dialogs.</summary>
        static Button MkStyledBtn(string text, SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush hoverBg, SolidColorBrush? pressedBg = null, SolidColorBrush? borderBrush = null)
        {
            var btn = new Button { Content = text, FontSize = 13, FontWeight = FontWeights.Medium, Cursor = Cursors.Hand, Padding = new Thickness(16, 10, 16, 10), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            var bgColor = bg.Color; var hoverColor = hoverBg.Color; var pressedColor = pressedBg?.Color ?? hoverColor;
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "border");
            borderFactory.SetValue(Border.BackgroundProperty, bg);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(16, 10, 16, 10));
            if (borderBrush != null) { borderFactory.SetValue(Border.BorderBrushProperty, borderBrush); borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1)); }
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);
            template.VisualTree = borderFactory;
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "border"));
            template.Triggers.Add(hoverTrigger);
            var pressedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, pressedBg ?? hoverBg, "border"));
            template.Triggers.Add(pressedTrigger);
            btn.Template = template;
            btn.Foreground = fg;
            return btn;
        }

        // ===============================================
        // DIAGNOSTICO DE INTEGRIDAD (solo dev)
        // ===============================================
        async Task RunDiagnoseOrphans()
        {
            ShowContextOverlay("Diagnosticando", "Comparando R2 vs BD...");
            try
            {
                var (r2Orphans, bdOrphans, matched) = await SupabaseService.Instance.DiagnoseDriveOrphans(_cts.Token);
                HideContextOverlay();

                Debug.WriteLine($"[Diagnose] R2={matched + r2Orphans.Count} BD={matched + bdOrphans.Count} Matched={matched} R2Orphans={r2Orphans.Count} BDOrphans={bdOrphans.Count}");

                if (r2Orphans.Count == 0 && bdOrphans.Count == 0)
                {
                    ShowToast($"Todo limpio: {matched} archivos sincronizados, 0 huerfanos", "success");
                    return;
                }

                var msg = $"R2 vs BD: {matched} sincronizados\n\n";
                if (r2Orphans.Count > 0) msg += $"R2 huerfanos (blobs sin BD): {r2Orphans.Count}\n";
                if (bdOrphans.Count > 0) msg += $"BD huerfanos (registros sin blob): {bdOrphans.Count}\n";

                if (r2Orphans.Count > 0)
                {
                    msg += $"\nLimpiar {r2Orphans.Count} blobs huerfanos de R2?";
                    if (Confirm(msg, "Diagnostico de integridad"))
                    {
                        ShowContextOverlay("Limpiando", $"Eliminando {r2Orphans.Count} blobs huerfanos...");
                        var cleaned = await SupabaseService.Instance.CleanDriveR2Orphans(r2Orphans);
                        HideContextOverlay();
                        ShowToast($"{cleaned} blobs huerfanos eliminados de R2", "success");
                    }
                }
                else
                {
                    ShowToast($"BD: {bdOrphans.Count} registros sin blob en R2", "warning");
                }
            }
            catch (Exception ex)
            {
                HideContextOverlay();
                ShowToast($"Error en diagnostico: {ex.Message}", "error");
            }
        }
    }

    static class DriveV2Ext { public static T Also<T>(this T o, Action<T> a) { a(o); return o; } }
}
