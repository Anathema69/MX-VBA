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
        private string _viewMode = "grid";
        private DriveFileDb? _selectedFile = null;
        private readonly List<Border> _navItems = new();

        // Caches
        private readonly Dictionary<int, string> _userNameCache = new();
        private readonly Dictionary<int, (int fileCount, int subCount, long totalSize)> _statsCache = new();
        private readonly Dictionary<int, (string Po, string Client, string Detail)> _orderInfoCache = new();

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
            Loaded += async (s, e) => { InitSidebar(); UpdateViewToggle(); await SafeLoad(() => NavigateToRoot()); };
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
        private void InitSidebar()
        {
            foreach (var (id, ico, lbl) in new[] { ("all", "\uE80F", "Todos los archivos"), ("recent", "\uE823", "Recientes"), ("starred", "\uE734", "Destacados"), ("trash", "\uE74D", "Papelera") })
            { var it = MkNav(id, ico, lbl); NavPanel.Children.Add(it); _navItems.Add(it); }
            SetNav("all");
            foreach (var (id, clr, lbl) in new[] { ("pdf", "#EF4444", "PDFs"), ("img", "#10B981", "Imagenes"), ("cad", "#8B5CF6", "Archivos CAD"), ("xls", "#10B981", "Hojas de calculo"), ("vid", "#EC4899", "Videos") })
                FilterPanel.Children.Add(MkFilter(clr, lbl, "--"));
        }

        Border MkNav(string id, string ico, string lbl)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = id };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = ico, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = TextSecondary });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 14, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Foreground = TextSecondary });
            b.Child = sp;
            b.MouseLeftButtonDown += async (s, e) => { if (id == "all") { SetNav(id); _selectedFile = null; HideDetail(); await SafeLoad(() => NavigateToRoot()); } };
            b.MouseEnter += (s, e) => { if (b.Tag as string != _activeNav) b.Background = HoverBg; };
            b.MouseLeave += (s, e) => { if (b.Tag as string != _activeNav) b.Background = Brushes.Transparent; };
            return b;
        }

        void SetNav(string id) { _activeNav = id; foreach (var it in _navItems) { var a = it.Tag as string == id; it.Background = a ? ActiveBg : Brushes.Transparent; if (it.Child is StackPanel sp) foreach (var c in sp.Children.OfType<TextBlock>()) c.Foreground = a ? Primary : TextSecondary; } }

        UIElement MkFilter(string clr, string lbl, string cnt)
        {
            var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent };
            var g = new Grid(); var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = BH(clr), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 13, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center });
            g.Children.Add(sp); g.Children.Add(new TextBlock { Text = cnt, FontSize = 12, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
            b.Child = g; b.MouseEnter += (s, e) => b.Background = HoverBg; b.MouseLeave += (s, e) => b.Background = Brushes.Transparent; return b;
        }

        // P12: Update filter counts
        void UpdateFilterCounts()
        {
            var counts = new[] {
                _currentFiles.Count(f => f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)),
                _currentFiles.Count(f => new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }.Any(x => f.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase))),
                _currentFiles.Count(f => new[] { ".dwg", ".dxf", ".step", ".stp" }.Any(x => f.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase))),
                _currentFiles.Count(f => new[] { ".xls", ".xlsx", ".csv" }.Any(x => f.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase))),
                _currentFiles.Count(f => new[] { ".mp4", ".avi", ".mkv", ".mov" }.Any(x => f.FileName.EndsWith(x, StringComparison.OrdinalIgnoreCase))),
            };
            for (int i = 0; i < FilterPanel.Children.Count && i < counts.Length; i++)
                if (FilterPanel.Children[i] is Border b && b.Child is Grid g)
                {
                    var ct = g.Children.OfType<TextBlock>().FirstOrDefault(t => t.HorizontalAlignment == HorizontalAlignment.Right);
                    if (ct != null) ct.Text = counts[i].ToString();
                }
        }

        // ===============================================
        // NAVIGATION
        // ===============================================
        async Task NavigateToRoot() { var r = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token); if (r.Any()) await NavTo(r.First().Id); else { _currentFolderId = null; _breadcrumb.Clear(); await LoadFolder(); } }
        async Task NavTo(int fId, bool hist = true) { if (hist && _currentFolderId.HasValue && _currentFolderId.Value != fId) { _backHistory.Push(_currentFolderId.Value); _forwardHistory.Clear(); } _currentFolderId = fId; _selectedFile = null; HideDetail(); _breadcrumb = await SupabaseService.Instance.GetDriveBreadcrumb(fId, _cts.Token); await LoadFolder(); }
        async Task NavBack() { if (_backHistory.Count == 0) return; if (_currentFolderId.HasValue) _forwardHistory.Push(_currentFolderId.Value); await NavTo(_backHistory.Pop(), hist: false); }
        async Task NavFwd() { if (_forwardHistory.Count == 0) return; if (_currentFolderId.HasValue) _backHistory.Push(_currentFolderId.Value); await NavTo(_forwardHistory.Pop(), hist: false); }

        // ===============================================
        // LOAD FOLDER (P2 spinner, P5 storage, P12 filters)
        // ===============================================
        async Task LoadFolder()
        {
            var sw0 = Stopwatch.StartNew();
            Debug.WriteLine($"[DriveV2] === LoadFolder START (id={_currentFolderId}) ===");

            // P2: Show spinner
            LoadingPanel.Visibility = Visibility.Visible;
            ((Storyboard)FindResource("SpinnerStoryboard")).Begin();
            EmptyState.Visibility = Visibility.Collapsed;
            ContentHost.Content = null;

            try
            {
                // Phase 1: folders + files
                var sw = Stopwatch.StartNew();
                var ft = SupabaseService.Instance.GetDriveChildFolders(_currentFolderId, _cts.Token);
                var fit = _currentFolderId.HasValue ? SupabaseService.Instance.GetDriveFilesByFolder(_currentFolderId.Value, _cts.Token) : Task.FromResult(new List<DriveFileDb>());
                await Task.WhenAll(ft, fit); _currentFolders = ft.Result; _currentFiles = fit.Result;
                Debug.WriteLine($"[DriveV2]   P1 Folders+Files: {sw.ElapsedMilliseconds}ms ({_currentFolders.Count}f, {_currentFiles.Count}fi)");

                // Phase 2: stats (only uncached)
                sw.Restart();
                var unc = _currentFolders.Where(f => !_statsCache.ContainsKey(f.Id)).ToList();
                if (unc.Count > 0)
                {
                    var tasks = unc.Select(async f => { var fi = await SupabaseService.Instance.GetDriveFilesByFolder(f.Id, _cts.Token); var su = await SupabaseService.Instance.GetDriveChildFolders(f.Id, _cts.Token); return (f.Id, fi.Count, su.Count, fi.Sum(x => x.FileSize ?? 0)); });
                    foreach (var r in await Task.WhenAll(tasks)) _statsCache[r.Id] = (r.Item2, r.Item3, r.Item4);
                }
                Debug.WriteLine($"[DriveV2]   P2 Stats: {sw.ElapsedMilliseconds}ms (q={unc.Count}, cached={_currentFolders.Count - unc.Count})");

                // Phase 3: order info (only uncached)
                sw.Restart();
                var lids = _currentFolders.Where(f => f.LinkedOrderId.HasValue).Select(f => f.LinkedOrderId!.Value).Distinct().ToList();
                var uncO = lids.Where(id => !_orderInfoCache.ContainsKey(id)).ToList();
                if (uncO.Count > 0)
                {
                    var cl = await SupabaseService.Instance.GetClients();
                    var cm = cl?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();
                    var ot = uncO.Select(async oid => { try { var o = await SupabaseService.Instance.GetOrderById(oid); if (o != null) { var cn = o.ClientId.HasValue && cm.ContainsKey(o.ClientId.Value) ? cm[o.ClientId.Value] : ""; _orderInfoCache[oid] = (o.Po ?? $"#{oid}", cn, Tr(o.Description, 40)); } else _orderInfoCache[oid] = ($"#{oid}", "", ""); } catch { _orderInfoCache[oid] = ($"#{oid}", "", ""); } });
                    await Task.WhenAll(ot);
                }
                Debug.WriteLine($"[DriveV2]   P3 Orders: {sw.ElapsedMilliseconds}ms (q={uncO.Count}, cached={lids.Count - uncO.Count})");

                // Phase 4: Render
                sw.Restart();
                RenderBreadcrumb();
                BackToFoldersBtn.Visibility = _breadcrumb.Count <= 1 ? Visibility.Collapsed : Visibility.Visible;
                SectionTitle.Text = _breadcrumb.LastOrDefault()?.Name ?? "IMA Drive";
                var tot = _currentFolders.Count + _currentFiles.Count;
                var pts = new List<string>();
                if (_currentFolders.Count > 0) pts.Add($"{_currentFolders.Count} carpeta{(_currentFolders.Count != 1 ? "s" : "")}");
                if (_currentFiles.Count > 0) pts.Add($"{_currentFiles.Count} archivo{(_currentFiles.Count != 1 ? "s" : "")}");
                SectionSubtitle.Text = pts.Count > 0 ? string.Join(" - ", pts) : "Sin contenido";
                StatusText.Text = $"{tot} elemento{(tot != 1 ? "s" : "")}";
                RenderContent();
                if (tot == 0) EmptyState.Visibility = Visibility.Visible;
                UpdateFilterCounts(); // P12
                Debug.WriteLine($"[DriveV2]   P4 Render: {sw.ElapsedMilliseconds}ms");

                // P5: Storage indicator (non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        long total = _statsCache.Values.Sum(s => s.totalSize) + _currentFiles.Sum(f => f.FileSize ?? 0);
                        long maxB = 10L * 1024 * 1024 * 1024;
                        var pct = Math.Max(1, Math.Min(100, (int)(total * 100 / maxB)));
                        Dispatcher.Invoke(() =>
                        {
                            StorageLabel.Text = $"{Services.Drive.DriveService.FormatFileSize(total)} de 10 GB";
                            StorageAvailLabel.Text = $"{Services.Drive.DriveService.FormatFileSize(maxB - total)} disponibles";
                            StorageFillCol.Width = new GridLength(pct, GridUnitType.Star);
                            StorageEmptyCol.Width = new GridLength(100 - pct, GridUnitType.Star);
                        });
                    }
                    catch { }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[DriveV2] ERR: {ex.Message}"); StatusText.Text = "Error"; SectionTitle.Text = "Error"; SectionSubtitle.Text = ex.Message; }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ((Storyboard)FindResource("SpinnerStoryboard")).Stop();
                Debug.WriteLine($"[DriveV2] === LoadFolder TOTAL: {sw0.ElapsedMilliseconds}ms ===");
            }
        }

        void InvalidateStats(int? fId = null) { if (fId.HasValue) _statsCache.Remove(fId.Value); else _statsCache.Clear(); if (_currentFolderId.HasValue) _statsCache.Remove(_currentFolderId.Value); }

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
        // CONTENT (P8 WrapPanel responsive)
        // ===============================================
        void RenderContent()
        {
            EmptyState.Visibility = Visibility.Collapsed;
            if (_currentFiles.Count == 0 && _currentFolders.Count > 0 && _viewMode == "list") RenderList();
            else RenderWrap();
        }

        void RenderWrap()
        {
            var w = new WrapPanel();
            foreach (var f in _currentFolders) { var c = MkFolderCard(f); c.Width = 280; c.Margin = new Thickness(6); w.Children.Add(c); }
            foreach (var f in _currentFiles) { var c = MkFileCard(f); c.Width = 220; c.Margin = new Thickness(6); w.Children.Add(c); }
            ContentHost.Content = w;
        }

        void RenderList()
        {
            var wrap = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = BorderColor, BorderThickness = new Thickness(1), ClipToBounds = true };
            var stk = new StackPanel();
            var hg = new Grid { Background = Background, Height = 40 };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var hdrs = new[] { "NOMBRE", "ARCHIVOS", "TAMANO", "ORDEN VINCULADA", "MODIFICADO" };
            for (int i = 0; i < hdrs.Length; i++) { var t = new TextBlock { Text = hdrs[i], FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(i == 0 ? 24 : 0, 0, 0, 0) }; Grid.SetColumn(t, i); hg.Children.Add(t); }
            stk.Children.Add(new Border { BorderBrush = BorderColor, BorderThickness = new Thickness(0, 0, 0, 1), Child = hg });
            foreach (var f in _currentFolders) stk.Children.Add(MkListRow(f));
            wrap.Child = stk; ContentHost.Content = wrap;
        }

        UIElement MkListRow(DriveFolderDb folder)
        {
            var ac = CH(FolderColors[folder.Id % FolderColors.Length]);
            var rg = new Grid { Height = 56, Cursor = Cursors.Hand, Background = Brushes.White };
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            var np = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
            var ib = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(38, ac.R, ac.G, ac.B)), Margin = new Thickness(0, 0, 12, 0) };
            ib.Child = MkFolderIco(16, new SolidColorBrush(ac)); np.Children.Add(ib);
            var nt = new TextBlock { Text = folder.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center }; np.Children.Add(nt);
            Grid.SetColumn(np, 0); rg.Children.Add(np);
            var hs = _statsCache.TryGetValue(folder.Id, out var st);
            AddCol(rg, 1, hs ? st.fileCount.ToString() : "-"); AddCol(rg, 2, hs ? Services.Drive.DriveService.FormatFileSize(st.totalSize) : "-");
            var otx = OrderTxt(folder.LinkedOrderId); AddCol(rg, 3, otx, folder.LinkedOrderId.HasValue ? Primary : TextMuted);
            AddCol(rg, 4, RelT(folder.UpdatedAt));
            var rb = new Border { BorderBrush = BorderLight, BorderThickness = new Thickness(0, 0, 0, 1), Child = rg };
            rb.MouseEnter += (s, e) => { rg.Background = HoverBg; nt.Foreground = Primary; }; rb.MouseLeave += (s, e) => { rg.Background = Brushes.White; nt.Foreground = TextPrimary; };
            rb.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = SafeLoad(() => NavTo(folder.Id)); }; return rb;
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
            var nameT = new TextBlock { Text = folder.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(0, 0, 0, 2), ToolTip = folder.Name };
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
            card.Child = mg;
            card.MouseEnter += (s, e) => { card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 4, Opacity = 0.08 }; nameT.Foreground = Primary; moreBtn.Visibility = Visibility.Visible; };
            card.MouseLeave += (s, e) => { card.Effect = null; nameT.Foreground = TextPrimary; moreBtn.Visibility = Visibility.Collapsed; };
            card.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = SafeLoad(() => NavTo(folder.Id)); };
            card.MouseRightButtonDown += (s, e) => { var m = new ContextMenu(); m.Items.Add(MI("Abrir", (_, _) => _ = SafeLoad(() => NavTo(folder.Id)))); m.Items.Add(MI("Renombrar", (_, _) => RenFolder(folder))); m.Items.Add(new Separator()); m.Items.Add(MI("Vincular a Orden...", (_, _) => LinkOrder(folder))); if (linked) m.Items.Add(MI("Desvincular de Orden", async (_, _) => await Unlink(folder))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFolder(folder)); del.Foreground = Destructive; m.Items.Add(del); card.ContextMenu = m; };
            return card;
        }

        // ===============================================
        // FILE CARD
        // ===============================================
        Border MkFileCard(DriveFileDb file)
        {
            var (cH, bH) = GFC(file.FileName); var fC = CH(cH); var fB = new SolidColorBrush(fC); var bgB = BH(bH);
            var sel = _selectedFile?.Id == file.Id;
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = sel ? Primary : BorderColor, BorderThickness = new Thickness(2), Cursor = Cursors.Hand, ClipToBounds = true };
            if (sel) card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 12, ShadowDepth = 2, Opacity = 0.15 };
            var mg = new Grid(); mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) }); mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var prev = new Border { Background = bgB, CornerRadius = new CornerRadius(10, 10, 0, 0) }; var pg = new Grid();
            pg.Children.Add(new TextBlock { Text = FIcon(file.FileName), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 48, Foreground = fB, Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var ext = System.IO.Path.GetExtension(file.FileName)?.TrimStart('.').ToUpperInvariant() ?? "";
            var badge = new Border { Background = fB, CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 12, 0) };
            badge.Child = new TextBlock { Text = ext, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }; pg.Children.Add(badge);
            prev.Child = pg; Grid.SetRow(prev, 0); mg.Children.Add(prev);
            var ip = new StackPanel { Margin = new Thickness(16) };
            var nameT = new TextBlock { Text = file.FileName, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8), ToolTip = file.FileName }; ip.Children.Add(nameT);
            var sg = new Grid(); sg.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(file.FileSize), FontSize = 11, Foreground = TextMuted, HorizontalAlignment = HorizontalAlignment.Left }); sg.Children.Add(new TextBlock { Text = RelT(file.UploadedAt), FontSize = 11, Foreground = TextLight, HorizontalAlignment = HorizontalAlignment.Right }); ip.Children.Add(sg);
            Grid.SetRow(ip, 1); mg.Children.Add(ip); card.Child = mg;
            card.MouseEnter += (s, e) => { if (_selectedFile?.Id != file.Id) { card.BorderBrush = SlateLight; card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1D, 0x4E, 0xD8), BlurRadius = 20, ShadowDepth = 4, Opacity = 0.08 }; } nameT.Foreground = Primary; };
            card.MouseLeave += (s, e) => { if (_selectedFile?.Id != file.Id) { card.BorderBrush = BorderColor; card.Effect = null; } nameT.Foreground = TextPrimary; };
            card.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) _ = DlFile(file); else { _selectedFile = file; ShowDetail(file); RenderContent(); } };
            card.MouseRightButtonDown += (s, e) => { var m = new ContextMenu(); m.Items.Add(MI("Descargar", async (_, _) => await DlFile(file))); m.Items.Add(MI("Renombrar", (_, _) => RenFile(file))); m.Items.Add(new Separator()); var del = MI("Eliminar", async (_, _) => await DelFile(file)); del.Foreground = Destructive; m.Items.Add(del); card.ContextMenu = m; };
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
        // Inline folder creation: insert editable card, Enter confirms, Escape cancels
        void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ContentHost.Content is not WrapPanel wp) { RenderWrap(); wp = ContentHost.Content as WrapPanel ?? new WrapPanel(); }

            var aC = CH(FolderColors[(_currentFolders.Count + 1) % FolderColors.Length]);
            var aB = new SolidColorBrush(aC);
            var tint = new SolidColorBrush(Color.FromArgb(38, aC.R, aC.G, aC.B));

            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = Primary, BorderThickness = new Thickness(2), Width = 280, Margin = new Thickness(6), Cursor = Cursors.IBeam, ClipToBounds = true };
            var mg = new Grid();
            mg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            mg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var acc = new Border { Background = aB, Height = 4 }; Grid.SetRow(acc, 0); mg.Children.Add(acc);

            var hg = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icBdr = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), Background = tint, Margin = new Thickness(0, 0, 12, 0) };
            icBdr.Child = MkFolderIco(22, aB); Grid.SetColumn(icBdr, 0); hg.Children.Add(icBdr);

            var tbBdr = new Border { CornerRadius = new CornerRadius(6), BorderBrush = Primary, BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center };
            var tb = new TextBox { Text = "Nueva carpeta", FontSize = 14, FontWeight = FontWeights.SemiBold, Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(0) };
            tb.SelectAll();
            tbBdr.Child = tb; Grid.SetColumn(tbBdr, 1); hg.Children.Add(tbBdr);
            Grid.SetRow(hg, 1); mg.Children.Add(hg);
            card.Child = mg;

            // Insert at beginning
            wp.Children.Insert(0, card);
            tb.Focus();

            var committed = false;
            tb.KeyDown += async (s, ke) =>
            {
                if (ke.Key == Key.Enter && !committed)
                {
                    committed = true;
                    var name = tb.Text.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        await SafeLoad(async () =>
                        {
                            var created = await SupabaseService.Instance.CreateDriveFolder(name, _currentFolderId, _currentUser.Id, _cts.Token);
                            if (created != null) { InvalidateStats(); await LoadFolder(); }
                            else { MessageBox.Show("Nombre duplicado?", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); wp.Children.Remove(card); }
                        });
                    }
                    else wp.Children.Remove(card);
                }
                else if (ke.Key == Key.Escape) { ke.Handled = true; wp.Children.Remove(card); }
            };
            tb.LostFocus += (s, le) => { if (!committed) wp.Children.Remove(card); };
        }

        async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentFolderId.HasValue) { MessageBox.Show("Abra una carpeta primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (!SupabaseService.Instance.IsDriveStorageConfigured) { MessageBox.Show("R2 no configurado.", "Storage", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var dlg = new OpenFileDialog { Title = "Seleccionar archivos", Multiselect = true, Filter = "Todos (*.*)|*.*" }; if (dlg.ShowDialog() != true) return;
            await UploadFiles(dlg.FileNames);
        }

        void CloseUploadPanel_Click(object sender, RoutedEventArgs e)
        {
            UploadPanel.Visibility = Visibility.Collapsed;
            // Restore detail panel if a file was selected
            if (_selectedFile != null) DetailPanel.Visibility = Visibility.Visible;
        }

        async Task DlFile(DriveFileDb file) { if (!SupabaseService.Instance.IsDriveStorageConfigured) { MessageBox.Show("R2 no configurado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; } var d = new SaveFileDialog { Title = "Guardar", FileName = file.FileName, Filter = "Todos (*.*)|*.*" }; if (d.ShowDialog() != true) return; await SafeLoad(async () => { StatusText.Text = $"Descargando {file.FileName}..."; var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, d.FileName, _cts.Token); StatusText.Text = ok ? $"{file.FileName} descargado" : "Error"; }); }
        void RenFolder(DriveFolderDb f) { var n = Prompt("Renombrar", "Nuevo nombre:", f.Name); if (string.IsNullOrWhiteSpace(n) || n == f.Name) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFolder(f.Id, n, _cts.Token)) { InvalidateStats(); await LoadFolder(); } }); }
        void RenFile(DriveFileDb f) { var n = Prompt("Renombrar", "Nuevo nombre:", f.FileName); if (string.IsNullOrWhiteSpace(n) || n == f.FileName) return; _ = SafeLoad(async () => { if (await SupabaseService.Instance.RenameDriveFile(f.Id, n, _cts.Token)) await LoadFolder(); }); }
        async Task DelFolder(DriveFolderDb f) { if (MessageBox.Show($"Eliminar \"{f.Name}\" y todo su contenido?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; await SafeLoad(async () => { if (await SupabaseService.Instance.DeleteDriveFolder(f.Id, _cts.Token)) { _statsCache.Remove(f.Id); InvalidateStats(); await LoadFolder(); } }); }
        async Task DelFile(DriveFileDb f) { if (MessageBox.Show($"Eliminar \"{f.FileName}\"?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; await SafeLoad(async () => { if (await SupabaseService.Instance.DeleteDriveFile(f.Id, _cts.Token)) { if (_selectedFile?.Id == f.Id) HideDetail(); InvalidateStats(); await LoadFolder(); } }); }

        // ===============================================
        // ORDER LINKING
        // ===============================================
        async void LinkOrder(DriveFolderDb folder) { var roots = await SupabaseService.Instance.GetDriveChildFolders(null, _cts.Token); var rid = roots.FirstOrDefault()?.Id; if (rid == null || folder.ParentId != rid) { MessageBox.Show("Solo carpetas de primer nivel.", "No permitido", MessageBoxButton.OK, MessageBoxImage.Information); return; } var orders = await SupabaseService.Instance.GetOrders(200); if (orders == null || orders.Count == 0) { MessageBox.Show("Sin ordenes.", "", MessageBoxButton.OK, MessageBoxImage.Information); return; } var allL = await SupabaseService.Instance.GetDriveChildFolders(rid, _cts.Token); var used = allL.Where(f => f.LinkedOrderId.HasValue && f.Id != folder.Id).Select(f => f.LinkedOrderId!.Value).ToHashSet(); var avail = orders.Where(o => !used.Contains(o.Id)).ToList(); if (avail.Count == 0) { MessageBox.Show("Todas vinculadas.", "", MessageBoxButton.OK, MessageBoxImage.Information); return; } var cl = await SupabaseService.Instance.GetClients(); var cn = cl?.ToDictionary(c => c.Id, c => c.Name) ?? new Dictionary<int, string>(); var sel = OrderDlg(avail, cn); if (sel == null) return; await SafeLoad(async () => { if (await SupabaseService.Instance.LinkDriveFolderToOrder(folder.Id, sel.Id, _cts.Token)) { _orderInfoCache.Remove(sel.Id); StatusText.Text = $"Vinculada a {sel.Po}"; await LoadFolder(); } }); }
        async Task Unlink(DriveFolderDb f) { await SafeLoad(async () => { if (await SupabaseService.Instance.UnlinkDriveFolder(f.Id, _cts.Token)) { StatusText.Text = "Desvinculada"; await LoadFolder(); } }); }
        async void LinkThisFolder_Click(object sender, RoutedEventArgs e) { if (!_currentFolderId.HasValue || !_selectionOrderId.HasValue) return; await SafeLoad(async () => { if (await SupabaseService.Instance.LinkDriveFolderToOrder(_currentFolderId.Value, _selectionOrderId.Value, _cts.Token)) { MessageBox.Show($"Vinculada a {_selectionOrderPo}", "OK", MessageBoxButton.OK, MessageBoxImage.Information); DialogResult = true; Close(); } }); }
        void CancelSelection_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        // P7: Modern order dialog
        static OrderDb? OrderDlg(List<OrderDb> orders, Dictionary<int, string> cNames)
        {
            var w = new Window { Title = "Vincular a Orden", Width = 520, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent };
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 24, ShadowDepth = 8, Opacity = 0.12 }, Margin = new Thickness(16) };
            var p = new StackPanel { Margin = new Thickness(24) };
            p.Children.Add(new TextBlock { Text = "Vincular a Orden", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)), Margin = new Thickness(0, 0, 0, 16) });
            p.Children.Add(new TextBlock { Text = "Seleccione la orden a vincular:", FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), Margin = new Thickness(0, 0, 0, 8) });
            var sbBdr = new Border { CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), Background = Brushes.White, ClipToBounds = true, Margin = new Thickness(0, 0, 0, 8) };
            var sb = new TextBox { FontSize = 13, Padding = new Thickness(12, 10, 12, 10), BorderThickness = new Thickness(0), Text = "Buscar por OC, cliente o detalle...", Foreground = Brushes.Gray };
            sb.GotFocus += (s, e) => { if (sb.Foreground == Brushes.Gray) { sb.Text = ""; sb.Foreground = Brushes.Black; } }; sb.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(sb.Text)) { sb.Text = "Buscar por OC, cliente o detalle..."; sb.Foreground = Brushes.Gray; } };
            sbBdr.Child = sb; p.Children.Add(sbBdr);
            var cb = new ComboBox { FontSize = 12, Height = 30, IsEditable = false, DisplayMemberPath = "DisplayText" };
            var items = orders.Select(o => { var c = o.ClientId.HasValue && cNames.ContainsKey(o.ClientId.Value) ? cNames[o.ClientId.Value] : ""; var d = Tr(o.Description, 30); var t = $"{o.Po ?? "Sin OC"} | {Tr(c, 18)}"; if (!string.IsNullOrEmpty(d)) t += $" | {d}"; return new { Order = o, DisplayText = t }; }).ToList();
            cb.ItemsSource = items; if (items.Count > 0) cb.SelectedIndex = 0;
            sb.TextChanged += (s, e) => { if (sb.Foreground == Brushes.Gray) return; var f = sb.Text?.Trim().ToLowerInvariant() ?? ""; var fl = items.Where(i => i.DisplayText.ToLowerInvariant().Contains(f)).ToList(); cb.ItemsSource = fl; if (fl.Count > 0) cb.SelectedIndex = 0; };
            p.Children.Add(cb); OrderDb? res = null;
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var cancel = new Button { Content = "Cancelar", Width = 90, Padding = new Thickness(0, 8, 0, 8), Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)), Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)), BorderThickness = new Thickness(1), FontWeight = FontWeights.Medium, Cursor = Cursors.Hand, IsCancel = true };
            var ok = new Button { Content = "Vincular", Width = 90, Padding = new Thickness(0, 8, 0, 8), Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) };
            ok.Click += (s, e) => { var sel2 = cb.SelectedItem; if (sel2 != null) { res = ((dynamic)sel2).Order; w.Close(); } };
            bp.Children.Add(cancel); bp.Children.Add(ok); p.Children.Add(bp);
            card.Child = p; w.Content = card; w.MouseLeftButtonDown += (s, e) => { try { w.DragMove(); } catch { } }; w.ShowDialog(); return res;
        }

        // ===============================================
        // EVENT HANDLERS
        // ===============================================
        void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }

        // P10: Search with debounce
        async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            var q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
            await Task.Delay(300);
            if ((SearchBox.Text?.Trim().ToLowerInvariant() ?? "") != q) return;
            if (string.IsNullOrEmpty(q)) { RenderContent(); return; }
            var ff = _currentFolders.Where(f => f.Name.ToLowerInvariant().Contains(q) || (f.LinkedOrderId.HasValue && OrderTxt(f.LinkedOrderId).ToLowerInvariant().Contains(q))).ToList();
            var ffi = _currentFiles.Where(f => f.FileName.ToLowerInvariant().Contains(q)).ToList();
            var w = new WrapPanel();
            foreach (var f in ff) { var c = MkFolderCard(f); c.Width = 280; c.Margin = new Thickness(6); w.Children.Add(c); }
            foreach (var f in ffi) { var c = MkFileCard(f); c.Width = 220; c.Margin = new Thickness(6); w.Children.Add(c); }
            ContentHost.Content = w;
            StatusText.Text = $"{ff.Count + ffi.Count} resultado(s)";
            EmptyState.Visibility = (ff.Count + ffi.Count) == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        void GridView_Click(object sender, RoutedEventArgs e) { _viewMode = "grid"; UpdateViewToggle(); RenderContent(); }
        void ListView_Click(object sender, RoutedEventArgs e) { _viewMode = "list"; UpdateViewToggle(); RenderContent(); }
        async void BackToFolders_Click(object sender, RoutedEventArgs e) { if (_breadcrumb.Count >= 2) await SafeLoad(() => NavTo(_breadcrumb[^2].Id)); else await SafeLoad(() => NavigateToRoot()); }
        void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();
        void DetailClose_Click(object sender, RoutedEventArgs e) { HideDetail(); RenderContent(); }
        async void DetailDownload_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) await DlFile(_selectedFile); }
        void DetailLink_Click(object sender, RoutedEventArgs e) { if (_selectedFile != null) { Clipboard.SetText(_selectedFile.StoragePath ?? _selectedFile.FileName); StatusText.Text = "Ruta copiada"; } }
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

        // Shared upload logic with progress panel (used by Upload_Click and Window_Drop)
        async Task UploadFiles(string[] filePaths)
        {
            if (!_currentFolderId.HasValue) return;
            DetailPanel.Visibility = Visibility.Collapsed; // Hide detail to show upload on right
            UploadPanel.Visibility = Visibility.Visible; UploadItemsPanel.Children.Clear();
            UploadHeaderText.Text = $"Subiendo {filePaths.Length} archivo(s)...";
            var tr = new Dictionary<string, (TextBlock st, Border badge, ProgressBar pb)>();
            foreach (var fp in filePaths)
            {
                var fn = System.IO.Path.GetFileName(fp);
                long fSz = 0; try { fSz = new System.IO.FileInfo(fp).Length; } catch { }
                var (cH2, bH2) = GFC(fn);
                var row = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 2, 0, 2), Background = Brushes.White };
                var rg = new Grid(); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icB = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(6), Background = BH(bH2), Margin = new Thickness(0, 0, 10, 0) };
                icB.Child = new TextBlock { Text = FIcon(fn), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = BH(cH2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(icB, 0); rg.Children.Add(icB);
                var infoP = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                infoP.Children.Add(new TextBlock { Text = fn, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis });
                infoP.Children.Add(new TextBlock { Text = Services.Drive.DriveService.FormatFileSize(fSz), FontSize = 11, Foreground = TextMuted });
                Grid.SetColumn(infoP, 1); rg.Children.Add(infoP);
                var sBadge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3), Background = HoverBg, VerticalAlignment = VerticalAlignment.Center };
                var sT = new TextBlock { Text = "Pendiente", FontSize = 11, FontWeight = FontWeights.Medium, Foreground = TextMuted };
                sBadge.Child = sT; Grid.SetColumn(sBadge, 2); rg.Children.Add(sBadge);
                row.Child = rg; UploadItemsPanel.Children.Add(row);
                var pb = new ProgressBar { Height = 3, Value = 0, Margin = new Thickness(0, 0, 0, 4), Foreground = Primary, Background = BorderLight };
                UploadItemsPanel.Children.Add(pb);
                tr[fp] = (sT, sBadge, pb);
            }
            int ok = 0, fail = 0;
            foreach (var fp in filePaths)
            {
                var (sT, sBadge, pb) = tr[fp];
                try { sT.Text = "Subiendo..."; sT.Foreground = Primary; sBadge.Background = ActiveBg; pb.IsIndeterminate = true; await SupabaseService.Instance.UploadDriveFile(fp, _currentFolderId.Value, _currentUser.Id, _cts.Token); pb.IsIndeterminate = false; pb.Value = 100; pb.Foreground = GreenOk; sT.Text = "Listo"; sT.Foreground = GreenOk; sBadge.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4)); ok++; }
                catch (Exception ex) { Debug.WriteLine($"Upload err: {ex.Message}"); pb.IsIndeterminate = false; pb.Value = 100; pb.Foreground = Destructive; sT.Text = "Error"; sT.Foreground = Destructive; sBadge.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)); fail++; }
            }
            UploadHeaderText.Text = fail > 0 ? $"{ok} subido(s), {fail} fallido(s)" : $"{ok} archivo(s) subido(s)";
            StatusText.Text = UploadHeaderText.Text;
            InvalidateStats(); await SafeLoad(() => LoadFolder());
        }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.Key == Key.Escape) { if (_selectedFile != null) { HideDetail(); RenderContent(); } else if (_breadcrumb.Count > 1) BackToFolders_Click(this, new RoutedEventArgs()); else Close(); } base.OnKeyDown(e); }
        async void OnMouseNav(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.XButton1) { e.Handled = true; await SafeLoad(() => NavBack()); } else if (e.ChangedButton == MouseButton.XButton2) { e.Handled = true; await SafeLoad(() => NavFwd()); } }

        // ===============================================
        // HELPERS
        // ===============================================
        async Task SafeLoad(Func<Task> a) { try { await a(); } catch (OperationCanceledException) { } catch (Exception ex) { Debug.WriteLine($"[DriveV2] ERR: {ex.Message}"); StatusText.Text = $"Error: {ex.Message}"; } }
        async Task<string> ResolveUser(int? uid) { if (!uid.HasValue) return "-"; if (_userNameCache.TryGetValue(uid.Value, out var c)) return c; try { var u = await SupabaseService.Instance.GetUserById(uid.Value); var n = u?.FullName ?? u?.Username ?? $"#{uid}"; _userNameCache[uid.Value] = n; return n; } catch { return $"#{uid}"; } }
        string OrderTxt(int? oid) { if (!oid.HasValue) return "-"; if (!_orderInfoCache.TryGetValue(oid.Value, out var oi)) return $"#{oid}"; return !string.IsNullOrEmpty(oi.Client) ? $"{oi.Po} | {Tr(oi.Client, 16)}" : oi.Po; }
        static UIElement MkFolderIco(double sz, Brush fill) => new System.Windows.Shapes.Path { Data = FolderGeo, Fill = fill, Stretch = Stretch.Uniform, Width = sz, Height = sz, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

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

        static string Tr(string? v, int m) => string.IsNullOrEmpty(v) ? "" : v.Length <= m ? v : v[..m] + "...";
        static MenuItem MI(string h, RoutedEventHandler hnd) { var m = new MenuItem { Header = h }; m.Click += hnd; return m; }
        static Color CH(string h) { h = h.TrimStart('#'); return Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16)); }
        static SolidColorBrush BH(string h) => new(CH(h));
    }

    static class DriveV2Ext { public static T Also<T>(this T o, Action<T> a) { a(o); return o; } }
}
