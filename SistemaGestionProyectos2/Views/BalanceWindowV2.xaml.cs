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
using SistemaGestionProyectos2.Services;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Ventana de Balance Profesional con diseño mejorado
    /// </summary>
    public partial class BalanceWindowV2 : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private int _currentYear;
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");
        private List<BalanceGastosDb> _balanceData;

        // Colores para las secciones
        private readonly Dictionary<string, Color[]> _sectionColors = new Dictionary<string, Color[]>
        {
            { "GASTOS", new[] { Color.FromRgb(254, 226, 226), Color.FromRgb(252, 165, 165) } },
            { "INGRESOS", new[] { Color.FromRgb(209, 250, 229), Color.FromRgb(110, 231, 183) } },
            { "UTILIDAD", new[] { Color.FromRgb(219, 234, 254), Color.FromRgb(96, 165, 250) } }
        };

        public BalanceWindowV2(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _currentYear = DateTime.Now.Year;

            // Inicializar año
            txtYear.Text = _currentYear.ToString();

            // Actualizar timestamp
            txtLastUpdate.Text = $"Última actualización: {DateTime.Now:dd/MM/yyyy HH:mm}";

            // Cargar datos
            _ = LoadBalanceData();
        }

        private async Task LoadBalanceData()
        {
            try
            {
                // Obtener datos de la vista
                var supabaseClient = _supabaseService.GetClient();
                var response = await supabaseClient
                    .From<BalanceGastosDb>()
                    .Where(b => b.Año == _currentYear)
                    .Order("mes_numero", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                _balanceData = response?.Models ?? new List<BalanceGastosDb>();

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

            // Crear columnas (Concepto + 12 meses + Total)
            gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            for (int i = 1; i <= 12; i++)
            {
                gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
            }
            gridBalance.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            int currentRow = 0;

            // Headers de meses
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddMonthHeaders(currentRow);
            currentRow++;

            // SECCIÓN GASTOS
            AddSectionHeader("💸 GASTOS", Color.FromRgb(254, 226, 226), currentRow);
            currentRow++;

            // Preparar datos
            var nomina = new decimal[12];
            var horasExtra = new decimal[12];
            var gastosFijos = new decimal[12];
            var gastosVariables = new decimal[12];
            var totalGastos = new decimal[12];

            foreach (var dato in _balanceData)
            {
                int mesIndex = dato.MesNumero - 1;
                if (mesIndex >= 0 && mesIndex < 12)
                {
                    nomina[mesIndex] = dato.Nomina;
                    horasExtra[mesIndex] = dato.HorasExtra * 100; // Asumiendo un valor por hora
                    gastosFijos[mesIndex] = dato.GastosFijos;
                    gastosVariables[mesIndex] = dato.GastosVariables;
                    totalGastos[mesIndex] = dato.TotalGastos;
                }
            }

            // Filas de gastos
            AddDataRow("Nómina", nomina, currentRow, false);
            currentRow++;
            AddDataRow("Horas Extra", horasExtra, currentRow, false);
            currentRow++;
            AddDataRow("Gastos Fijos", gastosFijos, currentRow, false);
            currentRow++;
            AddDataRow("Gastos Variables", gastosVariables, currentRow, false);
            currentRow++;
            AddDataRow("Subtotal Gastos", totalGastos, currentRow, true, Color.FromRgb(254, 226, 226));
            currentRow++;

            // Espacio
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            currentRow++;

            // SECCIÓN INGRESOS (placeholder por ahora)
            AddSectionHeader("💰 INGRESOS", Color.FromRgb(209, 250, 229), currentRow);
            currentRow++;

            var ingresosEsperados = new decimal[12];
            var ingresosReales = new decimal[12];
            // Por ahora con datos demo, en el futuro se cargarán de la BD
            for (int i = 0; i < 12; i++)
            {
                ingresosEsperados[i] = 0; // Se llenará cuando se implemente
                ingresosReales[i] = 0;
            }

            AddDataRow("Ingresos Esperados", ingresosEsperados, currentRow, false);
            currentRow++;
            AddDataRow("Ingresos Percibidos", ingresosReales, currentRow, false);
            currentRow++;
            AddDataRow("Subtotal Ingresos", ingresosReales, currentRow, true, Color.FromRgb(209, 250, 229));
            currentRow++;

            // Espacio
            gridBalance.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            currentRow++;

            // SECCIÓN UTILIDAD
            AddSectionHeader("📊 UTILIDAD", Color.FromRgb(219, 234, 254), currentRow);
            currentRow++;

            var utilidad = new decimal[12];
            for (int i = 0; i < 12; i++)
            {
                utilidad[i] = ingresosReales[i] - totalGastos[i];
            }

            AddDataRow("Utilidad Bruta", utilidad, currentRow, false);
            currentRow++;
            AddDataRow("UTILIDAD NETA", utilidad, currentRow, true, Color.FromRgb(219, 234, 254));
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

            // Headers de meses
            string[] meses = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                              "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

            for (int i = 0; i < 12; i++)
            {
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(237, 242, 247)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 224)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Height = 45
                };

                var headerText = new TextBlock
                {
                    Text = meses[i],
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                headerBorder.Child = headerText;
                Grid.SetRow(headerBorder, row);
                Grid.SetColumn(headerBorder, i + 1);
                gridBalance.Children.Add(headerBorder);
            }

            // Header Total
            var totalHeaderBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                BorderThickness = new Thickness(0, 0, 2, 2),
                Height = 45
            };

            var totalHeaderText = new TextBlock
            {
                Text = "TOTAL",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
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
                FontSize = 14,
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

        private void AddDataRow(string concepto, decimal[] valores, int row, bool isTotal, Color? backgroundColor = null)
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
                Height = isTotal ? 42 : 40
            };

            var conceptText = new TextBlock
            {
                Text = concepto,
                FontWeight = isTotal ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize = isTotal ? 13 : 12,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 0, 0)
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
                    Height = isTotal ? 42 : 40
                };

                var value = valores[i];
                total += value;

                var valueText = new TextBlock
                {
                    Text = value != 0 ? value.ToString("C0", _mexicanCulture) : "-",
                    FontWeight = isTotal ? FontWeights.SemiBold : FontWeights.Normal,
                    FontSize = isTotal ? 12 : 11,
                    Foreground = value < 0 ?
                        new SolidColorBrush(Color.FromRgb(220, 38, 38)) :
                        new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                valueBorder.Child = valueText;
                Grid.SetRow(valueBorder, row);
                Grid.SetColumn(valueBorder, i + 1);
                gridBalance.Children.Add(valueBorder);
            }

            // Celda de total
            var totalBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                BorderThickness = new Thickness(0, 0, 2, 2),
                Height = isTotal ? 42 : 40
            };

            var totalText = new TextBlock
            {
                Text = total.ToString("C0", _mexicanCulture),
                FontWeight = FontWeights.Bold,
                FontSize = isTotal ? 13 : 12,
                Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            totalBorder.Child = totalText;
            Grid.SetRow(totalBorder, row);
            Grid.SetColumn(totalBorder, 13);
            gridBalance.Children.Add(totalBorder);
        }

        private void UpdateKPIs()
        {
            decimal totalGastos = 0;
            decimal totalIngresos = 0;

            foreach (var dato in _balanceData)
            {
                totalGastos += dato.TotalGastos;
            }

            decimal utilidad = totalIngresos - totalGastos;
            decimal promedio = totalGastos / 12;
            decimal margen = totalIngresos > 0 ? (utilidad / totalIngresos) * 100 : 0;

            // Actualizar displays
            txtTotalGastos.Text = totalGastos.ToString("C", _mexicanCulture);
            txtTotalIngresos.Text = totalIngresos.ToString("C", _mexicanCulture);
            txtUtilidad.Text = utilidad.ToString("C", _mexicanCulture);
            txtPromedio.Text = promedio.ToString("C", _mexicanCulture);
            txtMargen.Text = $"{margen:F1}%";

            // Cambiar color de utilidad según sea positiva o negativa
            txtUtilidad.Foreground = utilidad >= 0 ?
                new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void ShowEmptyState()
        {
            txtTotalGastos.Text = "$0.00";
            txtTotalIngresos.Text = "$0.00";
            txtUtilidad.Text = "$0.00";
            txtPromedio.Text = "$0.00";
            txtMargen.Text = "0%";

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
            if (_currentYear < DateTime.Now.Year)
            {
                _currentYear++;
                txtYear.Text = _currentYear.ToString();
                await LoadBalanceData();
            }
            else
            {
                MessageBox.Show("No se puede mostrar balance de años futuros",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Función de exportación disponible en la versión completa",
                "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Modelo para la vista de la base de datos
    [Table("v_balance_gastos")]
    public class BalanceGastosDb : BaseModel
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

        [Column("nomina")]
        public decimal Nomina { get; set; }

        [Column("horas_extra")]
        public int HorasExtra { get; set; }

        [Column("gastos_fijos")]
        public decimal GastosFijos { get; set; }

        [Column("gastos_variables")]
        public decimal GastosVariables { get; set; }

        [Column("total_gastos")]
        public decimal TotalGastos { get; set; }
    }
}