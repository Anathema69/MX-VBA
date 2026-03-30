# IMA Drive V2 — Mejoras de Diseño y Correcciones

## Resumen del Análisis

Después de revisar DriveV2Window.xaml (622 líneas), DriveV2Window.xaml.cs (783 líneas), 
el export de Figma y las capturas de pantalla, identifiqué **12 problemas** organizados 
por prioridad.

---

## PROBLEMA 1: Context Menu (clic derecho) desfasado

**Archivo:** `DriveV2Window.xaml` (Resources) + `DriveV2Window.xaml.cs` (MakeFolderCard, MakeFileCard)

**Causa:** Se usa `ContextMenu` nativo de WPF sin estilos custom. El menú tiene 
bordes gruesos, fondo gris claro, y no combina con el diseño moderno.

**Solución:** Agregar estos estilos en `Window.Resources`:

```xml
<!-- ============================================= -->
<!-- CONTEXT MENU STYLE                            -->
<!-- ============================================= -->
<Style TargetType="ContextMenu">
    <Setter Property="Background" Value="White"/>
    <Setter Property="BorderBrush" Value="#E2E8F0"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="4"/>
    <Setter Property="HasDropShadow" Value="True"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ContextMenu">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="10" Padding="4"
                        Effect="{StaticResource CardShadow}">
                    <StackPanel IsItemsHost="True"
                                KeyboardNavigation.DirectionalNavigation="Cycle"/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style TargetType="MenuItem">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="#334155"/>
    <Setter Property="Padding" Value="12,8"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="MenuItem">
                <Border x:Name="Bg" Background="Transparent"
                        CornerRadius="6" Padding="{TemplateBinding Padding}">
                    <ContentPresenter ContentSource="Header"
                                      VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsHighlighted" Value="True">
                        <Setter TargetName="Bg" Property="Background" Value="#F1F5F9"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style TargetType="Separator">
    <Setter Property="Height" Value="1"/>
    <Setter Property="Margin" Value="8,4"/>
    <Setter Property="Background" Value="#F1F5F9"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Separator">
                <Border Background="{TemplateBinding Background}"
                        Height="{TemplateBinding Height}"/>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

También agregar este efecto reutilizable:
```xml
<DropShadowEffect x:Key="CardShadow" Color="#1E293B" BlurRadius="16"
                   ShadowDepth="4" Opacity="0.08" Direction="270"/>
```

---

## PROBLEMA 2: Animación de carga terrible

**Archivo:** `DriveV2Window.xaml` (LoadingText) + `DriveV2Window.xaml.cs` (LoadCurrentFolder)

**Causa:** Solo muestra un TextBlock "Cargando..." estático. Sin spinner ni animación.

**Solución:** Reemplazar el TextBlock por un spinner animado. En el XAML, 
reemplazar líneas 472-476:

```xml
<!-- Loading indicator con spinner -->
<StackPanel x:Name="LoadingPanel" HorizontalAlignment="Center"
            VerticalAlignment="Center" Margin="0,80,0,0"
            Visibility="Collapsed">
    <Border Width="48" Height="48" HorizontalAlignment="Center"
            Margin="0,0,0,16">
        <Border x:Name="SpinnerBorder" Width="48" Height="48"
                BorderBrush="{StaticResource PrimaryBrush}"
                BorderThickness="3" CornerRadius="24"
                RenderTransformOrigin="0.5,0.5">
            <Border.OpacityMask>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#FF000000" Offset="0"/>
                    <GradientStop Color="#00000000" Offset="1"/>
                </LinearGradientBrush>
            </Border.OpacityMask>
            <Border.RenderTransform>
                <RotateTransform x:Name="SpinnerRotation"/>
            </Border.RenderTransform>
        </Border>
    </Border>
    <TextBlock Text="Cargando archivos..." FontSize="14"
               Foreground="{StaticResource TextMutedBrush}"
               HorizontalAlignment="Center"/>
