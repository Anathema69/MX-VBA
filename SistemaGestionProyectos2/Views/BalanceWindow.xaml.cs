using SistemaGestionProyectos2.Models;
using SistemaGestionProyectos2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Postgrest.Attributes;
using Postgrest.Models;

namespace SistemaGestionProyectos2.Views
{
    /// <summary>
    /// Lógica de interacción para BalanceWindow.xaml
    /// </summary>
    public partial class BalanceWindow : Window
    {
        private readonly SupabaseService _supabaseService;
        private readonly UserSession _currentUser;
        private int _currentYear;
        private readonly CultureInfo _mexicanCulture = new CultureInfo("es-MX");
        private List<BalanceGastosDb> _balanceData;

        public BalanceWindow(UserSession currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _supabaseService = SupabaseService.Instance;
            _currentYear = DateTime.Now.Year;

            // Inicializar con el año actual
            txtYear.Text = _currentYear.ToString();

            // Cargar datos iniciales
            _ = LoadBalanceDataAsync();
        }

        private async Task LoadBalanceDataAsync()
        {
            try
            {
                // Mostrar estado de carga
                ShowLoadingState();

                // Obtener datos de la vista v_balance_gastos
                var balanceData = await GetBalanceDataFromView(_currentYear);

                if (balanceData != null && balanceData.Any())
                {
                    _balanceData = balanceData;
                    ProcessAndDisplayBalance();
                }
                else
                {
                    ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el balance: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowEmptyState();
            }
        }

        private async Task<List<BalanceGastosDb>> GetBalanceDataFromView(int year)
        {
            try
            {
                var supabaseClient = _supabaseService.GetClient();

                // Consultar la vista v_balance_gastos filtrando por año
                var response = await supabaseClient
                    .From<BalanceGastosDb>()
                    .Where(b => b.Año == year)
                    .Order("mes_numero", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<BalanceGastosDb>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo balance: {ex.Message}");
                return new List<BalanceGastosDb>();
            }
        }

        private void ProcessAndDisplayBalance()
        {
            // Limpiar el contenido anterior
            gastosGrid.Children.Clear();
            gastosGrid.RowDefinitions.Clear();
            gastosGrid.ColumnDefinitions.Clear();

            // Crear estructura de columnas
            for (int i = 0; i < 14; i++) // 14 columnas (concepto + 12 meses + total)
            {
                gastosGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = i == 0 ? new GridLength(180) :
                           i == 13 ? new GridLength(110) : new GridLength(95)
                });
            }

            // Diccionario para almacenar datos por concepto
            var conceptosData = new Dictionary<string, decimal[]>();
            conceptosData["Nómina"] = new decimal[12];
            conceptosData["Gastos Fijos"] = new decimal[12];
            conceptosData["Gastos Variables"] = new decimal[12];
            conceptosData["Total"] = new decimal[12];

            decimal totalAnualGastos = 0;

            // Procesar datos por mes
            foreach (var monthData in _balanceData)
            {
                int mesIndex = monthData.MesNumero - 1; // Índice 0-based

                if (mesIndex >= 0 && mesIndex < 12)
                {
                    conceptosData["Nómina"][mesIndex] = monthData.Nomina;
                    conceptosData["Gastos Fijos"][mesIndex] = monthData.GastosFijos;
                    conceptosData["Gastos Variables"][mesIndex] = monthData.GastosVariables;
                    conceptosData["Total"][mesIndex] = monthData.TotalGastos;

                    totalAnualGastos += monthData.TotalGastos;
                }
            }

            // Crear filas para cada concepto
            int rowIndex = 0;
            foreach (var concepto in conceptosData)
            {
                gastosGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Celda del concepto
                var conceptBorder = new Border
                {
                    BorderBrush = FindResource("BorderGray") as SolidColorBrush,
                    BorderThickness = new Thickness(1, 0, 1, 1),
                    Background = rowIndex == conceptosData.Count - 1 ?
                        FindResource("PrimaryLight") as SolidColorBrush :
                        Brushes.White,
                    Padding = new Thickness(12, 8, 12, 8)
                };

                var conceptText = new TextBlock
                {
                    Text = concepto.Key,
                    FontWeight = rowIndex == conceptosData.Count - 1 ? FontWeights.Bold : FontWeights.SemiBold,
                    FontSize = rowIndex == conceptosData.Count - 1 ? 15 : 14,
                    Foreground = Brushes.Black  // Negro sólido
                };

                conceptBorder.Child = conceptText;
                Grid.SetRow(conceptBorder, rowIndex);
                Grid.SetColumn(conceptBorder, 0);
                gastosGrid.Children.Add(conceptBorder);

                // Celdas de valores mensuales
                decimal totalFila = 0;
                for (int mes = 0; mes < 12; mes++)
                {
                    var valueBorder = new Border
                    {
                        BorderBrush = FindResource("BorderGray") as SolidColorBrush,
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = rowIndex == conceptosData.Count - 1 ?
                            FindResource("PrimaryLight") as SolidColorBrush :
                            Brushes.White,
                        Padding = new Thickness(8, 8, 8, 8)
                    };

                    var value = concepto.Value[mes];
                    totalFila += value;

                    var valueText = new TextBlock
                    {
                        Text = value > 0 ? value.ToString("C0", _mexicanCulture) : "-",
                        HorizontalAlignment = HorizontalAlignment.Right,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = rowIndex == conceptosData.Count - 1 ? 13 : 12,
                        FontWeight = rowIndex == conceptosData.Count - 1 ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = value > 0 ?
                            FindResource("DarkText") as SolidColorBrush :
                            FindResource("TextGray") as SolidColorBrush
                    };

                    valueBorder.Child = valueText;
                    Grid.SetRow(valueBorder, rowIndex);
                    Grid.SetColumn(valueBorder, mes + 1);
                    gastosGrid.Children.Add(valueBorder);
                }

                // Celda del total de la fila
                var totalBorder = new Border
                {
                    BorderBrush = FindResource("BorderGray") as SolidColorBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = FindResource("WarningLight") as SolidColorBrush,
                    Padding = new Thickness(12, 8, 12, 8)
                };

                var totalText = new TextBlock
                {
                    Text = totalFila.ToString("C0", _mexicanCulture),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black  // Negro sólido
                };

                totalBorder.Child = totalText;
                Grid.SetRow(totalBorder, rowIndex);
                Grid.SetColumn(totalBorder, 13);
                gastosGrid.Children.Add(totalBorder);

                rowIndex++;
            }

            // Actualizar tarjetas de resumen
            UpdateSummaryCards(totalAnualGastos);
        }

