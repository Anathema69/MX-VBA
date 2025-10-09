using SistemaGestionProyectos2.Tests;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SistemaGestionProyectos2.Views
{
    public partial class TestRunnerWindow : Window
    {
        private StringBuilder _logBuilder;

        public TestRunnerWindow()
        {
            InitializeComponent();
            _logBuilder = new StringBuilder();
        }

        private async void RunTestsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Deshabilitar botón durante ejecución
                RunTestsButton.IsEnabled = false;
                StatusTextBlock.Text = "⏳ Ejecutando tests...";
                ResultsTextBlock.Text = "Iniciando tests de validación...\n\n";
                _logBuilder.Clear();

                // Redirigir Console.WriteLine a nuestro TextBlock
                var originalOut = Console.Out;
                var writer = new StringWriter(_logBuilder);
                Console.SetOut(writer);

                // Ejecutar tests
                var tests = new SupabaseServiceIntegrationTests();
                bool allPassed = await tests.RunAllTests();

                // Restaurar Console
                Console.SetOut(originalOut);

                // Mostrar resultados
                ResultsTextBlock.Text = _logBuilder.ToString();

                // Actualizar estado
                if (allPassed)
                {
                    StatusTextBlock.Text = "✅ Todos los tests pasaron exitosamente";
                    StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(39, 174, 96)); // Verde
                }
                else
                {
                    StatusTextBlock.Text = "❌ Algunos tests fallaron - Revisa los detalles arriba";
                    StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(231, 76, 60)); // Rojo
                }
            }
            catch (Exception ex)
            {
                ResultsTextBlock.Text = $"❌ ERROR EJECUTANDO TESTS:\n\n{ex.Message}\n\n{ex.StackTrace}";
                StatusTextBlock.Text = "❌ Error durante la ejecución de tests";
                StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(231, 76, 60));
            }
            finally
            {
                RunTestsButton.IsEnabled = true;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsTextBlock.Text = "Presiona 'Ejecutar Todos los Tests' para iniciar...";
            StatusTextBlock.Text = "Listo para ejecutar tests";
            StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(52, 73, 94));
            _logBuilder.Clear();
        }
    }
}
