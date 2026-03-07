using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SistemaGestionProyectos2.Models;

namespace SistemaGestionProyectos2.Views
{
    public partial class DriveV2Window : Window
    {
        private readonly UserSession _currentUser;

        // State
        private string _activeNav = "all";
        private string _viewMode = "grid";
        private MockFolder? _currentFolder = null;
        private MockFile? _selectedFile = null;
        private readonly List<Border> _navItems = new();

        // ===============================================
        // THEME COLORS (from Figma)
        // ===============================================
        private static readonly SolidColorBrush Primary = Freeze(0x1D, 0x4E, 0xD8);
        private static readonly SolidColorBrush PrimaryHover = Freeze(0x1E, 0x40, 0xAF);
        private static readonly SolidColorBrush PrimaryLight = Freeze(0x3B, 0x82, 0xF6);
        private static readonly SolidColorBrush Background = Freeze(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush CardBg = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush BorderColor = Freeze(0xE2, 0xE8, 0xF0);
        private static readonly SolidColorBrush BorderLight = Freeze(0xF1, 0xF5, 0xF9);
        private static readonly SolidColorBrush TextPrimary = Freeze(0x0F, 0x17, 0x2A);
        private static readonly SolidColorBrush TextSecondary = Freeze(0x47, 0x55, 0x69);
        private static readonly SolidColorBrush TextMuted = Freeze(0x64, 0x74, 0x8B);
        private static readonly SolidColorBrush TextLight = Freeze(0x94, 0xA3, 0xB8);
        private static readonly SolidColorBrush SlateLight = Freeze(0xCB, 0xD5, 0xE1);
        private static readonly SolidColorBrush HoverBg = Freeze(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush ActiveBg = Freeze(0xEF, 0xF6, 0xFF);
        private static readonly SolidColorBrush Destructive = Freeze(0xDC, 0x26, 0x26);

        static SolidColorBrush Freeze(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        static SolidColorBrush FreezeA(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        // ===============================================
        // FOLDER ACCENT COLORS
        // ===============================================
        private static readonly string[] FolderAccentColors =
        {
            "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#06B6D4"
        };

        // ===============================================
        // FILE TYPE CONFIG
        // ===============================================
        private static readonly Dictionary<string, (string color, string bgColor, string icon)> FileTypeConfig = new()
        {
            ["pdf"]  = ("#EF4444", "#FEF2F2", "\uE8A5"),
            ["dwg"]  = ("#8B5CF6", "#F5F3FF", "\uE8A5"),
            ["xlsx"] = ("#10B981", "#F0FDF4", "\uE8A5"),
            ["xls"]  = ("#10B981", "#F0FDF4", "\uE8A5"),
            ["docx"] = ("#3B82F6", "#EFF6FF", "\uE8A5"),
            ["doc"]  = ("#3B82F6", "#EFF6FF", "\uE8A5"),
            ["mp4"]  = ("#EC4899", "#FDF2F8", "\uE714"),
            ["zip"]  = ("#F59E0B", "#FFFBEB", "\uE8B7"),
            ["jpg"]  = ("#10B981", "#F0FDF4", "\uEB9F"),
            ["jpeg"] = ("#10B981", "#F0FDF4", "\uEB9F"),
            ["png"]  = ("#10B981", "#F0FDF4", "\uEB9F"),
        };

        private static (string color, string bgColor, string icon) GetFileConfig(string ext)
        {
            ext = ext.TrimStart('.').ToLowerInvariant();
            return FileTypeConfig.TryGetValue(ext, out var cfg)
                ? cfg
                : ("#64748B", "#F8FAFC", "\uE8A5");
        }

        // ===============================================
        // MOCK DATA
        // ===============================================
        private readonly List<MockFolder> _mockFolders = new()
        {
            new() { Id = 1, Name = "Valvulas Industriales",       OrderCode = "OC-2025-001", FileCount = 24, Size = "156 MB",  LastModified = "hace 2 horas",  Color = "#3B82F6" },
            new() { Id = 2, Name = "Motores Electricos Siemens",  OrderCode = "OC-2025-002", FileCount = 18, Size = "89 MB",   LastModified = "hace 5 horas",  Color = "#10B981" },
            new() { Id = 3, Name = "Sensores de Proximidad",      OrderCode = "OC-2025-003", FileCount = 32, Size = "203 MB",  LastModified = "ayer",          Color = "#F59E0B" },
            new() { Id = 4, Name = "PLC Allen Bradley",           OrderCode = "OC-2025-004", FileCount = 15, Size = "67 MB",   LastModified = "hace 2 dias",   Color = "#8B5CF6" },
            new() { Id = 5, Name = "Rodamientos SKF",             OrderCode = "OC-2025-005", FileCount = 41, Size = "312 MB",  LastModified = "hace 3 dias",   Color = "#EF4444" },
            new() { Id = 6, Name = "Bandas Transportadoras",      OrderCode = "OC-2025-006", FileCount = 28, Size = "178 MB",  LastModified = "hace 4 dias",   Color = "#06B6D4" },
        };

        private readonly List<MockFile> _mockFiles = new()
        {
            new() { Id = 1, Name = "Cotizacion_v3.pdf",            Extension = "pdf",  Size = "2.4 MB",   UploadedBy = "Carlos M.",   UploadDate = "5 de marzo, 2026",  Type = "Documento PDF" },
            new() { Id = 2, Name = "Plano_tecnico_rev2.dwg",       Extension = "dwg",  Size = "18.7 MB",  UploadedBy = "Ing. Lopez",  UploadDate = "4 de marzo, 2026",  Type = "Archivo CAD" },
            new() { Id = 3, Name = "Factura_001234.pdf",            Extension = "pdf",  Size = "1.1 MB",   UploadedBy = "Admin",       UploadDate = "3 de marzo, 2026",  Type = "Documento PDF" },
            new() { Id = 4, Name = "Fotos_instalacion.zip",         Extension = "zip",  Size = "45.2 MB",  UploadedBy = "Carlos M.",   UploadDate = "6 de marzo, 2026",  Type = "Archivo comprimido" },
            new() { Id = 5, Name = "Especificaciones.xlsx",         Extension = "xlsx", Size = "340 KB",   UploadedBy = "Vendedor 1",  UploadDate = "2 de marzo, 2026",  Type = "Hoja de calculo Excel" },
            new() { Id = 6, Name = "Manual_operacion.pdf",          Extension = "pdf",  Size = "8.9 MB",   UploadedBy = "Proveedor",   UploadDate = "1 de marzo, 2026",  Type = "Documento PDF" },
            new() { Id = 7, Name = "Reporte_calidad.docx",          Extension = "docx", Size = "5.2 MB",   UploadedBy = "QA Team",     UploadDate = "7 de marzo, 2026",  Type = "Documento Word" },
            new() { Id = 8, Name = "Video_prueba.mp4",              Extension = "mp4",  Size = "124.5 MB", UploadedBy = "Carlos M.",   UploadDate = "6 de marzo, 2026",  Type = "Video MP4" },
        };

        // ===============================================
        // CONSTRUCTOR
        // ===============================================
        public DriveV2Window(UserSession user)
        {
            InitializeComponent();
            _currentUser = user;

            CardBg.Freeze();

            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
            this.SourceInitialized += (s, e) => Helpers.WindowHelper.MaximizeToCurrentMonitor(this);

            Loaded += (s, e) =>
            {
                InitializeSidebar();
                UpdateViewToggle();
                NavigateToRoot();
            };
        }

        // ===============================================
        // SIDEBAR
        // ===============================================
        private void InitializeSidebar()
        {
            var navItems = new[]
            {
                ("all",     "\uE80F", "Todos los archivos"),
                ("recent",  "\uE823", "Recientes"),
                ("starred", "\uE734", "Destacados"),
                ("trash",   "\uE74D", "Papelera"),
            };

            foreach (var (id, icon, label) in navItems)
            {
                var item = CreateNavItem(id, icon, label);
                NavPanel.Children.Add(item);
                _navItems.Add(item);
            }

            SetActiveNavItem("all");

            var filterItems = new[]
            {
                ("pdf",    "#EF4444", "PDFs",              "124"),
                ("images", "#10B981", "Imagenes",           "89"),
                ("cad",    "#8B5CF6", "Archivos CAD",       "45"),
                ("excel",  "#10B981", "Hojas de calculo",   "67"),
                ("videos", "#EC4899", "Videos",             "23"),
            };

            foreach (var (id, color, label, count) in filterItems)
            {
                FilterPanel.Children.Add(CreateFilterItem(id, color, label, count));
            }
        }

        private Border CreateNavItem(string id, string icon, string label)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Tag = id
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            sp.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Foreground = TextSecondary
            });

            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = TextSecondary
            });