</StackPanel>
```

En Window.Resources agregar el Storyboard:
```xml
<Storyboard x:Key="SpinnerStoryboard" RepeatBehavior="Forever">
    <DoubleAnimation Storyboard.TargetName="SpinnerRotation"
                     Storyboard.TargetProperty="Angle"
                     From="0" To="360" Duration="0:0:1"/>
</Storyboard>
```

En el code-behind, actualizar LoadCurrentFolder:
- Reemplazar `LoadingText.Visibility = Visibility.Visible` por:
```csharp
LoadingPanel.Visibility = Visibility.Visible;
((Storyboard)FindResource("SpinnerStoryboard")).Begin();
```
- Reemplazar `LoadingText.Visibility = Visibility.Collapsed` por:
```csharp
LoadingPanel.Visibility = Visibility.Collapsed;
((Storyboard)FindResource("SpinnerStoryboard")).Stop();
```

---

## PROBLEMA 3: Panel de Upload sin diseño

**Archivo:** `DriveV2Window.xaml` (UploadPanel) + `DriveV2Window.xaml.cs` (Upload_Click)

**Causa:** El panel de upload usa ProgressBar nativo de WPF con IsIndeterminate 
que da una animación básica verde. Los estados "Pendiente", "Subiendo", "Listo" 
no tienen diseño.

**Solución:** Reemplazar el UploadPanel en XAML (líneas 480-488):

```xml
<Border x:Name="UploadPanel" Grid.Row="3"
        Background="White" BorderBrush="#E2E8F0"
        BorderThickness="0,1,0,0" Padding="0"
        VerticalAlignment="Bottom" MaxHeight="200"
        Visibility="Collapsed">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <!-- Header -->
        <Border Grid.Row="0" Padding="16,10" Background="#F8FAFC"
                BorderBrush="#E2E8F0" BorderThickness="0,0,0,1">
            <Grid>
                <TextBlock x:Name="UploadHeaderText"
                           Text="Subiendo archivos..."
                           FontSize="13" FontWeight="SemiBold"
                           Foreground="#0F172A"
                           VerticalAlignment="Center"/>
                <Button Style="{StaticResource IconButton}"
                        HorizontalAlignment="Right"
                        Click="CloseUploadPanel_Click">
                    <TextBlock Text="&#xE711;"
                               FontFamily="Segoe MDL2 Assets"
                               FontSize="10" Foreground="#94A3B8"/>
                </Button>
            </Grid>
        </Border>
        <!-- File list -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto"
                      Padding="12,8">
            <StackPanel x:Name="UploadItemsPanel"/>
        </ScrollViewer>
    </Grid>
