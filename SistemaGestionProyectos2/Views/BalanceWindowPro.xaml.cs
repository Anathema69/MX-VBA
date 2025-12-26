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

        // Colores para las secciones
        private readonly Dictionary<string, Color[]> _sectionColors = new Dictionary<string, Color[]>
        {
            { "GASTOS", new[] { Color.FromRgb(254, 226, 226), Color.FromRgb(252, 165, 165) } },
            { "INGRESOS", new[] { Color.FromRgb(209, 250, 229), Color.FromRgb(110, 231, 183) } },
            { "UTILIDAD", new[] { Color.FromRgb(219, 234, 254), Color.FromRgb(96, 165, 250) } },
            { "VENTAS", new[] { Color.FromRgb(255, 237, 213), Color.FromRgb(251, 191, 36) } }
        };

        private int _selectedRowIndex = -1;
        private int _selectedColumnIndex = -1;
        private List<Border> _highlightedCells = new List<Border>();

        public BalanceWindowPro(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _currentYear = DateTime.Now.Year;

            // Maximizar ventana dejando visible la barra de tareas
            MaximizeWithTaskbar();

            // Inicializar año
            txtYear.Text = _currentYear.ToString();

            // Actualizar timestamp
            txtLastUpdate.Text = $"Última actualización: {DateTime.Now:dd/MM/yyyy HH:mm}";

            // Cargar datos
            _ = LoadBalanceData();
        }

        private void MaximizeWithTaskbar()
        {
            // Obtener el área de trabajo (sin incluir la barra de tareas)
            var workingArea = SystemParameters.WorkArea;
            this.Left = workingArea.Left;
            this.Top = workingArea.Top;
            this.Width = workingArea.Width;
            this.Height = workingArea.Height;
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
            // Limpiar grid
            gridBalance.Children.Clear();
            gridBalance.RowDefinitions.Clear();
            gridBalance.ColumnDefinitions.Clear();

            // Crear columnas (Concepto + 12 meses + Total) - Anchos aumentados para mejor legibilidad
            gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            for (int i = 1; i <= 12; i++)
            {
                gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            }
            gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

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
            AddSectionHeader("GASTOS", _sectionColors["GASTOS"][0], currentRow);
            currentRow++;

            AddDataRow("Nómina", nomina, currentRow, false);
            currentRow++;

            AddDataRow("Horas Extra", horasExtra, currentRow, false);
            currentRow++;

            AddDataRow("Gastos Fijos", gastosFijos, currentRow, false);
            currentRow++;

            AddDataRow("Gastos Variables", gastosVariables, currentRow, false);
            currentRow++;

            // Total Gastos (renglón de suma con estilo destacado)
            AddDataRow("Total Gastos", totalGastos, currentRow, true, _sectionColors["GASTOS"][1], false);
            currentRow++;

            // Espacio
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            currentRow++;

            // SECCIÓN INGRESOS
            AddSectionHeader("INGRESOS", _sectionColors["INGRESOS"][0], currentRow);
            currentRow++;

            AddDataRow("Ingresos Esperados", ingresosEsperados, currentRow, false);
            currentRow++;

            AddDataRow("Ingresos Percibidos", ingresosPercibidos, currentRow, false);
            currentRow++;

            AddDataRow("Diferencia", diferencia, currentRow, false, null, false, true); // true para mostrar negativos
            currentRow++;

            // Espacio
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            currentRow++;

            // SECCIÓN UTILIDAD
            AddSectionHeader("UTILIDAD APROXIMADA", _sectionColors["UTILIDAD"][0], currentRow);
            currentRow++;

            AddDataRow("Utilidad Aproximada", utilidadAproximada, currentRow, true,
                _sectionColors["UTILIDAD"][0], false, true);
            currentRow++;

            // Espacio
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            currentRow++;

            // SECCIÓN VENTAS
            AddSectionHeader("VENTAS TOTALES", _sectionColors["VENTAS"][0], currentRow);
            currentRow++;

            AddDataRow("Ventas Totales", ventasTotales, currentRow, true,
                _sectionColors["VENTAS"][0]);
        }

        private void AddMonthHeaders(int row)
        {
            // Celda vacía para concepto
            var emptyBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(237, 242, 247)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 224)),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
            Grid.SetRow(emptyBorder, row);
            Grid.SetColumn(emptyBorder, 0);
            gridBalance.Children.Add(emptyBorder);

            // Headers de meses - versiones cortas para ahorrar espacio
            string[] meses = { "Ene", "Feb", "Mar", "Abr", "May", "Jun",
                              "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

            for (int i = 0; i < 12; i++)
            {
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(237, 242, 247)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 224)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = 48,
                    Tag = $"header_col_{i + 1}",
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var headerText = new TextBlock
                {
                    Text = meses[i],
                    FontWeight = FontWeights.Bold,
                    FontSize = 15,
                    Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Evento para resaltar columna
                headerBorder.MouseLeftButtonDown += (s, e) => HighlightColumn(i + 1);
                headerBorder.MouseEnter += (s, e) => headerBorder.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                headerBorder.MouseLeave += (s, e) =>
                {
                    if (_selectedColumnIndex != i + 1)
                        headerBorder.Background = new SolidColorBrush(Color.FromRgb(237, 242, 247));
                };

                headerBorder.Child = headerText;
                Grid.SetRow(headerBorder, row);
                Grid.SetColumn(headerBorder, i + 1);
                gridBalance.Children.Add(headerBorder);
            }

            // Header Total
            var totalHeaderBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(252, 211, 77)), // #FCD34D - ámbar suave
                BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                BorderThickness = new Thickness(0, 0, 2, 2),
                Height = 40
            };

            var totalHeaderText = new TextBlock
            {
                Text = "TOTAL",
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            totalHeaderBorder.Child = totalHeaderText;
            Grid.SetRow(totalHeaderBorder, row);
            Grid.SetColumn(totalHeaderBorder, 13);
            gridBalance.Children.Add(totalHeaderBorder);
        }

        private void AddSectionHeader(string title, Color backgroundColor, int row)
        {
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                Height = 40,
                CornerRadius = new CornerRadius(5)
            };

            var headerText = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 0, 0)
            };

            headerBorder.Child = headerText;
            Grid.SetRow(headerBorder, row);
            Grid.SetColumn(headerBorder, 0);
            Grid.SetColumnSpan(headerBorder, 14);
            gridBalance.Children.Add(headerBorder);
        }

        private void AddDataRow(string concepto, decimal[] valores, int row, bool isTotal,
    Color? backgroundColor = null, bool isEditable = false, bool showNegative = false)
        {
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Celda de concepto
            var conceptBorder = new Border
            {
                Background = isTotal ?
                    new SolidColorBrush(backgroundColor ?? Color.FromRgb(243, 244, 246)) :
                    Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Height = isTotal ? 50 : 46
            };

            var conceptText = new TextBlock
            {
                Text = concepto,
                FontWeight = isTotal ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize = isTotal ? 16 : 15,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            conceptBorder.Child = conceptText;
            Grid.SetRow(conceptBorder, row);
            Grid.SetColumn(conceptBorder, 0);
            gridBalance.Children.Add(conceptBorder);

            // Celdas de valores mensuales
            decimal total = 0;
            for (int i = 0; i < 12; i++)
            {
                var valueBorder = new Border
                {
                    Background = isTotal ?
                        new SolidColorBrush(backgroundColor ?? Color.FromRgb(243, 244, 246)) :
                        Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = isTotal ? 50 : 46
                };

                var value = valores[i];
                total += value;

                // Para Horas Extra, hacer las celdas editables
                // En el método AddDataRow, mejorar la lógica de horas extras:
                if (concepto == "Horas Extra" && !isTotal)
                {
                    var monthIndex = i;
                    var valueTextBox = new TextBox
                    {
                        Text = value > 0 ? value.ToString("C2", _mexicanCulture) : "$0.00",
                        FontSize = 14,
                        FontWeight = FontWeights.Medium,
                        TextAlignment = TextAlignment.Right,
                        BorderThickness = new Thickness(0),
                        Background = Brushes.Transparent,
                        Padding = new Thickness(0, 0, 10, 0),
                        Tag = new { Month = i + 1, Year = _currentYear, OriginalValue = value }
                    };

                    valueTextBox.GotFocus += (s, e) =>
                    {
                        var tb = s as TextBox;
                        tb.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195));
                        // Limpiar el formato para edición
                        if (tb.Text == "$0.00")
                        {
                            tb.Text = "";
                        }
                        else
                        {
                            tb.SelectAll();
                        }
                    };

                    
                    valueTextBox.LostFocus += async (s, e) =>
                    {
                        var tb = s as TextBox;
                        tb.Background = Brushes.Transparent;
                        var tag = (dynamic)tb.Tag;

                        string cleanValue = tb.Text.Replace("$", "").Replace(",", "").Trim();

                        if (string.IsNullOrWhiteSpace(cleanValue))
                        {
                            cleanValue = "0";
                        }

                        if (decimal.TryParse(cleanValue, out decimal newAmount))
                        {
                            if (newAmount != tag.OriginalValue)
                            {
                                if (newAmount > 0 || tag.OriginalValue > 0)
                                {
                                    bool success = await _supabaseService.UpdateOvertimeHours(
                                        tag.Year, tag.Month, newAmount,
                                        $"Actualizado desde balance", _currentUser.Id);

                                    if (success)
                                    {
                                        // Mostrar mensaje temporal
                                        tb.Text = newAmount.ToString("C0", _mexicanCulture);

                                        // Recargar todo el balance para reflejar cambios
                                        await LoadBalanceData();
                                    }
                                }
                                else
                                {
                                    tb.Text = "$0.00";
                                }
                            }
                            else
                            {
                                tb.Text = newAmount > 0 ? newAmount.ToString("C0", _mexicanCulture) : "$0.00";
                            }
                        }
                        else
                        {
                            tb.Text = tag.OriginalValue > 0 ?
                                ((decimal)tag.OriginalValue).ToString("C0", _mexicanCulture) : "$0.00";
                        }
                    };

                    valueBorder.Child = valueTextBox;
                }
                else
                {
                    // Celda normal no editable
                    var valueText = new TextBlock
                    {
                        Text = value != 0 ? value.ToString("C2", _mexicanCulture) : "$0.00",
                        FontWeight = isTotal ? FontWeights.Bold : FontWeights.Medium,
                        FontSize = isTotal ? 16 : 14,
                        Foreground = (showNegative && value < 0) ?
                            new SolidColorBrush(Color.FromRgb(220, 38, 38)) :
                            new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };

                    valueBorder.Child = valueText;
                }

                Grid.SetRow(valueBorder, row);
                Grid.SetColumn(valueBorder, i + 1);
                gridBalance.Children.Add(valueBorder);
            }

            // Celda de total
            var totalBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(252, 211, 77)), // #FCD34D - ámbar suave
                BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                BorderThickness = new Thickness(0, 0, 2, 2),
                Height = isTotal ? 50 : 46
            };

            var totalText = new TextBlock
            {
                Text = total.ToString("C2", _mexicanCulture),
                FontWeight = FontWeights.Bold,
                FontSize = isTotal ? 17 : 15,
                Foreground = (showNegative && total < 0) ?
                    new SolidColorBrush(Color.FromRgb(220, 38, 38)) :
                    new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            totalBorder.Child = totalText;
            Grid.SetRow(totalBorder, row);
            Grid.SetColumn(totalBorder, 13);
            gridBalance.Children.Add(totalBorder);
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
                        totalText.Text = total.ToString("C0", _mexicanCulture);
                    }
                }
            }
        }

        private void UpdateKPIs()
        {
            decimal totalGastos = _balanceData.Sum(d => d.TotalGastos);
            decimal totalIngresos = _balanceData.Sum(d => d.IngresosPercibidos);
            decimal utilidad = _balanceData.Sum(d => d.UtilidadAproximada);
            decimal promedio = totalGastos / 12;
            decimal margen = totalIngresos > 0 ? (utilidad / totalIngresos) * 100 : 0;

            // Actualizar displays
            txtTotalGastos.Text = totalGastos.ToString("C2", _mexicanCulture);
            txtTotalIngresos.Text = totalIngresos.ToString("C2", _mexicanCulture);
            txtUtilidad.Text = utilidad.ToString("C2", _mexicanCulture);
            

            // Cambiar color de utilidad
            txtUtilidad.Foreground = utilidad >= 0 ?
                new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void ShowEmptyState()
        {
            txtTotalGastos.Text = "$0.00";
            txtTotalIngresos.Text = "$0.00";
            txtUtilidad.Text = "$0.00";
            

            // Limpiar grid
            gridBalance.Children.Clear();
            var emptyMessage = new TextBlock
            {
                Text = $"No hay datos disponibles para el año {_currentYear}",
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 50)
            };
            gridBalance.Children.Add(emptyMessage);
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
                        // Guardar color original
                        _highlightedCells.Add(border);

                        // Aplicar resaltado suave
                        if (border.Background != null && border.Background is SolidColorBrush)
                        {
                            var originalBrush = border.Background as SolidColorBrush;
                            border.Background = new SolidColorBrush(Color.FromArgb(40, 102, 126, 234)); // Azul suave con transparencia
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

                        // Aplicar resaltado suave
                        if (border.Background != null && border.Background is SolidColorBrush)
                        {
                            border.Background = new SolidColorBrush(Color.FromArgb(40, 102, 126, 234)); // Azul suave con transparencia
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

            foreach (var child in gridBalance.Children)
            {
                if (child is Border border)
                {
                    var row = Grid.GetRow(border);
                    var col = Grid.GetColumn(border);

                    // Resaltar fila
                    if (row == rowIndex)
                    {
                        _highlightedCells.Add(border);
                        border.Background = new SolidColorBrush(Color.FromArgb(30, 102, 126, 234));
                    }

                    // Resaltar columna
                    if (col == columnIndex)
                    {
                        if (!_highlightedCells.Contains(border))
                            _highlightedCells.Add(border);

                        if (row == rowIndex)
                        {
                            // Intersección - resaltado más fuerte
                            border.Background = new SolidColorBrush(Color.FromArgb(60, 102, 126, 234));
                        }
                        else
                        {
                            border.Background = new SolidColorBrush(Color.FromArgb(30, 102, 126, 234));
                        }
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

                if (col == 13) // Columna de totales
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                }
                else if (col == 0 || row == 0) // Headers
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(237, 242, 247));
                }
                else
                {
                    border.Background = Brushes.White;
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