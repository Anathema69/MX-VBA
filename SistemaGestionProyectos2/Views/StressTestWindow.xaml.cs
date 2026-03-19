using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using SistemaGestionProyectos2.Tests;

namespace SistemaGestionProyectos2.Views
{
    public class TestResultDisplay
    {
        public int Index { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public long ElapsedMs { get; set; }
        public long ThresholdMs { get; set; }
        public string Status { get; set; }
        public bool Passed { get; set; }

        // Colores para binding
        public Brush StatusBg => Passed
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7))  // verde claro
            : new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)); // rojo claro
        public Brush StatusFg => Passed
            ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))  // verde
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)); // rojo

        public Brush CategoryBg { get; set; }
        public Brush CategoryFg { get; set; }
    }

    public partial class StressTestWindow : Window
    {
        private readonly ObservableCollection<TestResultDisplay> _displayResults = new();
        private List<TestResult> _rawResults;
        private bool _isRunning;
        private int _passedCount;
        private int _failedCount;
        private readonly Stopwatch _totalTimer = new();

        // Colores por categoria (frozen para rendimiento)
        private static readonly Brush CacheBg = Freeze(new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)));
        private static readonly Brush CacheFg = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)));
        private static readonly Brush FilterBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFC, 0xE7, 0xF3)));
        private static readonly Brush FilterFg = Freeze(new SolidColorBrush(Color.FromRgb(0xBE, 0x18, 0x5D)));
        private static readonly Brush QueryBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)));
        private static readonly Brush QueryFg = Freeze(new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09)));
        private static readonly Brush StressBg = Freeze(new SolidColorBrush(Color.FromRgb(0xED, 0xE9, 0xFE)));
        private static readonly Brush StressFg = Freeze(new SolidColorBrush(Color.FromRgb(0x6D, 0x28, 0xD9)));
        private static readonly Brush WorkflowBg = Freeze(new SolidColorBrush(Color.FromRgb(0xCC, 0xFB, 0xF1)));
        private static readonly Brush WorkflowFg = Freeze(new SolidColorBrush(Color.FromRgb(0x0D, 0x94, 0x88)));

        private static Brush Freeze(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        public StressTestWindow()
        {
            InitializeComponent();
            MaximizeWithTaskbar();
            this.SourceInitialized += (s, e) => MaximizeWithTaskbar();
            lvResults.ItemsSource = _displayResults;
        }

        private void MaximizeWithTaskbar()
        {
            Helpers.WindowHelper.MaximizeToCurrentMonitor(this);
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            _isRunning = true;
            btnRun.IsEnabled = false;
            btnRunWorkflow.IsEnabled = false;
            btnCopy.IsEnabled = false;
            _displayResults.Clear();
            _passedCount = 0;
            _failedCount = 0;
            _rawResults = null;

            pbProgress.Value = 0;
            pbProgress.Maximum = 23;
            txtProgress.Text = "Ejecutando tests...";
            txtTotal.Text = "0 / 23";
            txtPassed.Text = "0";
            txtFailed.Text = "0";
            txtTotalTime.Text = "--";

            _totalTimer.Restart();

            try
            {
                var test = new PerformanceStressTest();
                _rawResults = await test.RunAllTests(result =>
                {
                    Dispatcher.Invoke(() => OnTestResult(result));
                });

                _totalTimer.Stop();
                txtProgress.Text = _failedCount == 0
                    ? "Todos los tests completados exitosamente"
                    : $"Completado con {_failedCount} test(s) fallido(s)";
                txtTotalTime.Text = $"{_totalTimer.ElapsedMilliseconds:N0} ms";
            }
            catch (Exception ex)
            {
                _totalTimer.Stop();
                txtProgress.Text = $"Error: {ex.Message}";
                txtTotalTime.Text = $"{_totalTimer.ElapsedMilliseconds:N0} ms";
            }
            finally
            {
                _isRunning = false;
                btnRun.IsEnabled = true;
                btnRunWorkflow.IsEnabled = true;
                btnCopy.IsEnabled = _rawResults != null && _rawResults.Count > 0;
            }
        }

        private void OnTestResult(TestResult result)
        {
            var index = _displayResults.Count + 1;

            if (result.Passed) _passedCount++;
            else _failedCount++;

            var (catBg, catFg) = GetCategoryColors(result.Category);

            _displayResults.Add(new TestResultDisplay
            {
                Index = index,
                Category = result.Category,
                Name = result.Name,
                ElapsedMs = result.ElapsedMs,
                ThresholdMs = result.ThresholdMs,
                Status = result.Passed ? "PASS" : "FAIL",
                Passed = result.Passed,
                CategoryBg = catBg,
                CategoryFg = catFg
            });

            pbProgress.Value = index;
            var max = (int)pbProgress.Maximum;
            txtProgressCount.Text = $"{index} / {max}";
            txtTotal.Text = $"{index} / {max}";
            txtPassed.Text = _passedCount.ToString();
            txtFailed.Text = _failedCount.ToString();
            txtTotalTime.Text = $"{_totalTimer.ElapsedMilliseconds:N0} ms";

            // Auto-scroll al ultimo item
            if (_displayResults.Count > 0)
                lvResults.ScrollIntoView(_displayResults[^1]);
        }

        private (Brush bg, Brush fg) GetCategoryColors(string category)
        {
            return category switch
            {
                "Cache" => (CacheBg, CacheFg),
                "ServerFilter" => (FilterBg, FilterFg),
                "Queries" => (QueryBg, QueryFg),
                "Stress" => (StressBg, StressFg),
                "Workflow" => (WorkflowBg, WorkflowFg),
                _ => (CacheBg, CacheFg)
            };
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_rawResults == null || _rawResults.Count == 0) return;

            try
            {
                var report = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    totalTests = _rawResults.Count,
                    passed = _rawResults.Count(r => r.Passed),
                    failed = _rawResults.Count(r => !r.Passed),
                    totalElapsedMs = _totalTimer.ElapsedMilliseconds,
                    tests = _rawResults.Select(r => new
                    {
                        r.Name,
                        r.Category,
                        r.Passed,
                        r.ElapsedMs,
                        r.ThresholdMs,
                        r.Error
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                Clipboard.SetText(json);

                // Feedback visual temporal
                var original = btnCopy.Content;
                btnCopy.Content = "Copiado!";
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    btnCopy.Content = original;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al copiar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRunWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            _isRunning = true;
            btnRun.IsEnabled = false;
            btnRunWorkflow.IsEnabled = false;
            btnCopy.IsEnabled = false;
            _displayResults.Clear();
            _passedCount = 0;
            _failedCount = 0;
            _rawResults = null;

            pbProgress.Value = 0;
            pbProgress.Maximum = 5;
            txtProgress.Text = "Ejecutando tests de workflow...";
            txtTotal.Text = "0 / 5";
            txtPassed.Text = "0";
            txtFailed.Text = "0";
            txtTotalTime.Text = "--";

            _totalTimer.Restart();

            try
            {
                var test = new DriveWorkflowTests();
                _rawResults = await test.RunAllTests(result =>
                {
                    Dispatcher.Invoke(() => OnTestResult(result));
                });

                _totalTimer.Stop();
                txtProgress.Text = _failedCount == 0
                    ? "Todos los workflow tests completados exitosamente"
                    : $"Completado con {_failedCount} test(s) fallido(s)";
                txtTotalTime.Text = $"{_totalTimer.ElapsedMilliseconds:N0} ms";
            }
            catch (Exception ex)
            {
                _totalTimer.Stop();
                txtProgress.Text = $"Error: {ex.Message}";
                txtTotalTime.Text = $"{_totalTimer.ElapsedMilliseconds:N0} ms";
            }
            finally
            {
                _isRunning = false;
                btnRun.IsEnabled = true;
                btnRunWorkflow.IsEnabled = true;
                btnCopy.IsEnabled = _rawResults != null && _rawResults.Count > 0;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