</Border>
```

En el code-behind, mejorar la creación de filas de upload en Upload_Click.
Reemplazar la sección de creación de rows (alrededor de línea 588-598):

```csharp
foreach (var fp in dlg.FileNames)
{
    var fn = System.IO.Path.GetFileName(fp);
    var fileSize = new System.IO.FileInfo(fp).Length;
    
    // Row container
    var row = new Border 
    { 
        CornerRadius = new CornerRadius(8), 
        Padding = new Thickness(12, 8, 12, 8), 
        Margin = new Thickness(0, 2, 0, 2),
        Background = Brushes.White
    };
    var rowGrid = new Grid();
    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    
    // File icon
    var (cH, bH) = GetFileCfg(fn);
    var iconBorder = new Border 
    { 
        Width = 32, Height = 32, CornerRadius = new CornerRadius(6),
        Background = BrushHex(bH), Margin = new Thickness(0, 0, 10, 0)
    };
    iconBorder.Child = new TextBlock 
    { 
        Text = FileIcon(fn), FontFamily = new FontFamily("Segoe MDL2 Assets"),
        FontSize = 14, Foreground = BrushHex(cH),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    Grid.SetColumn(iconBorder, 0);
    rowGrid.Children.Add(iconBorder);
    
    // Name + size
    var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
    infoPanel.Children.Add(new TextBlock 
    { 
        Text = fn, FontSize = 12, FontWeight = FontWeights.Medium,
        Foreground = TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis 
    });
    infoPanel.Children.Add(new TextBlock 
    { 
        Text = Services.Drive.DriveService.FormatFileSize(fileSize),
        FontSize = 11, Foreground = TextMuted 
    });
    Grid.SetColumn(infoPanel, 1);
    rowGrid.Children.Add(infoPanel);
    
    // Status badge
    var statusBadge = new Border 
    { 
        CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
        Background = HoverBg, VerticalAlignment = VerticalAlignment.Center
    };
    var sb = new TextBlock 
    { 
        Text = "Pendiente", FontSize = 11, FontWeight = FontWeights.Medium,
        Foreground = TextMuted
    };
    statusBadge.Child = sb;
    Grid.SetColumn(statusBadge, 2);
    rowGrid.Children.Add(statusBadge);
    
    row.Child = rowGrid;
    UploadItemsPanel.Children.Add(row);
    
    // Progress bar below the row
    var pb = new Border 
    { 
        Height = 3, CornerRadius = new CornerRadius(2),
        Background = BorderLight, Margin = new Thickness(0, 0, 0, 4),
        ClipToBounds = true
    };
    var pbFill = new Border 
    { 
        CornerRadius = new CornerRadius(2), Width = 0,
        Background = Primary, HorizontalAlignment = HorizontalAlignment.Left
    };
    pb.Child = pbFill;
    UploadItemsPanel.Children.Add(pb);
    
    tr[fp] = (sb, statusBadge, pbFill);
}
```

Y actualizar los estados en el loop de upload:
```csharp
// Subiendo
sb.Text = "Subiendo..."; sb.Foreground = Primary;
statusBadge.Background = ActiveBg;

// Listo
sb.Text = "Listo ✓"; sb.Foreground = GreenOk;
statusBadge.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4));

// Error
sb.Text = "Error"; sb.Foreground = Destructive;
statusBadge.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2));
```

Agregar handler:
```csharp
private void CloseUploadPanel_Click(object sender, RoutedEventArgs e) 
    => UploadPanel.Visibility = Visibility.Collapsed;
```

---

## PROBLEMA 4: Nombres de carpeta truncados / ilegibles

**Archivo:** `DriveV2Window.xaml.cs` (MakeFolderCard, ~línea 400)

**Causa:** `MaxWidth = 200` en el TextBlock del nombre es muy restrictivo.
Además, cuando el nombre incluye el código OC + descripción larga, se corta mal.

**Solución:** En MakeFolderCard, cambiar:
```csharp
// ANTES
var nameT = new TextBlock { ..., MaxWidth = 200, ... };

