using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Postgrest.Attributes;
using Postgrest.Models;
using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Ventana de Balance Profesional con diseño mejorado
    /// </summary>
    public partial class BalanceWindowPro : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private int _currentYear;
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");
        private List<BalanceCompletoDb> _balanceData;

        // Colores minimalistas con acentos
        private static class BalanceColors
        {
            // Fondos
            public static readonly Color Background = Color.FromRgb(250, 250, 250);     // #FAFAFA
            public static readonly Color White = Color.FromRgb(255, 255, 255);
            public static readonly Color HeaderBg = Color.FromRgb(244, 244, 245);       // #F4F4F5
            public static readonly Color TotalRowBg = Color.FromRgb(250, 250, 250);     // #FAFAFA

            // Columna Total Anual - destacada con azul suave
            public static readonly Color TotalColumnBg = Color.FromRgb(239, 246, 255);  // #EFF6FF - Azul muy claro
            public static readonly Color TotalColumnBorder = Color.FromRgb(147, 197, 253); // #93C5FD - Azul medio

            // Bordes
            public static readonly Color Border = Color.FromRgb(229, 229, 229);         // #E5E5E5
            public static readonly Color BorderDark = Color.FromRgb(212, 212, 216);     // #D4D4D8

            // Textos
            public static readonly Color TextPrimary = Color.FromRgb(24, 24, 27);       // #18181B
            public static readonly Color TextSecondary = Color.FromRgb(113, 113, 122);  // #71717A
            public static readonly Color TextMuted = Color.FromRgb(161, 161, 170);      // #A1A1AA

            // Acentos para secciones
            public static readonly Color AccentGastos = Color.FromRgb(244, 63, 94);     // #F43F5E - Rosa/Rojo
            public static readonly Color AccentIngresos = Color.FromRgb(16, 185, 129);  // #10B981 - Verde
            public static readonly Color AccentVentas = Color.FromRgb(245, 158, 11);    // #F59E0B - Amarillo
            public static readonly Color AccentResultado = Color.FromRgb(59, 130, 246); // #3B82F6 - Azul

            // Semáforo
            public static readonly Color Red = Color.FromRgb(239, 68, 68);              // #EF4444
            public static readonly Color RedLight = Color.FromRgb(254, 226, 226);       // #FEE2E2
            public static readonly Color Yellow = Color.FromRgb(245, 158, 11);          // #F59E0B
            public static readonly Color YellowLight = Color.FromRgb(254, 249, 195);    // #FEF9C3
            public static readonly Color Green = Color.FromRgb(34, 197, 94);            // #22C55E
            public static readonly Color GreenLight = Color.FromRgb(220, 252, 231);     // #DCFCE7
        }

        private int _selectedRowIndex = -1;
        private int _selectedColumnIndex = -1;
        private List<Border> _highlightedCells = new List<Border>();
        private int _currentMonth = DateTime.Now.Month; // Mes actual (1-12)

        public BalanceWindowPro(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _currentYear = DateTime.Now.Year;

            // Inicializar año
            txtYear.Text = _currentYear.ToString();

            // Actualizar timestamp
            txtLastUpdate.Text = $"Actualizado: {DateTime.Now:dd/MM/yyyy HH:mm}";

            // Cargar datos
            _ = LoadBalanceData();
        }

        private async Task LoadBalanceData()
        {
            try
            {
                // Obtener datos de la vista v_balance_completo
                var supabaseClient = _supabaseService.GetClient();
                var response = await supabaseClient
                    .From<BalanceCompletoDb>()
                    .Where(b => b.Año == _currentYear)
                    .Order("mes_numero", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                _balanceData = response?.Models ?? new List<BalanceCompletoDb>();

                if (!_balanceData.Any())
                {
                    ShowEmptyState();
                    return;
                }

                // Construir la tabla
                BuildBalanceTable();

                // Actualizar KPIs
                UpdateKPIs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar balance: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowEmptyState();
            }
        }

        private void BuildBalanceTable()
        {
            // Ocultar estado vacío
            EmptyState.Visibility = Visibility.Collapsed;

            // Limpiar grid
            gridBalance.Children.Clear();
            gridBalance.RowDefinitions.Clear();
            gridBalance.ColumnDefinitions.Clear();

            // Crear columnas proporcionales para adaptarse a diferentes tamaños de pantalla
            // Concepto: 160px fijo, Meses: proporcionales (*), Total: 120px fijo
            gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            for (int i = 1; i <= 12; i++)
            {
                gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 85 });
            }
            gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            int currentRow = 0;

            // Headers de meses
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddMonthHeaders(currentRow);
            currentRow++;

            // Preparar arrays de datos
            var nomina = new decimal[12];
            var horasExtra = new decimal[12];
            var gastosFijos = new decimal[12];
            var gastosVariables = new decimal[12];
            var gastoOperativo = new decimal[12];
            var gastoIndirecto = new decimal[12];
            var totalGastos = new decimal[12];
            var ingresosEsperados = new decimal[12];
            var ingresosPercibidos = new decimal[12];
            var diferencia = new decimal[12];
            var ventasTotales = new decimal[12];
            var utilidadAproximada = new decimal[12];

            foreach (var dato in _balanceData)
            {
                int mesIndex = dato.MesNumero - 1;
                if (mesIndex >= 0 && mesIndex < 12)
                {
                    // Gastos
                    nomina[mesIndex] = dato.Nomina;
                    horasExtra[mesIndex] = dato.HorasExtra;
                    gastosFijos[mesIndex] = dato.GastosFijos;
                    gastosVariables[mesIndex] = dato.GastosVariables;
                    gastoOperativo[mesIndex] = dato.GastoOperativo;
                    gastoIndirecto[mesIndex] = dato.GastoIndirecto;
                    totalGastos[mesIndex] = dato.TotalGastos;

                    // Ingresos - Ahora usando valores correctos de la BD
                    ingresosEsperados[mesIndex] = dato.IngresosEsperados;
                    ingresosPercibidos[mesIndex] = dato.IngresosPercibidos;
                    diferencia[mesIndex] = dato.Diferencia;

                    // Ventas
                    ventasTotales[mesIndex] = dato.VentasTotales;

                    // Utilidad - Calculada en la BD
                    utilidadAproximada[mesIndex] = dato.UtilidadAproximada;
                }
            }

            // SECCIÓN GASTOS
            AddSectionHeader("Gastos", currentRow);
            currentRow++;

            AddDataRow("Nómina", nomina, currentRow, false);
            currentRow++;

            AddDataRow("Horas Extra", horasExtra, currentRow, false);
            currentRow++;

            AddDataRow("Gastos Fijos", gastosFijos, currentRow, false);
            currentRow++;

            AddDataRow("Gastos Variables", gastosVariables, currentRow, false);
            currentRow++;

            AddDataRow("Gasto Operativo", gastoOperativo, currentRow, false);
            currentRow++;

            AddDataRow("Gasto Indirecto", gastoIndirecto, currentRow, false);
            currentRow++;

            // Total Gastos (renglón de suma con estilo destacado)
            AddDataRow("Total Gastos", totalGastos, currentRow, true);
            currentRow++;

            // SECCIÓN INGRESOS
            AddSectionHeader("Ingresos", currentRow);
            currentRow++;

            AddDataRow("Ingresos Esperados", ingresosEsperados, currentRow, false);
            currentRow++;

            AddDataRow("Ingresos Percibidos", ingresosPercibidos, currentRow, false);
            currentRow++;

            AddDataRow("Diferencia", diferencia, currentRow, true, null, false, true);
            currentRow++;

            // SECCIÓN VENTAS CON SEMÁFORO
            AddSectionHeader("Ventas", currentRow);
            currentRow++;

            AddSemaforoDataRow("Ventas Totales", ventasTotales, nomina, gastosFijos, currentRow);
            currentRow++;

            // SECCIÓN UTILIDAD
            AddSectionHeader("Resultado", currentRow);
            currentRow++;

            AddUtilidadDataRow("Utilidad Aproximada", utilidadAproximada, currentRow);
        }

        private void AddMonthHeaders(int row)
        {
            // Celda de encabezado "Concepto"
            var conceptHeader = new Border
            {
                Background = new SolidColorBrush(BalanceColors.HeaderBg),
                BorderBrush = new SolidColorBrush(BalanceColors.Border),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Height = 44
            };
            var conceptText = new TextBlock
            {
                Text = "Concepto",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(BalanceColors.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            conceptHeader.Child = conceptText;
            Grid.SetRow(conceptHeader, row);
            Grid.SetColumn(conceptHeader, 0);
            gridBalance.Children.Add(conceptHeader);

            // Headers de meses
            string[] meses = { "Ene", "Feb", "Mar", "Abr", "May", "Jun",
                              "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

            // Color para mes actual
            var currentMonthBg = Color.FromRgb(219, 234, 254);      // #DBEAFE - Azul claro
            var currentMonthText = Color.FromRgb(29, 78, 216);      // #1D4ED8 - Azul oscuro

            for (int i = 0; i < 12; i++)
            {
                int colIndex = i + 1;
                bool isCurrentMonth = (i + 1) == _currentMonth && _currentYear == DateTime.Now.Year;

                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(isCurrentMonth ? currentMonthBg : BalanceColors.HeaderBg),
                    BorderBrush = new SolidColorBrush(BalanceColors.Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = 44,
                    Tag = $"header_col_{colIndex}",
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // Para el mes actual, usar un StackPanel con el nombre y un indicador
                if (isCurrentMonth)
                {
                    var headerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var headerText = new TextBlock
                    {
                        Text = meses[i],
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(currentMonthText)
                    };

                    // Indicador de mes actual (dot)
                    var dot = new System.Windows.Shapes.Ellipse
                    {
                        Width = 6,
                        Height = 6,
                        Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246)), // #3B82F6
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    headerPanel.Children.Add(headerText);
                    headerPanel.Children.Add(dot);
                    headerBorder.Child = headerPanel;
                }
                else
                {
                    var headerText = new TextBlock
                    {
                        Text = meses[i],
                        FontWeight = FontWeights.Medium,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(BalanceColors.TextSecondary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    headerBorder.Child = headerText;
                }

                // Evento para resaltar columna
                headerBorder.MouseLeftButtonDown += (s, e) => HighlightColumn(colIndex);
                var normalBg = isCurrentMonth ? currentMonthBg : BalanceColors.HeaderBg;
                headerBorder.MouseEnter += (s, e) => headerBorder.Background = new SolidColorBrush(BalanceColors.Border);
                headerBorder.MouseLeave += (s, e) =>
                {
                    if (_selectedColumnIndex != colIndex)
                        headerBorder.Background = new SolidColorBrush(normalBg);
                };

                Grid.SetRow(headerBorder, row);
                Grid.SetColumn(headerBorder, colIndex);
                gridBalance.Children.Add(headerBorder);
            }

            // Header Total Anual - destacado con azul
            var totalHeaderBorder = new Border
            {
                Background = new SolidColorBrush(BalanceColors.TotalColumnBg),
                BorderBrush = new SolidColorBrush(BalanceColors.TotalColumnBorder),
                BorderThickness = new Thickness(2, 0, 0, 1),
                Height = 44
            };

            var totalHeaderText = new TextBlock
            {
                Text = "Total Anual",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175)), // #1E40AF - Azul oscuro
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            totalHeaderBorder.Child = totalHeaderText;
            Grid.SetRow(totalHeaderBorder, row);
            Grid.SetColumn(totalHeaderBorder, 13);
            gridBalance.Children.Add(totalHeaderBorder);
        }

        private void AddSectionHeader(string title, int row, Color? accentColor = null)
        {
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Determinar color de acento según la sección
            Color accent = accentColor ?? BalanceColors.Border;
            bool isVentas = title.ToLower().Contains("venta");

            if (title.ToLower().Contains("gasto"))
                accent = BalanceColors.AccentGastos;
            else if (title.ToLower().Contains("ingreso"))
                accent = BalanceColors.AccentIngresos;
            else if (isVentas)
                accent = BalanceColors.AccentVentas;
            else if (title.ToLower().Contains("resultado") || title.ToLower().Contains("utilidad"))
                accent = BalanceColors.AccentResultado;

            // Header con línea de acento a la izquierda
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(BalanceColors.TotalRowBg),
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(4, 0, 0, 0),
                Height = 32,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // Contenedor principal
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var headerText = new TextBlock
            {
                Text = title.ToUpper(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(headerText);

            // Si es VENTAS, agregar leyenda del semáforo (junto al título)
            if (isVentas)
            {
                var legendPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(24, 0, 0, 0)
                };

                // Bajo
                legendPanel.Children.Add(CreateLegendItem(BalanceColors.Red, "Bajo"));
                // Medio
                legendPanel.Children.Add(CreateLegendItem(BalanceColors.Yellow, "Medio"));
                // Meta
                legendPanel.Children.Add(CreateLegendItem(BalanceColors.Green, "Meta"));

                Grid.SetColumn(legendPanel, 1);
                headerGrid.Children.Add(legendPanel);
            }

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, row);
            Grid.SetColumn(headerBorder, 0);
            Grid.SetColumnSpan(headerBorder, 14);
            gridBalance.Children.Add(headerBorder);
        }

        private StackPanel CreateLegendItem(Color dotColor, string label)
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 0, 0, 0)
            };

            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(dotColor),
                VerticalAlignment = VerticalAlignment.Center
            };

            var text = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(BalanceColors.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };

            item.Children.Add(dot);
            item.Children.Add(text);
            return item;
        }

        // Sobrecarga para mantener compatibilidad (ignora el color viejo)
        private void AddSectionHeader(string title, Color backgroundColor, int row)
        {
            AddSectionHeader(title, row);
        }

        private void AddDataRow(string concepto, decimal[] valores, int row, bool isTotal,
    Color? backgroundColor = null, bool isEditable = false, bool showNegative = false)
        {
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Celda de concepto
            var conceptBorder = new Border
            {
                Background = new SolidColorBrush(isTotal ? BalanceColors.TotalRowBg : BalanceColors.White),
                BorderBrush = new SolidColorBrush(BalanceColors.Border),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Height = 40
            };

            var conceptText = new TextBlock
            {
                Text = concepto,
                FontWeight = isTotal ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 13,
                Foreground = new SolidColorBrush(isTotal ? BalanceColors.TextPrimary : BalanceColors.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };

            conceptBorder.Child = conceptText;
            Grid.SetRow(conceptBorder, row);
            Grid.SetColumn(conceptBorder, 0);
            gridBalance.Children.Add(conceptBorder);

            // Color para mes actual
            var currentMonthBg = Color.FromRgb(239, 246, 255);  // #EFF6FF - Azul muy claro

            // Celdas de valores mensuales
            decimal total = 0;
            for (int i = 0; i < 12; i++)
            {
                bool isCurrentMonth = (i + 1) == _currentMonth && _currentYear == DateTime.Now.Year;

                // Determinar color de fondo
                Color bgColor;
                if (isCurrentMonth)
                    bgColor = currentMonthBg;
                else if (isTotal)
                    bgColor = BalanceColors.TotalRowBg;
                else
                    bgColor = BalanceColors.White;

                var valueBorder = new Border
                {
                    Background = new SolidColorBrush(bgColor),
                    BorderBrush = new SolidColorBrush(BalanceColors.Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = 40
                };

                var value = valores[i];
                total += value;

                // Para Horas Extra, hacer las celdas editables con entrada numérica robusta
                if (concepto == "Horas Extra" && !isTotal)
                {
                    var monthIndex = i;
                    var valueTextBox = new TextBox
                    {
                        Text = value.ToString("C2", _mexicanCulture),
                        FontSize = 13,
                        FontWeight = FontWeights.Normal,
                        TextAlignment = TextAlignment.Right,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(BalanceColors.TextPrimary),
                        Padding = new Thickness(0, 0, 12, 0),
                        MaxLength = 15,
                        Tag = new { Month = i + 1, Year = _currentYear, OriginalValue = value }
                    };

                    // Al entrar en edición: mostrar número limpio y seleccionar todo
                    valueTextBox.GotFocus += (s, e) =>
                    {
                        var tb = s as TextBox;
                        tb.Background = new SolidColorBrush(BalanceColors.YellowLight);
                        var tag = (dynamic)tb.Tag;

                        // Mostrar número sin símbolo de moneda
                        decimal currentValue = tag.OriginalValue;
                        tb.Text = currentValue == 0 ? "" : currentValue.ToString("F2").Replace(",", "");

                        // Seleccionar todo para reemplazar al escribir
                        tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);
                    };

                    // Filtrar entrada: solo números y un punto decimal
                    valueTextBox.PreviewTextInput += (s, e) =>
                    {
                        var tb = s as TextBox;
                        string newChar = e.Text;

                        // Solo permitir dígitos y punto decimal
                        if (!char.IsDigit(newChar[0]) && newChar != ".")
                        {
                            e.Handled = true;
                            return;
                        }

                        // Solo un punto decimal permitido
                        if (newChar == "." && tb.Text.Contains("."))
                        {
                            e.Handled = true;
                            return;
                        }

                        // Limitar a 2 decimales
                        if (tb.Text.Contains("."))
                        {
                            int dotIndex = tb.Text.IndexOf('.');
                            int cursorPos = tb.SelectionStart;

                            // Si el cursor está después del punto
                            if (cursorPos > dotIndex)
                            {
                                string afterDot = tb.Text.Substring(dotIndex + 1);
                                // Si ya hay 2 decimales y no hay texto seleccionado, bloquear
                                if (afterDot.Length >= 2 && tb.SelectionLength == 0)
                                {
                                    e.Handled = true;
                                    return;
                                }
                            }
                        }
                    };

                    // Bloquear pegado de texto no numérico
                    DataObject.AddPastingHandler(valueTextBox, (s, e) =>
                    {
                        if (e.DataObject.GetDataPresent(typeof(string)))
                        {
                            string pastedText = (string)e.DataObject.GetData(typeof(string));
                            // Limpiar el texto pegado: solo números y punto
                            string cleaned = System.Text.RegularExpressions.Regex.Replace(pastedText, @"[^\d.]", "");

                            // Asegurar solo un punto decimal
                            int firstDot = cleaned.IndexOf('.');
                            if (firstDot >= 0)
                            {
                                cleaned = cleaned.Substring(0, firstDot + 1) +
                                         cleaned.Substring(firstDot + 1).Replace(".", "");

                                // Limitar a 2 decimales
                                if (cleaned.Length > firstDot + 3)
                                    cleaned = cleaned.Substring(0, firstDot + 3);
                            }

                            if (cleaned != pastedText)
                            {
                                e.CancelCommand();
                                var tb = s as TextBox;
                                int selStart = tb.SelectionStart;
                                string newText = tb.Text.Substring(0, selStart) + cleaned +
                                               tb.Text.Substring(selStart + tb.SelectionLength);
                                tb.Text = newText;
                                tb.SelectionStart = selStart + cleaned.Length;
                            }
                        }
                    });

                    // Manejar teclas especiales
                    valueTextBox.PreviewKeyDown += async (s, e) =>
                    {
                        var tb = s as TextBox;
                        var tag = (dynamic)tb.Tag;

                        if (e.Key == System.Windows.Input.Key.Escape)
                        {
                            // Cancelar: restaurar valor original
                            e.Handled = true;
                            tb.Text = ((decimal)tag.OriginalValue).ToString("C2", _mexicanCulture);
                            tb.Background = Brushes.Transparent;
                            System.Windows.Input.Keyboard.ClearFocus();
                            gridBalance.Focus();
                        }
                        else if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
                        {
                            // Guardar y salir
                            if (e.Key == System.Windows.Input.Key.Enter)
                                e.Handled = true;

                            await SaveHorasExtra(tb);
                            System.Windows.Input.Keyboard.ClearFocus();
                            gridBalance.Focus();
                        }
                        // Bloquear espacio
                        else if (e.Key == System.Windows.Input.Key.Space)
                        {
                            e.Handled = true;
                        }
                    };

                    // Al perder focus: formatear y guardar
                    valueTextBox.LostFocus += async (s, e) =>
                    {
                        var tb = s as TextBox;

                        // Si ya tiene formato de moneda, no procesar
                        if (tb.Text.StartsWith("$"))
                        {
                            tb.Background = Brushes.Transparent;
                            return;
                        }

                        await SaveHorasExtra(tb);
                    };

                    valueBorder.Child = valueTextBox;
                }
                else
                {
                    // Celda normal no editable
                    var valueText = new TextBlock
                    {
                        Text = value != 0 ? value.ToString("C2", _mexicanCulture) : "-",
                        FontWeight = isTotal ? FontWeights.SemiBold : FontWeights.Normal,
                        FontSize = 13,
                        Foreground = (showNegative && value < 0) ?
                            new SolidColorBrush(BalanceColors.Red) :
                            new SolidColorBrush(BalanceColors.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 12, 0)
                    };

                    valueBorder.Child = valueText;
                }

                Grid.SetRow(valueBorder, row);
                Grid.SetColumn(valueBorder, i + 1);
                gridBalance.Children.Add(valueBorder);
            }

            // Celda de Total Anual - destacada con azul
            var totalBorder = new Border
            {
                Background = new SolidColorBrush(BalanceColors.TotalColumnBg),
                BorderBrush = new SolidColorBrush(BalanceColors.TotalColumnBorder),
                BorderThickness = new Thickness(2, 0, 0, 1),
                Height = 40
            };

            var totalText = new TextBlock
            {
                Text = total.ToString("C2", _mexicanCulture),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = (showNegative && total < 0) ?
                    new SolidColorBrush(BalanceColors.Red) :
                    new SolidColorBrush(Color.FromRgb(30, 64, 175)), // #1E40AF - Azul oscuro
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            totalBorder.Child = totalText;
            Grid.SetRow(totalBorder, row);
            Grid.SetColumn(totalBorder, 13);
            gridBalance.Children.Add(totalBorder);
        }

        /// <summary>
        /// Agrega una fila de Ventas con indicador de semáforo (dot)
        /// Rojo: $0 o por debajo del umbral
        /// Amarillo: en rango medio
        /// Verde: meta alcanzada
        /// </summary>
        private void AddSemaforoDataRow(string concepto, decimal[] ventas, decimal[] nomina, decimal[] gastosFijos, int row)
        {
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Celda de concepto
            var conceptBorder = new Border
            {
                Background = new SolidColorBrush(BalanceColors.TotalRowBg),
                BorderBrush = new SolidColorBrush(BalanceColors.Border),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Height = 40
            };

            var conceptText = new TextBlock
            {
                Text = concepto,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(BalanceColors.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };

            conceptBorder.Child = conceptText;
            Grid.SetRow(conceptBorder, row);
            Grid.SetColumn(conceptBorder, 0);
            gridBalance.Children.Add(conceptBorder);

            // Color para mes actual
            var currentMonthBg = Color.FromRgb(239, 246, 255);  // #EFF6FF - Azul muy claro

            // Celdas de valores mensuales con indicador dot y texto del mismo color
            decimal total = 0;
            for (int i = 0; i < 12; i++)
            {
                bool isCurrentMonth = (i + 1) == _currentMonth && _currentYear == DateTime.Now.Year;

                decimal ventaMes = ventas[i];
                decimal nominaMes = nomina[i];
                decimal gastosFijosMes = gastosFijos[i];

                // Calcular umbrales
                decimal baseGastos = nominaMes + gastosFijosMes;
                decimal umbralAmarillo = baseGastos * 1.1m;
                decimal umbralVerde = umbralAmarillo + 100000m;

                // Determinar colores según semáforo (dot y texto oscuro para legibilidad)
                Color dotColor;
                Color textColor;
                string tooltip;

                if (ventaMes == 0)
                {
                    dotColor = BalanceColors.Red;
                    textColor = Color.FromRgb(153, 27, 27);    // #991B1B - Rojo oscuro
                    tooltip = $"Sin ventas\nMeta mínima: {umbralAmarillo:C0}\nMeta óptima: {umbralVerde:C0}";
                }
                else if (ventaMes < umbralAmarillo)
                {
                    dotColor = BalanceColors.Red;
                    textColor = Color.FromRgb(153, 27, 27);    // #991B1B - Rojo oscuro
                    tooltip = $"Por debajo de meta\nVentas: {ventaMes:C0}\nMeta mínima: {umbralAmarillo:C0}\nFaltan: {(umbralAmarillo - ventaMes):C0}";
                }
                else if (ventaMes < umbralVerde)
                {
                    dotColor = BalanceColors.Yellow;
                    textColor = Color.FromRgb(180, 83, 9);     // #B45309 - Ámbar oscuro
                    tooltip = $"En rango medio\nVentas: {ventaMes:C0}\nMeta óptima: {umbralVerde:C0}\nFaltan: {(umbralVerde - ventaMes):C0}";
                }
                else
                {
                    dotColor = BalanceColors.Green;
                    textColor = Color.FromRgb(21, 128, 61);    // #15803D - Verde oscuro
                    tooltip = $"Meta alcanzada\nVentas: {ventaMes:C0}\nSuperó meta por: {(ventaMes - umbralVerde):C0}";
                }

                // Contenedor principal - con resaltado si es mes actual
                var valueBorder = new Border
                {
                    Background = new SolidColorBrush(isCurrentMonth ? currentMonthBg : BalanceColors.White),
                    BorderBrush = new SolidColorBrush(BalanceColors.Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = 40,
                    ToolTip = new ToolTip
                    {
                        Content = tooltip,
                        FontSize = 12
                    }
                };

                total += ventaMes;

                // Grid para dot + valor
                var innerGrid = new Grid();
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Indicador circular (dot)
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(dotColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 0);
                innerGrid.Children.Add(dot);

                // Valor con color que coincide con el semáforo
                var valueText = new TextBlock
                {
                    Text = ventaMes.ToString("C2", _mexicanCulture),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(textColor),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(valueText, 1);
                innerGrid.Children.Add(valueText);

                valueBorder.Child = innerGrid;
                Grid.SetRow(valueBorder, row);
                Grid.SetColumn(valueBorder, i + 1);
                gridBalance.Children.Add(valueBorder);
            }

            // Celda de total
            var totalBorder = new Border
            {
                Background = new SolidColorBrush(BalanceColors.TotalColumnBg),
                BorderBrush = new SolidColorBrush(BalanceColors.TotalColumnBorder),
                BorderThickness = new Thickness(2, 0, 0, 1),
                Height = 40
            };

            var totalText = new TextBlock
            {
                Text = total.ToString("C2", _mexicanCulture),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            totalBorder.Child = totalText;
            Grid.SetRow(totalBorder, row);
            Grid.SetColumn(totalBorder, 13);
            gridBalance.Children.Add(totalBorder);
        }

        /// <summary>
        /// Agrega la fila de Utilidad con indicador de flecha (▲/▼)
        /// </summary>
        private void AddUtilidadDataRow(string concepto, decimal[] valores, int row)
        {
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Celda de concepto
            var conceptBorder = new Border
            {
                Background = new SolidColorBrush(BalanceColors.TotalRowBg),
                BorderBrush = new SolidColorBrush(BalanceColors.Border),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Height = 44
            };

            var conceptText = new TextBlock
            {
                Text = concepto,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(BalanceColors.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };

            conceptBorder.Child = conceptText;
            Grid.SetRow(conceptBorder, row);
            Grid.SetColumn(conceptBorder, 0);
            gridBalance.Children.Add(conceptBorder);

            // Color para mes actual
            var currentMonthBg = Color.FromRgb(239, 246, 255);  // #EFF6FF

            // Celdas de valores mensuales con indicador de flecha
            decimal total = 0;
            for (int i = 0; i < 12; i++)
            {
                bool isCurrentMonth = (i + 1) == _currentMonth && _currentYear == DateTime.Now.Year;
                var value = valores[i];
                total += value;

                bool isPositive = value >= 0;
                Color textColor = isPositive ? Color.FromRgb(21, 128, 61) : Color.FromRgb(153, 27, 27);
                Color arrowColor = isPositive ? BalanceColors.Green : BalanceColors.Red;

                var valueBorder = new Border
                {
                    Background = new SolidColorBrush(isCurrentMonth ? currentMonthBg : BalanceColors.White),
                    BorderBrush = new SolidColorBrush(BalanceColors.Border),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = 44
                };

                // Grid para flecha + valor
                var innerGrid = new Grid();
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Flecha indicadora
                var arrow = new TextBlock
                {
                    Text = isPositive ? "▲" : "▼",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(arrowColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };

                // Valor
                var valueText = new TextBlock
                {
                    Text = value.ToString("C0", _mexicanCulture),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(textColor),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };

                // Panel para alinear flecha y valor a la derecha
                var valuePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                valuePanel.Children.Add(arrow);
                valuePanel.Children.Add(new TextBlock
                {
                    Text = value.ToString("C2", _mexicanCulture),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(textColor),
                    VerticalAlignment = VerticalAlignment.Center
                });

                valueBorder.Child = valuePanel;
                Grid.SetRow(valueBorder, row);
                Grid.SetColumn(valueBorder, i + 1);
                gridBalance.Children.Add(valueBorder);
            }

            // Celda de Total con estilo especial según positivo/negativo
            bool totalPositive = total >= 0;
            Color totalBgColor = totalPositive ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 242, 242); // Verde/Rojo muy claro
            Color totalTextColor = totalPositive ? Color.FromRgb(21, 128, 61) : Color.FromRgb(153, 27, 27);

            var totalBorder = new Border
            {
                Background = new SolidColorBrush(totalBgColor),
                BorderBrush = new SolidColorBrush(BalanceColors.TotalColumnBorder),
                BorderThickness = new Thickness(2, 0, 0, 1),
                Height = 44
            };

            var totalPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            totalPanel.Children.Add(new TextBlock
            {
                Text = totalPositive ? "▲" : "▼",
                FontSize = 12,
                Foreground = new SolidColorBrush(totalTextColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            totalPanel.Children.Add(new TextBlock
            {
                Text = total.ToString("C2", _mexicanCulture),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(totalTextColor),
                VerticalAlignment = VerticalAlignment.Center
            });

            totalBorder.Child = totalPanel;
            Grid.SetRow(totalBorder, row);
            Grid.SetColumn(totalBorder, 13);
            gridBalance.Children.Add(totalBorder);
        }

        /// <summary>
        /// Guarda el valor de horas extra desde un TextBox
        /// Formatea automáticamente: sin decimal agrega .00, con decimal incompleto completa a 2 dígitos
        /// </summary>
        private async Task SaveHorasExtra(TextBox tb)
        {
            var tag = (dynamic)tb.Tag;
            tb.Background = Brushes.Transparent;

            string inputValue = tb.Text.Trim();

            // Si está vacío, es 0
            if (string.IsNullOrWhiteSpace(inputValue))
            {
                inputValue = "0";
            }

            // Limpiar: remover cualquier caracter no numérico excepto punto
            inputValue = System.Text.RegularExpressions.Regex.Replace(inputValue, @"[^\d.]", "");

            // Manejar punto decimal
            if (inputValue.Contains("."))
            {
                string[] parts = inputValue.Split('.');
                string intPart = string.IsNullOrEmpty(parts[0]) ? "0" : parts[0];
                string decPart = parts.Length > 1 ? parts[1] : "";

                // Completar decimales a 2 dígitos
                if (decPart.Length == 0)
                    decPart = "00";
                else if (decPart.Length == 1)
                    decPart = decPart + "0";
                else if (decPart.Length > 2)
                    decPart = decPart.Substring(0, 2);

                inputValue = intPart + "." + decPart;
            }
            else
            {
                // Sin punto decimal: agregar .00
                if (string.IsNullOrEmpty(inputValue))
                    inputValue = "0";
                inputValue = inputValue + ".00";
            }

            // Parsear usando cultura invariante (punto como separador decimal)
            if (decimal.TryParse(inputValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal newAmount))
            {
                // Redondear a 2 decimales
                newAmount = Math.Round(newAmount, 2);

                if (newAmount != (decimal)tag.OriginalValue)
                {
                    if (newAmount > 0 || (decimal)tag.OriginalValue > 0)
                    {
                        bool success = await _supabaseService.UpdateOvertimeHours(
                            tag.Year, tag.Month, newAmount,
                            $"Actualizado desde balance", _currentUser.Id);

                        if (success)
                        {
                            tb.Text = newAmount.ToString("C2", _mexicanCulture);
                            tb.Tag = new { Month = (int)tag.Month, Year = (int)tag.Year, OriginalValue = newAmount };
                            await LoadBalanceData();
                            return;
                        }
                    }
                }

                // Sin cambios o monto cero: solo formatear
                tb.Text = newAmount.ToString("C2", _mexicanCulture);
                tb.Tag = new { Month = (int)tag.Month, Year = (int)tag.Year, OriginalValue = newAmount };
            }
            else
            {
                // Valor inválido: restaurar original
                tb.Text = ((decimal)tag.OriginalValue).ToString("C2", _mexicanCulture);
            }
        }

        // Nuevo método para refrescar solo los datos
        private async Task RefreshBalanceData()
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();
                var response = await supabaseClient
                    .From<BalanceCompletoDb>()
                    .Where(b => b.Año == _currentYear)
                    .Order("mes_numero", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                _balanceData = response?.Models ?? new List<BalanceCompletoDb>();

                // Solo actualizar KPIs sin reconstruir toda la tabla
                UpdateKPIs();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al refrescar datos: {ex.Message}");
            }
        }

        // Método para actualizar el total de una fila específica
        private void UpdateRowTotal(int row)
        {
            decimal total = 0;

            // Buscar todas las celdas de esa fila y sumar
            foreach (var child in gridBalance.Children)
            {
                if (child is Border border && Grid.GetRow(border) == row)
                {
                    int col = Grid.GetColumn(border);
                    // Columnas 1-12 son los meses
                    if (col >= 1 && col <= 12)
                    {
                        if (border.Child is TextBox tb)
                        {
                            string cleanValue = tb.Text.Replace("$", "").Replace(",", "");
                            if (decimal.TryParse(cleanValue, out decimal val))
                            {
                                total += val;
                            }
                        }
                        else if (border.Child is TextBlock txt)
                        {
                            string cleanValue = txt.Text.Replace("$", "").Replace(",", "").Replace("-", "");
                            if (decimal.TryParse(cleanValue, out decimal val))
                            {
                                total += val;
                            }
                        }
                    }
                    // Columna 13 es el total
                    else if (col == 13 && border.Child is TextBlock totalText)
                    {
                        totalText.Text = total.ToString("C2", _mexicanCulture);
                    }
                }
            }
        }

        private void UpdateKPIs()
        {
            decimal totalGastos = _balanceData.Sum(d => d.TotalGastos);
            decimal totalIngresos = _balanceData.Sum(d => d.IngresosPercibidos);
            decimal utilidad = _balanceData.Sum(d => d.UtilidadAproximada);
            decimal totalVentas = _balanceData.Sum(d => d.VentasTotales);

            // Actualizar displays con 2 decimales
            txtTotalIngresos.Text = totalIngresos.ToString("C2", _mexicanCulture);
            txtTotalGastos.Text = totalGastos.ToString("C2", _mexicanCulture);
            txtUtilidad.Text = utilidad.ToString("C2", _mexicanCulture);
            txtTotalVentas.Text = totalVentas.ToString("C2", _mexicanCulture);

            // Destacar KPI de Utilidad según positiva/negativa
            bool isPositive = utilidad >= 0;

            // Colores según estado
            Color bgColor = isPositive ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 242, 242);  // Verde/Rojo muy claro
            Color borderColor = isPositive ? Color.FromRgb(134, 239, 172) : Color.FromRgb(252, 165, 165);  // Verde/Rojo claro
            Color textColor = isPositive ? Color.FromRgb(21, 128, 61) : Color.FromRgb(153, 27, 27);  // Verde/Rojo oscuro

            borderUtilidad.Background = new SolidColorBrush(bgColor);
            borderUtilidad.BorderBrush = new SolidColorBrush(borderColor);
            txtUtilidad.Foreground = new SolidColorBrush(textColor);

            // Indicador con flecha y porcentaje
            if (totalIngresos > 0)
            {
                decimal porcentaje = (utilidad / totalIngresos) * 100;
                txtUtilidadIndicator.Text = $"{(isPositive ? "▲" : "▼")} {Math.Abs(porcentaje):F1}%";
                txtUtilidadIndicator.Foreground = new SolidColorBrush(textColor);
            }
            else
            {
                txtUtilidadIndicator.Text = "";
            }
        }

        private void ShowEmptyState()
        {
            txtTotalIngresos.Text = "$0";
            txtTotalGastos.Text = "$0";
            txtUtilidad.Text = "$0";
            txtTotalVentas.Text = "$0";

            // Limpiar grid y mostrar estado vacío
            gridBalance.Children.Clear();
            EmptyState.Visibility = Visibility.Visible;
        }

        private async void BtnPrevYear_Click(object sender, RoutedEventArgs e)
        {
            _currentYear--;
            txtYear.Text = _currentYear.ToString();
            await LoadBalanceData();
        }

        private async void BtnNextYear_Click(object sender, RoutedEventArgs e)
        {
            _currentYear++;
            txtYear.Text = _currentYear.ToString();
            await LoadBalanceData();
        }

        /* Exportación deshabilitada en esta versión
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Función de exportación disponible en la versión completa",
                "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        */

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Métodos para resaltado de filas y columnas
        private static readonly Color HighlightColor = Color.FromArgb(25, 24, 24, 27); // #18181B con transparencia

        private void HighlightColumn(int columnIndex)
        {
            ClearHighlights();
            _selectedColumnIndex = columnIndex;

            foreach (var child in gridBalance.Children)
            {
                if (child is Border border)
                {
                    var col = Grid.GetColumn(border);
                    if (col == columnIndex)
                    {
                        _highlightedCells.Add(border);
                        if (border.Background != null && border.Background is SolidColorBrush)
                        {
                            border.Background = new SolidColorBrush(HighlightColor);
                            border.Tag = border.Tag + "_highlighted";
                        }
                    }
                }
            }
        }

        private void HighlightRow(int rowIndex)
        {
            ClearHighlights();
            _selectedRowIndex = rowIndex;

            foreach (var child in gridBalance.Children)
            {
                if (child is Border border)
                {
                    var row = Grid.GetRow(border);
                    if (row == rowIndex)
                    {
                        _highlightedCells.Add(border);
                        if (border.Background != null && border.Background is SolidColorBrush)
                        {
                            border.Background = new SolidColorBrush(HighlightColor);
                            border.Tag = border.Tag + "_highlighted";
                        }
                    }
                }
            }
        }

        private void HighlightCell(int rowIndex, int columnIndex)
        {
            ClearHighlights();
            _selectedRowIndex = rowIndex;
            _selectedColumnIndex = columnIndex;

            var highlightLight = Color.FromArgb(20, 24, 24, 27);
            var highlightStrong = Color.FromArgb(40, 24, 24, 27);

            foreach (var child in gridBalance.Children)
            {
                if (child is Border border)
                {
                    var row = Grid.GetRow(border);
                    var col = Grid.GetColumn(border);

                    if (row == rowIndex)
                    {
                        _highlightedCells.Add(border);
                        border.Background = new SolidColorBrush(highlightLight);
                    }

                    if (col == columnIndex)
                    {
                        if (!_highlightedCells.Contains(border))
                            _highlightedCells.Add(border);

                        border.Background = new SolidColorBrush(
                            row == rowIndex ? highlightStrong : highlightLight);
                    }
                }
            }
        }

        private void ClearHighlights()
        {
            foreach (var border in _highlightedCells)
            {
                // Restaurar color original basado en el tipo de celda
                var tag = border.Tag?.ToString() ?? "";

                if (tag.Contains("_highlighted"))
                {
                    tag = tag.Replace("_highlighted", "");
                    border.Tag = tag;
                }

                // Determinar el color original basado en la posición y tipo
                var row = Grid.GetRow(border);
                var col = Grid.GetColumn(border);

                if (col == 13 || col == 0 || row == 0) // Headers y totales
                {
                    border.Background = new SolidColorBrush(BalanceColors.HeaderBg);
                }
                else
                {
                    border.Background = new SolidColorBrush(BalanceColors.White);
                }
            }

            _highlightedCells.Clear();
            _selectedRowIndex = -1;
            _selectedColumnIndex = -1;
        }
    }

    // Modelo para la vista de la base de datos
    [Table("v_balance_completo")]
    public class BalanceCompletoDb : BaseModel
    {
        [Column("fecha")]
        public DateTime? Fecha { get; set; }

        [Column("año")]
        public int Año { get; set; }

        [Column("mes_numero")]
        public int MesNumero { get; set; }

        [Column("mes_nombre")]
        public string MesNombre { get; set; }

        [Column("mes_corto")]
        public string MesCorto { get; set; }

        // Gastos
        [Column("nomina")]
        public decimal Nomina { get; set; }

        [Column("horas_extra")]
        public decimal HorasExtra { get; set; }

        [Column("gastos_fijos")]
        public decimal GastosFijos { get; set; }

        [Column("gastos_variables")]
        public decimal GastosVariables { get; set; }

        // Gastos de órdenes
        [Column("gasto_operativo")]
        public decimal GastoOperativo { get; set; }

        [Column("gasto_indirecto")]
        public decimal GastoIndirecto { get; set; }

        [Column("total_gastos")]
        public decimal TotalGastos { get; set; }

        // Ingresos
        [Column("ingresos_esperados")]
        public decimal IngresosEsperados { get; set; }

        [Column("ingresos_percibidos")]
        public decimal IngresosPercibidos { get; set; }

        [Column("diferencia")]
        public decimal Diferencia { get; set; }

        [Column("ventas_totales")]
        public decimal VentasTotales { get; set; }

        [Column("utilidad_aproximada")]
        public decimal UtilidadAproximada { get; set; }
    }
}