            border.Child = sp;

            border.MouseLeftButtonDown += (s, e) =>
            {
                SetActiveNavItem(id);
                _currentFolder = null;
                _selectedFile = null;
                HideDetailPanel();
                NavigateToRoot();
            };

            border.MouseEnter += (s, e) =>
            {
                if (border.Tag as string != _activeNav)
                    border.Background = HoverBg;
            };
            border.MouseLeave += (s, e) =>
            {
                if (border.Tag as string != _activeNav)
                    border.Background = Brushes.Transparent;
            };

            return border;
        }

        private void SetActiveNavItem(string id)
        {
            _activeNav = id;
            foreach (var item in _navItems)
            {
                var isActive = item.Tag as string == id;
                item.Background = isActive ? ActiveBg : Brushes.Transparent;
                var sp = item.Child as StackPanel;
                if (sp == null) continue;
                foreach (var child in sp.Children.OfType<TextBlock>())
                {
                    child.Foreground = isActive ? Primary : TextSecondary;
                }
            }
        }

        private UIElement CreateFilterItem(string id, string color, string label, string count)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };

            var grid = new Grid();
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = BrushFromHex(color),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            sp.Children.Add(dot);

            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            });

            grid.Children.Add(sp);
            grid.Children.Add(new TextBlock
            {
                Text = count,
                FontSize = 12,
                Foreground = TextLight,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });

            border.Child = grid;

            border.MouseEnter += (s, e) => border.Background = HoverBg;
            border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;

            return border;
        }

        // ===============================================
        // BREADCRUMB
        // ===============================================
        private void UpdateBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();

            // Home icon
            var home = new TextBlock
            {
                Text = "\uE80F",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            home.MouseLeftButtonDown += (s, e) =>
            {
                _currentFolder = null;
                HideDetailPanel();
                NavigateToRoot();
            };
            BreadcrumbPanel.Children.Add(home);

            var segments = new List<string>();
            if (_currentFolder != null)
            {
                segments.Add("IMA MECATRONICA");
                segments.Add($"{_currentFolder.OrderCode} - {_currentFolder.Name}");
            }
            else
            {
                segments.Add("Todos los archivos");
            }

            for (int i = 0; i < segments.Count; i++)
            {
                var isLast = i == segments.Count - 1;

                // Chevron
                BreadcrumbPanel.Children.Add(new TextBlock
                {
                    Text = "\uE76C",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10,
                    Foreground = TextLight,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0)
                });

                var segment = new TextBlock
                {
                    Text = segments[i],
                    FontSize = 13,
                    Foreground = isLast ? TextPrimary : TextMuted,
                    FontWeight = isLast ? FontWeights.Medium : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = isLast ? Cursors.Arrow : Cursors.Hand
                };

                if (!isLast)
                {
                    segment.MouseEnter += (s, e) => ((TextBlock)s!).Foreground = Primary;
                    segment.MouseLeave += (s, e) => ((TextBlock)s!).Foreground = TextMuted;
                    segment.MouseLeftButtonDown += (s, e) =>
                    {
                        _currentFolder = null;
                        HideDetailPanel();
                        NavigateToRoot();
                    };
                }

                BreadcrumbPanel.Children.Add(segment);
            }
        }

        // ===============================================
        // NAVIGATION
        // ===============================================
        private void NavigateToRoot()
        {
            _currentFolder = null;
            _selectedFile = null;
            BackToFoldersBtn.Visibility = Visibility.Collapsed;
            SectionTitle.Text = "Ordenes de compra";

            var totalFiles = _mockFolders.Sum(f => f.FileCount);
            SectionSubtitle.Text = $"{_mockFolders.Count} carpetas - {totalFiles} archivos totales";

            UpdateBreadcrumb();
            RenderContent();
        }

        private void NavigateToFolder(MockFolder folder)
        {
            _currentFolder = folder;
            _selectedFile = null;
            HideDetailPanel();
            BackToFoldersBtn.Visibility = Visibility.Visible;
            SectionTitle.Text = $"{folder.OrderCode} - {folder.Name}";
            SectionSubtitle.Text = $"{_mockFiles.Count} archivos";

            UpdateBreadcrumb();
            RenderContent();
        }

        // ===============================================
        // CONTENT RENDERING
        // ===============================================
        private void RenderContent()
        {
            EmptyState.Visibility = Visibility.Collapsed;

            if (_currentFolder == null)
            {
                if (_viewMode == "grid")
                    RenderFolderGrid();
                else
                    RenderFolderList();
            }
            else
            {
                RenderFileGrid();
            }
        }

        private void RenderFolderGrid()
        {
            var grid = new UniformGrid { Columns = 3 };

            foreach (var folder in _mockFolders)
            {
                grid.Children.Add(CreateFolderCard(folder));
            }

            ContentHost.Content = grid;
        }

        private void RenderFolderList()
        {
            var container = new StackPanel();

            // Table wrapper
            var tableBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderBrush = BorderColor,
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            var tablePanel = new StackPanel();

            // Header row
            var headerGrid = new Grid
            {
                Background = Background,
                Height = 40
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            var headers = new[] { "Nombre", "Archivos", "Tamano", "Modificado" };
            for (int i = 0; i < headers.Length; i++)
            {
                var h = new TextBlock
                {
                    Text = headers[i].ToUpperInvariant(),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMuted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(i == 0 ? 24 : 0, 0, 0, 0)
                };
                Grid.SetColumn(h, i);
                headerGrid.Children.Add(h);
            }

            // Bottom border for header
            var headerBorderLine = new Border
            {
                BorderBrush = BorderColor,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = headerGrid
            };
            tablePanel.Children.Add(headerBorderLine);

            // Folder rows
            foreach (var folder in _mockFolders)
            {
                tablePanel.Children.Add(CreateFolderListRow(folder));
            }

            tableBorder.Child = tablePanel;
            ContentHost.Content = tableBorder;
        }

        private UIElement CreateFolderListRow(MockFolder folder)
        {
            var accentColor = ColorFromHex(folder.Color);

            var rowGrid = new Grid
            {
                Height = 60,
                Cursor = Cursors.Hand,
                Background = Brushes.White
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            // Name col: icon + texts
            var namePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24, 0, 0, 0)
            };

            var iconBg = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B)),
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBg.Child = new TextBlock
            {
                Text = "\uED41",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = new SolidColorBrush(accentColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            namePanel.Children.Add(iconBg);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var orderText = new TextBlock
            {
                Text = folder.OrderCode,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary
            };
            textPanel.Children.Add(orderText);
            textPanel.Children.Add(new TextBlock
            {
                Text = folder.Name,
                FontSize = 12,
                Foreground = TextMuted,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 300
            });
            namePanel.Children.Add(textPanel);
            Grid.SetColumn(namePanel, 0);
            rowGrid.Children.Add(namePanel);

            // File count
            var countText = new TextBlock
            {
                Text = folder.FileCount.ToString(),
                FontSize = 13,
                Foreground = TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(countText, 1);
            rowGrid.Children.Add(countText);

            // Size
            var sizeText = new TextBlock
            {
                Text = folder.Size,
                FontSize = 13,
                Foreground = TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sizeText, 2);
            rowGrid.Children.Add(sizeText);

            // Modified
            var modText = new TextBlock
            {
                Text = folder.LastModified,
                FontSize = 12,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(modText, 3);
            rowGrid.Children.Add(modText);

            // Bottom border
            var rowBorder = new Border
            {
                BorderBrush = BorderLight,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = rowGrid
            };

            // Hover
            rowBorder.MouseEnter += (s, e) =>
            {
                rowGrid.Background = HoverBg;
                orderText.Foreground = Primary;
            };
            rowBorder.MouseLeave += (s, e) =>
            {
                rowGrid.Background = Brushes.White;
                orderText.Foreground = TextPrimary;
            };

            // Double click navigate
            rowBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    NavigateToFolder(folder);
            };

            return rowBorder;
        }

        private void RenderFileGrid()
        {
            var grid = new UniformGrid { Columns = 4 };

            foreach (var file in _mockFiles)
            {
                grid.Children.Add(CreateFileCard(file));
            }

            ContentHost.Content = grid;
        }

        // ===============================================
        // FOLDER CARD
        // ===============================================
        private UIElement CreateFolderCard(MockFolder folder)
        {
            var accentColor = ColorFromHex(folder.Color);
            var accentBrush = new SolidColorBrush(accentColor);
            var tintBrush = new SolidColorBrush(Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B));

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderBrush = BorderColor,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(8),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                Tag = folder
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });       // accent line
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // stats
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // footer

            // Accent line
            var accentLine = new Border
            {
                Background = accentBrush,
                Height = 4
            };
            Grid.SetRow(accentLine, 0);
            mainGrid.Children.Add(accentLine);

            // Header content
            var headerGrid = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Folder icon with tinted background
            var iconBorder = new Border
            {
                Width = 48, Height = 48,
                CornerRadius = new CornerRadius(8),
                Background = tintBrush,
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBorder.Child = new TextBlock
            {
                Text = "\uED41",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBorder, 0);
            headerGrid.Children.Add(iconBorder);

            // Text info
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var orderCodeText = new TextBlock
            {
                Text = folder.OrderCode,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                Margin = new Thickness(0, 0, 0, 2)
            };
            infoPanel.Children.Add(orderCodeText);
            infoPanel.Children.Add(new TextBlock
            {
                Text = folder.Name,
                FontSize = 12,
                Foreground = TextMuted,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            });
            Grid.SetColumn(infoPanel, 1);
            headerGrid.Children.Add(infoPanel);

            // More button (visible on hover)
            var moreBtn = new Button
            {
                Style = FindResource("IconButton") as Style,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Top
            };
            moreBtn.Content = new TextBlock
            {
                Text = "\uE712",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = TextMuted
            };
            Grid.SetColumn(moreBtn, 2);
            headerGrid.Children.Add(moreBtn);

            Grid.SetRow(headerGrid, 1);
            mainGrid.Children.Add(headerGrid);

            // Stats row
            var statsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20, 0, 20, 0)
            };
            statsPanel.Children.Add(new TextBlock
            {
                Text = "\uE8A5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            statsPanel.Children.Add(new TextBlock
            {
                Text = $"{folder.FileCount} archivos",
                FontSize = 12,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            });
            statsPanel.Children.Add(new Ellipse
            {
                Width = 4, Height = 4,
                Fill = SlateLight,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0)
            });
            statsPanel.Children.Add(new TextBlock
            {
                Text = folder.Size,
                FontSize = 12,
                Foreground = TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(statsPanel, 2);
            mainGrid.Children.Add(statsPanel);

            // Footer
            var footer = new Border
            {
                BorderBrush = BorderLight,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(20, 16, 20, 0),
                Padding = new Thickness(0, 12, 0, 16)
            };
            footer.Child = new TextBlock
            {
                Text = $"Modificado {folder.LastModified}",
                FontSize = 11,
                Foreground = TextLight
            };
            Grid.SetRow(footer, 3);
            mainGrid.Children.Add(footer);

            card.Child = mainGrid;

            // Hover effect
            card.MouseEnter += (s, e) =>
            {
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0x1D, 0x4E, 0xD8),
                    BlurRadius = 20,
                    ShadowDepth = 4,
                    Opacity = 0.08
                };
                orderCodeText.Foreground = Primary;
                moreBtn.Visibility = Visibility.Visible;
            };
            card.MouseLeave += (s, e) =>
            {
                card.Effect = null;
                orderCodeText.Foreground = TextPrimary;
                moreBtn.Visibility = Visibility.Collapsed;
            };

            // Double click: navigate into folder
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    NavigateToFolder(folder);
            };

            // Context menu
            card.MouseRightButtonDown += (s, e) =>
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("Abrir", (_, _) => NavigateToFolder(folder)));
                menu.Items.Add(CreateMenuItem("Renombrar", (_, _) => { }));
                menu.Items.Add(new Separator());
                menu.Items.Add(CreateMenuItem("Vincular a Orden...", (_, _) => { }));
                menu.Items.Add(new Separator());
                var deleteItem = CreateMenuItem("Eliminar", (_, _) => { });
                deleteItem.Foreground = Destructive;
                menu.Items.Add(deleteItem);
                card.ContextMenu = menu;
            };

            return card;
        }

        // ===============================================
        // FILE CARD
        // ===============================================
        private UIElement CreateFileCard(MockFile file)
        {
            var (colorHex, bgColorHex, iconChar) = GetFileConfig(file.Extension);
            var fileColor = ColorFromHex(colorHex);
            var fileBrush = new SolidColorBrush(fileColor);
            var fileBgBrush = BrushFromHex(bgColorHex);

            var isSelected = _selectedFile?.Id == file.Id;

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderBrush = isSelected ? Primary : BorderColor,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(8),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                Tag = file
            };

            if (isSelected)
            {
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0x1D, 0x4E, 0xD8),
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.15
                };
            }

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) }); // preview
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // info

            // Preview area
            var previewBorder = new Border
            {
                Background = fileBgBrush,
                CornerRadius = new CornerRadius(10, 10, 0, 0)
            };

            var previewGrid = new Grid();

            // File icon
            previewGrid.Children.Add(new TextBlock
            {
                Text = iconChar,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 48,
                Foreground = fileBrush,
                Opacity = 0.8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Extension badge
            var badgeBorder = new Border
            {
                Background = fileBrush,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 12, 12, 0)
            };
            badgeBorder.Child = new TextBlock
            {
                Text = file.Extension.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            previewGrid.Children.Add(badgeBorder);

            previewBorder.Child = previewGrid;
            Grid.SetRow(previewBorder, 0);
            mainGrid.Children.Add(previewBorder);

            // Info area
            var infoPanel = new StackPanel { Margin = new Thickness(16) };

            var nameText = new TextBlock
            {
                Text = file.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 8),
                ToolTip = file.Name
            };
            infoPanel.Children.Add(nameText);

            var statsGrid = new Grid();
            statsGrid.Children.Add(new TextBlock
            {
                Text = file.Size,
                FontSize = 11,
                Foreground = TextMuted,
                HorizontalAlignment = HorizontalAlignment.Left
            });
            statsGrid.Children.Add(new TextBlock
            {
                Text = file.UploadedBy,
                FontSize = 11,
                Foreground = TextLight,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 100
            });
            infoPanel.Children.Add(statsGrid);

            Grid.SetRow(infoPanel, 1);
            mainGrid.Children.Add(infoPanel);

            card.Child = mainGrid;

            // Hover effect
            card.MouseEnter += (s, e) =>
            {
                if (_selectedFile?.Id != file.Id)
                {
                    card.BorderBrush = SlateLight;
                    card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(0x1D, 0x4E, 0xD8),
                        BlurRadius = 20,
                        ShadowDepth = 4,
                        Opacity = 0.08
                    };
                }
                nameText.Foreground = Primary;
            };
            card.MouseLeave += (s, e) =>
            {
                if (_selectedFile?.Id != file.Id)
                {
                    card.BorderBrush = BorderColor;
                    card.Effect = null;
                }
                nameText.Foreground = TextPrimary;
            };

            // Click: select and show detail
            card.MouseLeftButtonDown += (s, e) =>
            {
                _selectedFile = file;
                ShowDetailPanel(file);
                RenderContent(); // re-render to update selection state
            };

            // Context menu
            card.MouseRightButtonDown += (s, e) =>
            {
                var menu = new ContextMenu();
                menu.Items.Add(CreateMenuItem("Descargar", (_, _) => { }));
                menu.Items.Add(CreateMenuItem("Renombrar", (_, _) => { }));
                menu.Items.Add(new Separator());
                var deleteItem = CreateMenuItem("Eliminar", (_, _) => { });
                deleteItem.Foreground = Destructive;
                menu.Items.Add(deleteItem);
                card.ContextMenu = menu;
            };

            return card;
        }

        // ===============================================
        // DETAIL PANEL
        // ===============================================
        private void ShowDetailPanel(MockFile file)
        {
            var (colorHex, _, _) = GetFileConfig(file.Extension);
            var fileColor = ColorFromHex(colorHex);

            DetailFileName.Text = file.Name;
            DetailFileExt.Text = $"Archivo {file.Extension.ToUpperInvariant()}";
            DetailPreviewIcon.Text = "\uE8A5";

            // Update preview background gradient based on file color
            var lightColor = Color.FromArgb(40, fileColor.R, fileColor.G, fileColor.B);
            var lighterColor = Color.FromArgb(20, fileColor.R, fileColor.G, fileColor.B);
            var gradient = new LinearGradientBrush(lighterColor, lightColor, 45);
            DetailPreviewBg.Background = gradient;
            DetailPreviewIcon.Foreground = new SolidColorBrush(fileColor);

            // Info items
            DetailInfoPanel.Children.Clear();

            var infoItems = new[]
            {
                ("\uE8A5", "Tipo",            file.Type),
                ("\uEDA2", "Tamano",          file.Size),
                ("\uE787", "Fecha de subida", file.UploadDate),
                ("\uE77B", "Subido por",      file.UploadedBy),
                ("\uED41", "Ubicacion",       _currentFolder != null
                    ? $"IMA MECATRONICA / {_currentFolder.OrderCode}"
                    : "IMA MECATRONICA"),
            };

            foreach (var (icon, label, value) in infoItems)
            {
                DetailInfoPanel.Children.Add(CreateDetailInfoItem(icon, label, value));
            }

            DetailPanel.Visibility = Visibility.Visible;
        }

        private void HideDetailPanel()
        {
            _selectedFile = null;
            DetailPanel.Visibility = Visibility.Collapsed;
        }

        private UIElement CreateDetailInfoItem(string icon, string label, string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };

            var iconBorder = new Border
            {
                Width = 32, Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = Background,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            iconBorder.Child = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = TextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(iconBorder);

            var textPanel = new StackPanel { Margin = new Thickness(44, 0, 0, 0) };
            textPanel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = TextLight,
                Margin = new Thickness(0, 0, 0, 2)
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = TextPrimary,
                TextWrapping = TextWrapping.Wrap
            });
            grid.Children.Add(textPanel);

            return grid;
        }

        // ===============================================
        // VIEW MODE
        // ===============================================
        private void UpdateViewToggle()
        {
            // Grid button
            var gridBg = _viewMode == "grid" ? Brushes.White : Brushes.Transparent;
            var gridFg = _viewMode == "grid" ? Primary : TextMuted;
            GridViewBtn.Background = gridBg;
            if (GridViewBtn.Content is TextBlock gridIcon)
                gridIcon.Foreground = gridFg;
            if (_viewMode == "grid")
            {
                GridViewBtn.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.1
                };
            }
            else
            {
                GridViewBtn.Effect = null;
            }

            // List button
            var listBg = _viewMode == "list" ? Brushes.White : Brushes.Transparent;
            var listFg = _viewMode == "list" ? Primary : TextMuted;
            ListViewBtn.Background = listBg;
            if (ListViewBtn.Content is TextBlock listIcon)
                listIcon.Foreground = listFg;
            if (_viewMode == "list")
            {
                ListViewBtn.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.1
                };
            }
            else
            {
                ListViewBtn.Effect = null;
            }
        }

        // ===============================================
        // EVENT HANDLERS
        // ===============================================
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void GridView_Click(object sender, RoutedEventArgs e)
        {
            _viewMode = "grid";
            UpdateViewToggle();
            RenderContent();
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            _viewMode = "list";
            UpdateViewToggle();
            RenderContent();
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Nueva carpeta - Pendiente de conectar con BD",
                "IMA Drive v2", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Subir archivo - Pendiente de conectar con R2",
                "IMA Drive v2", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BackToFolders_Click(object sender, RoutedEventArgs e)
        {
            HideDetailPanel();
            NavigateToRoot();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DetailClose_Click(object sender, RoutedEventArgs e)
        {
            HideDetailPanel();
            RenderContent();
        }

        private void DetailDownload_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Descargar archivo - Pendiente de conectar con R2",
                "IMA Drive v2", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DetailLink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Enlace copiado (simulado)",
                "IMA Drive v2", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DetailDelete_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Eliminar archivo - Pendiente de conectar con BD",
                "IMA Drive v2", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Drag & Drop
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (_currentFolder != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                DragDropOverlay.Visibility = Visibility.Visible;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        // Keyboard
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_selectedFile != null)
                {
                    HideDetailPanel();
                    RenderContent();
                }
                else if (_currentFolder != null)
                {
                    NavigateToRoot();
                }
                else
                {
                    Close();
                }
            }
            base.OnKeyDown(e);
        }

        // Mouse back/forward buttons
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1 && _currentFolder != null)
            {
                HideDetailPanel();
                NavigateToRoot();
            }
            base.OnMouseDown(e);
        }

        // ===============================================
        // HELPERS
        // ===============================================
        private static MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        private static Color ColorFromHex(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16)
            );
        }

        private static SolidColorBrush BrushFromHex(string hex)
        {
            return new SolidColorBrush(ColorFromHex(hex));
        }

        // ===============================================
        // MOCK DATA CLASSES
        // ===============================================
        private class MockFolder
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string OrderCode { get; set; } = "";
            public int FileCount { get; set; }
            public string Size { get; set; } = "";
            public string LastModified { get; set; } = "";
            public string Color { get; set; } = "#3B82F6";
        }

        private class MockFile
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Extension { get; set; } = "";
            public string Size { get; set; } = "";
            public string UploadedBy { get; set; } = "";
            public string UploadDate { get; set; } = "";
            public string Type { get; set; } = "";
        }
    }
}