// DESPUÉS - quitar MaxWidth y usar TextWrapping
var nameT = new TextBlock 
{ 
    Text = folder.Name, 
    FontSize = 14, 
    FontWeight = FontWeights.SemiBold, 
    Foreground = TextPrimary, 
    TextTrimming = TextTrimming.CharacterEllipsis, 
    TextWrapping = TextWrapping.NoWrap,
    Margin = new Thickness(0, 0, 0, 2), 
    ToolTip = folder.Name 
};
```

También ajustar el Grid del header (hg) para que la columna del nombre 
use Star width:
```csharp
hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // icon
hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // more btn
```

---

## PROBLEMA 5: Almacenamiento no muestra tamaño real de R2

**Archivo:** `DriveV2Window.xaml.cs`

**Causa:** El StorageLabel se inicializa con "0 B de 10 GB" pero nunca se 
actualiza con el tamaño real del bucket.

**Solución:** Agregar un método para calcular el total al final de LoadCurrentFolder,
después del Phase 4:

```csharp
// PHASE 5: Update storage (non-blocking)
_ = Task.Run(async () =>
{
    try
    {
        var allFiles = await SupabaseService.Instance.GetAllDriveFiles(_cts.Token);
        var totalBytes = allFiles.Sum(f => f.FileSize ?? 0);
        var totalStr = Services.Drive.DriveService.FormatFileSize(totalBytes);
        var maxGb = 10; // o 100 si ya tienes plan
        var maxBytes = (long)maxGb * 1024 * 1024 * 1024;
        var pct = Math.Min(100, (double)totalBytes / maxBytes * 100);
        
        Dispatcher.Invoke(() =>
        {
            StorageLabel.Text = $"{totalStr} de {maxGb} GB";
            StorageAvailLabel.Text = $"{Services.Drive.DriveService.FormatFileSize(maxBytes - totalBytes)} disponibles";
            // Actualizar barra de progreso
            var fill = Math.Max(1, (int)pct);
            StorageFillCol.Width = new GridLength(fill, GridUnitType.Star);
            StorageEmptyCol.Width = new GridLength(100 - fill, GridUnitType.Star);
        });
    }
    catch { /* silently fail */ }
});
```

**Nota:** Si `GetAllDriveFiles` no existe, necesitarás un método en 
SupabaseService que haga un SELECT con SUM(file_size) sobre drive_files.
Algo como:
```csharp
public async Task<long> GetDriveTotalStorageBytes(CancellationToken ct)
{
    var result = await _client.From<DriveFileDb>()
        .Select("file_size")
        .Get(ct);
    return result.Models.Sum(f => f.FileSize ?? 0);
}
```

---

## PROBLEMA 6: Íconos Segoe MDL2 que no cargan

**Archivo:** `DriveV2Window.xaml.cs` (FileIcon method, línea 756)

**Causa:** Algunos glifos de Segoe MDL2 Assets no existen en todas las versiones 
de Windows. Por ejemplo, `\uED41` (ubicación) y `\uEDA2` (tamaño) pueden no 
renderizar.

**Solución:** Usar solo glifos seguros. Reemplazar el método FileIcon:

```csharp
private static string FileIcon(string fn) 
{
    var e = System.IO.Path.GetExtension(fn)?.ToLowerInvariant();
    return e switch 
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\uE91B",  // Photo
        ".mp4" or ".avi" or ".mkv" or ".mov" => "\uE714",   // Video
        ".zip" or ".rar" or ".7z" => "\uE8C8",              // Archive
        ".pdf" => "\uEA90",                                   // PDF
        ".doc" or ".docx" => "\uE8A5",                       // Document
        ".xls" or ".xlsx" or ".csv" => "\uE80B",            // Table
        ".ppt" or ".pptx" => "\uE8A5",                      // Presentation
        ".dwg" or ".dxf" or ".step" or ".stp" => "\uE8FD",  // Engineering
        ".log" or ".txt" => "\uE8A5",                        // Text
        _ => "\uE8A5"                                         // Generic file
    };
}
```

Y en ShowDetail, reemplazar los íconos problemáticos:
```csharp
foreach (var (ico, lbl, val) in new[] {
    ("\uE8A5", "Tipo", FriendlyType(file.FileName)),       // Document icon
    ("\uE7F8", "Tamaño", Services.Drive.DriveService.FormatFileSize(file.FileSize)),  // Storage
    ("\uE787", "Fecha de subida", file.UploadedAt?.ToString("dd 'de' MMMM, yyyy") ?? "Sin fecha"),  // Calendar
    ("\uE77B", "Subido por", uploader),                     // Person
    ("\uE8B7", "Ubicación", loc)                            // Folder
})
```

---

## PROBLEMA 7: Dialogs (Nueva carpeta, Vincular) con diseño genérico

**Archivo:** `DriveV2Window.xaml.cs` (Prompt method, OrderDialog method)

**Causa:** Los dialogs usan Window de WPF nativo con WindowStyle.ToolWindow 
que tiene la barra de título antigua de Windows.

**Solución:** Reescribir el método Prompt para usar WindowStyle.None:

```csharp
private static string Prompt(string title, string label, string def)
{
    var w = new Window 
    { 
        Title = title, Width = 420, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, 
        ResizeMode = ResizeMode.NoResize, 
        WindowStyle = WindowStyle.None,
        AllowsTransparency = true,
        Background = Brushes.Transparent
    };
    
    var card = new Border 
    { 
        Background = Brushes.White, CornerRadius = new CornerRadius(12),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
        BorderThickness = new Thickness(1),
        Effect = new System.Windows.Media.Effects.DropShadowEffect 
        { 
            Color = Color.FromRgb(0x1E, 0x29, 0x3B), BlurRadius = 24,
            ShadowDepth = 8, Opacity = 0.12
        },
        Margin = new Thickness(16)
    };
    
    var p = new StackPanel { Margin = new Thickness(24) };
    
    // Title
    p.Children.Add(new TextBlock 
    { 
        Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
        Margin = new Thickness(0, 0, 0, 16)
    });
    
    // Label
    p.Children.Add(new TextBlock 
    { 
        Text = label, FontSize = 13,
        Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
        Margin = new Thickness(0, 0, 0, 8)
    });
    
    // TextBox con estilo
    var tb = new TextBox 
    { 
        Text = def, FontSize = 14, Padding = new Thickness(12, 10, 12, 10),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
        BorderThickness = new Thickness(1)
    };
    // Nota: CornerRadius en TextBox requiere un template custom o usar una Border wrapper
    var tbBorder = new Border 
    { 
        CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
        BorderThickness = new Thickness(1), Background = Brushes.White,
        ClipToBounds = true
    };
    tb.BorderThickness = new Thickness(0);
    tbBorder.Child = tb;
    tb.SelectAll();
    p.Children.Add(tbBorder);
    
    // Botones
    var bp = new StackPanel 
    { 
        Orientation = Orientation.Horizontal, 
        HorizontalAlignment = HorizontalAlignment.Right, 
        Margin = new Thickness(0, 20, 0, 0) 
    };
    
    string? res = null;
    
    var cancel = new Button 
    { 
        Content = "Cancelar", Width = 90, Padding = new Thickness(0, 8, 0, 8),
        Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
        Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
        BorderThickness = new Thickness(1),
        FontWeight = FontWeights.Medium,
        Cursor = Cursors.Hand,
        IsCancel = true
    };
    
    var ok = new Button 
    { 
        Content = "Aceptar", Width = 90, Padding = new Thickness(0, 8, 0, 8),
        Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        FontWeight = FontWeights.SemiBold,
        Cursor = Cursors.Hand,
        IsDefault = true,
        Margin = new Thickness(8, 0, 0, 0)
    };
    ok.Click += (s, e) => { res = tb.Text; w.Close(); };
    
    bp.Children.Add(cancel);
    bp.Children.Add(ok);
    p.Children.Add(bp);
    
    card.Child = p;
    w.Content = card;
    w.Loaded += (s, e) => tb.Focus();
    w.MouseLeftButtonDown += (s, e) => w.DragMove();
    w.ShowDialog();
    return res ?? "";
}
```

Aplicar el mismo patrón para OrderDialog (línea 664+). Misma estructura:
WindowStyle.None, AllowsTransparency, Border con CornerRadius, y los mismos 
colores del sistema de diseño.

---

## PROBLEMA 8: Grid de carpetas no es responsive

**Archivo:** `DriveV2Window.xaml.cs` (RenderGrid, línea 306-312)

**Causa:** Usa `UniformGrid { Columns = 3 o 4 }` fijo, que no se adapta al 
tamaño de ventana. En pantallas pequeñas las cards se comprimen.

**Solución:** Reemplazar UniformGrid por WrapPanel:

```csharp
private void RenderGrid()
{
    var wrap = new WrapPanel();
    foreach (var f in _currentFolders)
    {
        var card = MakeFolderCard(f);
        card.Width = 280; // ancho fijo para cada card
        card.Margin = new Thickness(6);
        wrap.Children.Add(card);
    }
    foreach (var f in _currentFiles)
    {
        var card = MakeFileCard(f);
        card.Width = 220;
        card.Margin = new Thickness(6);
        wrap.Children.Add(card);
    }
    ContentHost.Content = wrap;
}
```

Nota: Quitar el `Margin = new Thickness(8)` de dentro de MakeFolderCard y 
MakeFileCard ya que ahora se maneja desde RenderGrid.

---

## PROBLEMA 9: Tooltip de orden vinculada básico

**Archivo:** `DriveV2Window.xaml.cs` (GetOrderTooltip, MakeFolderCard)

**Causa:** El tooltip es solo texto plano. El diseño de Figma (captura 
"sugerencia_de_info_orden.png") muestra un tooltip rico con badge de estado, 
cliente y detalle separados.

**Solución:** Reemplazar el tooltip de string por un UIElement:

```csharp
private UIElement? MakeOrderTooltip(int? orderId)
{
    if (!orderId.HasValue || !_orderInfoCache.TryGetValue(orderId.Value, out var oi))
        return null;
    
    var panel = new StackPanel { MaxWidth = 280 };
    
    // Header: Orden + Badge
    var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
    header.Children.Add(new TextBlock 
    { 
        Text = $"Orden: {oi.Po}", FontSize = 13, FontWeight = FontWeights.SemiBold,
        Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center 
    });
    // Badge
    var badge = new Border 
    { 
        Background = new SolidColorBrush(Color.FromRgb(0xED, 0xEF, 0xF2)),
        CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2),
        Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
    };
    badge.Child = new TextBlock 
    { 
        Text = "VINCULADA", FontSize = 10, FontWeight = FontWeights.SemiBold,
        Foreground = TextMuted
    };
    header.Children.Add(badge);
    panel.Children.Add(header);
    
    // Info lines
    var infoBorder = new Border 
    { 
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
        BorderThickness = new Thickness(2, 0, 0, 0),
        Padding = new Thickness(10, 4, 0, 4)
    };
    var infoStack = new StackPanel();
    if (!string.IsNullOrEmpty(oi.Client))
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = "Cliente:", FontSize = 12, Foreground = TextMuted, Width = 55 });
        row.Children.Add(new TextBlock { Text = oi.Client, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TextPrimary });
        infoStack.Children.Add(row);
    }
    if (!string.IsNullOrEmpty(oi.Detail))
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = "Detalle:", FontSize = 12, Foreground = TextMuted, Width = 55 });
        row.Children.Add(new TextBlock { Text = oi.Detail, FontSize = 12, Foreground = TextPrimary, TextWrapping = TextWrapping.Wrap });
        infoStack.Children.Add(row);
    }
    infoBorder.Child = infoStack;
    panel.Children.Add(infoBorder);
    
    return panel;
}
```

Y en MakeFolderCard, cambiar el ToolTip de texto a este método:
```csharp
// ANTES
ToolTip = GetOrderTooltip(folder.LinkedOrderId)