        private void UpdateSummaryCards(decimal totalGastos)
        {
            // Total de Gastos
            txtTotalGastos.Text = totalGastos.ToString("C", _mexicanCulture);

            // Por ahora, Ingresos y Utilidad se muestran como 0
            // Esto puede ser actualizado cuando se implemente la funcionalidad de ingresos
            txtTotalIngresos.Text = "$0.00";

            decimal utilidad = 0 - totalGastos; // Ingresos - Gastos
            txtUtilidad.Text = utilidad.ToString("C", _mexicanCulture);
            txtUtilidad.Foreground = utilidad >= 0 ?
                FindResource("SuccessColor") as SolidColorBrush :
                FindResource("DangerColor") as SolidColorBrush;

            // Promedio mensual
            decimal promedioMensual = totalGastos / 12;
            txtPromedioMensual.Text = promedioMensual.ToString("C", _mexicanCulture);
        }

        private void ShowLoadingState()
        {
            txtTotalGastos.Text = "Cargando...";
            txtTotalIngresos.Text = "Cargando...";
            txtUtilidad.Text = "Cargando...";
            txtPromedioMensual.Text = "Cargando...";
        }

        private void ShowEmptyState()
        {
            txtTotalGastos.Text = "$0.00";
            txtTotalIngresos.Text = "$0.00";
            txtUtilidad.Text = "$0.00";
            txtPromedioMensual.Text = "$0.00";

            // Limpiar la tabla
            gastosGrid.Children.Clear();

            // Mostrar mensaje de no datos
            var emptyMessage = new TextBlock
            {
                Text = $"No hay datos de balance para el año {_currentYear}",
                FontSize = 14,
                Foreground = FindResource("TextGray") as SolidColorBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 50)
            };

            Grid.SetColumn(emptyMessage, 0);
            Grid.SetColumnSpan(emptyMessage, 14);
            gastosGrid.Children.Add(emptyMessage);
        }

        private async void BtnPreviousYear_Click(object sender, RoutedEventArgs e)
        {
            _currentYear--;
            txtYear.Text = _currentYear.ToString();
            await LoadBalanceDataAsync();
        }

        private async void BtnNextYear_Click(object sender, RoutedEventArgs e)
        {
            // No permitir años futuros más allá del año actual
            if (_currentYear < DateTime.Now.Year)
            {
                _currentYear++;
                txtYear.Text = _currentYear.ToString();
                await LoadBalanceDataAsync();
            }
            else
            {
                MessageBox.Show("No se puede mostrar balance de años futuros",
                    "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Modelo para mapear la vista v_balance_gastos
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