// DESPUÉS
ToolTip = MakeOrderTooltip(folder.LinkedOrderId)
```

También aplicar estilo al ToolTip globalmente en Window.Resources:
```xml
<Style TargetType="ToolTip">
    <Setter Property="Background" Value="White"/>
    <Setter Property="BorderBrush" Value="#E2E8F0"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="12"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="ToolTip">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="8" Padding="{TemplateBinding Padding}"
                        Effect="{StaticResource CardShadow}">
                    <ContentPresenter/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## PROBLEMA 10: Barra de búsqueda no filtra

**Archivo:** `DriveV2Window.xaml.cs` (SearchBox_TextChanged)

**Causa:** El handler solo oculta/muestra el placeholder pero no filtra contenido.

**Solución:** Implementar filtrado real:

```csharp
private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) 
{
    SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) 
        ? Visibility.Visible : Visibility.Collapsed;
    
    // Debounce: esperar 300ms antes de filtrar
    var query = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
    await Task.Delay(300);
    if (SearchBox.Text?.Trim().ToLowerInvariant() != query) return; // cancelado por nuevo input
    
    if (string.IsNullOrEmpty(query))
    {
        RenderContent();
        return;
    }
    
    // Filtrar folders y files visibles
    var filteredFolders = _currentFolders
        .Where(f => f.Name.ToLowerInvariant().Contains(query) ||
                     (f.LinkedOrderId.HasValue && GetOrderDisplayText(f.LinkedOrderId).ToLowerInvariant().Contains(query)))
        .ToList();
    var filteredFiles = _currentFiles
        .Where(f => f.FileName.ToLowerInvariant().Contains(query))
        .ToList();
    
    // Renderizar filtrado
    var wrap = new WrapPanel();
    foreach (var f in filteredFolders) wrap.Children.Add(MakeFolderCard(f));
    foreach (var f in filteredFiles) wrap.Children.Add(MakeFileCard(f));
    ContentHost.Content = wrap;
    
    StatusText.Text = $"{filteredFolders.Count + filteredFiles.Count} resultado(s)";
    EmptyState.Visibility = (filteredFolders.Count + filteredFiles.Count) == 0 
        ? Visibility.Visible : Visibility.Collapsed;
}
```

---

## PROBLEMA 11: Window no es resizable

**Archivo:** `DriveV2Window.xaml` (línea 9)

**Causa:** `ResizeMode="NoResize"` impide redimensionar la ventana.

**Solución:** Cambiar a:
```xml
ResizeMode="CanResize"
```

Y quitar `Height="700" Width="1100"` ya que la ventana se maximiza 
en el constructor via WindowHelper.MaximizeToCurrentMonitor.

---

## PROBLEMA 12: Sidebar con sección "Filtrar por tipo" no funcional

**Archivo:** `DriveV2Window.xaml.cs` (MakeFilterItem, InitializeSidebar)

**Causa:** Los contadores muestran "--" hardcodeado y los filtros no hacen nada.

**Solución:** Actualizar los contadores al final de LoadCurrentFolder, Phase 4:

```csharp
// Actualizar contadores de tipo en sidebar
UpdateFilterCounts();
```

Agregar método:
```csharp
private void UpdateFilterCounts()
{
    // Contar todos los archivos recursivamente sería costoso,
    // por ahora contar los de la carpeta actual
    var counts = new Dictionary<string, int>
    {
        ["pdf"] = _currentFiles.Count(f => f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)),
        ["img"] = _currentFiles.Count(f => new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }
            .Any(ext => f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))),
        ["cad"] = _currentFiles.Count(f => new[] { ".dwg", ".dxf", ".step", ".stp" }
            .Any(ext => f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))),
        ["xls"] = _currentFiles.Count(f => new[] { ".xls", ".xlsx", ".csv" }
            .Any(ext => f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))),
        ["vid"] = _currentFiles.Count(f => new[] { ".mp4", ".avi", ".mkv", ".mov" }
            .Any(ext => f.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))),
    };
    
    var types = new[] { "pdf", "img", "cad", "xls", "vid" };
    for (int i = 0; i < FilterPanel.Children.Count && i < types.Length; i++)
    {
        if (FilterPanel.Children[i] is Border b && b.Child is Grid g)
        {
            var countText = g.Children.OfType<TextBlock>()
                .FirstOrDefault(t => t.HorizontalAlignment == HorizontalAlignment.Right);
            if (countText != null)
                countText.Text = counts.TryGetValue(types[i], out var c) ? c.ToString() : "0";
        }
    }
}
```

---

## Resumen de Plugins/NuGet necesarios

No se necesitan plugins adicionales. Todo se resuelve con WPF nativo:
- `Segoe MDL2 Assets` (ya incluida en Windows 10/11)
- `System.Windows.Media.Effects.DropShadowEffect` (nativo)
- `Storyboard` + `DoubleAnimation` (nativo)
- `WrapPanel` (nativo)

---

## Orden de implementación sugerido

1. **Context Menu** (Problema 1) — impacto visual inmediato
2. **Spinner de carga** (Problema 2) — mejora percepción de velocidad
3. **Dialogs modernos** (Problema 7) — Nueva carpeta y Vincular
4. **Upload panel** (Problema 3) — experiencia de subida
5. **Nombres de carpeta** (Problema 4) — legibilidad
6. **Grid responsive** (Problema 8) — adaptar a tamaños
7. **Íconos** (Problema 6) — fixes de rendering
8. **Tooltip de orden** (Problema 9) — info rica
9. **Storage real** (Problema 5) — dato real del R2
10. **Búsqueda** (Problema 10) — funcionalidad
11. **Resize** (Problema 11) — quick fix
12. **Filtros sidebar** (Problema 12) — nice-to-have
