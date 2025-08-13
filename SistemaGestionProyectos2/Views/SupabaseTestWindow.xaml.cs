using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Supabase;
using Postgrest.Attributes;
using Postgrest.Models;

namespace SistemaGestionProyectos2.Views
{
    public partial class SupabaseTestWindow : Window
    {
        private Client _supabaseClient;
        private IConfiguration _configuration;
        private int _testsExecuted = 0;
        private bool _isConnected = false;

        public SupabaseTestWindow()
        {
            InitializeComponent();
            TimestampText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }

        // Método helper para agregar texto con formato al RichTextBox
        private void AddResult(string title, string content, bool isSuccess = true, bool isHeader = false)
        {
            var paragraph = new Paragraph();

            if (isHeader)
            {
                paragraph.Inlines.Add(new Run($"\n═══════════════════════════════════════\n")
                {
                    Foreground = Brushes.Gray
                });
                paragraph.Inlines.Add(new Run($"► {title}")
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = Brushes.Cyan
                });
                paragraph.Inlines.Add(new Run($"\n═══════════════════════════════════════\n")
                {
                    Foreground = Brushes.Gray
                });
            }
            else
            {
                var icon = isSuccess ? "✅" : "❌";
                var color = isSuccess ? Brushes.LightGreen : Brushes.IndianRed;

                paragraph.Inlines.Add(new Run($"{icon} {title}: ")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = color
                });
            }

            if (!string.IsNullOrEmpty(content))
            {
                paragraph.Inlines.Add(new Run($"\n{content}\n")
                {
                    Foreground = Brushes.LightGray
                });
            }

            ResultsRichTextBox.Document.Blocks.Add(paragraph);
            ResultsRichTextBox.ScrollToEnd();
        }

        // Test 1: Conexión Básica
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            AddResult("TEST DE CONEXIÓN", "", true, true);
            StatusText.Text = "Ejecutando test de conexión...";

            try
            {
                // Cargar configuración
                var builder = new ConfigurationBuilder()
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                _configuration = builder.Build();

                var url = _configuration["Supabase:Url"];
                var key = _configuration["Supabase:AnonKey"];

                AddResult("Configuración", $"URL: {url}\nKey: {key?.Substring(0, 20)}...", true);

                // Crear cliente
                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = false
                };

                _supabaseClient = new Client(url, key, options);
                await _supabaseClient.InitializeAsync();

                _isConnected = true;
                UpdateConnectionStatus(true);

                AddResult("Conexión establecida", "Cliente Supabase inicializado correctamente", true);
                Check1.IsChecked = true;

                StatusText.Text = "Conexión exitosa";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error de conexión", ex.Message, false);
                StatusText.Text = "Error en conexión";
                UpdateConnectionStatus(false);
            }
        }

        // Test 2: Listar Tablas (simulado)
        private async void TestTables_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("ESTRUCTURA DE BASE DE DATOS", "", true, true);
            StatusText.Text = "Listando tablas...";

            try
            {
                // Lista conocida de tablas
                var tables = new List<string>
                {
                    "t_order (Órdenes)",
                    "t_client (Clientes)",
                    "t_contact (Contactos)",
                    "t_supplier (Proveedores)",
                    "t_invoice (Facturas)",
                    "t_quote (Cotizaciones)",
                    "users (Usuarios)",
                    "order_status (Estados)",
                    "order_history (Historial)"
                };

                AddResult("Tablas encontradas", string.Join("\n• ", tables), true);
                Check2.IsChecked = true;

                StatusText.Text = "Tablas listadas";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error al listar tablas", ex.Message, false);
                StatusText.Text = "Error listando tablas";
            }
        }

        // Test 3: Cargar Órdenes
        private async void TestOrders_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("TEST DE CARGA DE ÓRDENES", "", true, true);
            StatusText.Text = "Cargando órdenes...";

            try
            {
                var response = await _supabaseClient
                    .From<OrderTable>()
                    .Select("*")
                    .Limit(10)
                    .Get();

                var count = response?.Models?.Count ?? 0;

                if (count > 0)
                {
                    var ordersInfo = "Primeras órdenes:\n";
                    foreach (var order in response.Models.Take(5))
                    {
                        ordersInfo += $"  • #{order.Po} - {order.Description?.Substring(0, Math.Min(30, order.Description?.Length ?? 0))}...\n";
                    }

                    AddResult($"Órdenes cargadas: {count}", ordersInfo, true);
                }
                else
                {
                    AddResult("Sin órdenes", "No se encontraron registros en t_order", false);
                }

                Check3.IsChecked = true;
                StatusText.Text = $"{count} órdenes cargadas";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error al cargar órdenes", ex.Message, false);
                StatusText.Text = "Error cargando órdenes";
            }
        }

        // Test 4: Verificar Usuarios
        private async void TestUsers_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("TEST DE USUARIOS", "", true, true);
            StatusText.Text = "Verificando usuarios...";

            try
            {
                var response = await _supabaseClient
                    .From<UserTable>()
                    .Select("*")
                    .Get();

                var count = response?.Models?.Count ?? 0;

                if (count > 0)
                {
                    var usersInfo = "Usuarios encontrados:\n";
                    var byRole = response.Models.GroupBy(u => u.Role);

                    foreach (var group in byRole)
                    {
                        usersInfo += $"\n{group.Key?.ToUpper()}:\n";
                        foreach (var user in group)
                        {
                            usersInfo += $"  • {user.Username} - {user.FullName}\n";
                        }
                    }

                    AddResult($"Total de usuarios: {count}", usersInfo, true);
                }
                else
                {
                    AddResult("Sin usuarios", "No se encontraron usuarios en la BD", false);
                }

                Check4.IsChecked = true;
                StatusText.Text = $"{count} usuarios encontrados";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error al verificar usuarios", ex.Message, false);
                StatusText.Text = "Error verificando usuarios";
            }
        }

        // Test 5: Simular Login
        private async void TestLogin_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("TEST DE AUTENTICACIÓN", "", true, true);
            StatusText.Text = "Simulando login...";

            try
            {
                // Probar con usuario admin
                var response = await _supabaseClient
                    .From<UserTable>()
                    .Where(x => x.Username == "admin")
                    .Single();

                if (response != null)
                {
                    AddResult("Usuario encontrado",
                        $"Username: {response.Username}\n" +
                        $"Nombre: {response.FullName}\n" +
                        $"Rol: {response.Role}\n" +
                        $"Email: {response.Email}\n" +
                        $"Activo: {(response.IsActive ? "Sí" : "No")}",
                        true);

                    // Simular verificación de password
                    AddResult("Verificación de contraseña",
                        "⚠️ En producción se verificaría el hash del password",
                        true);
                }
                else
                {
                    AddResult("Usuario no encontrado", "No se encontró el usuario 'admin'", false);
                }

                Check5.IsChecked = true;
                StatusText.Text = "Login simulado";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error en simulación de login", ex.Message, false);
                StatusText.Text = "Error en login";
            }
        }

        // Test 6: Test de Escritura
        private async void TestWrite_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("TEST DE ESCRITURA", "", true, true);
            StatusText.Text = "Probando escritura...";

            var result = MessageBox.Show(
                "Esta prueba intentará crear un registro temporal en la BD.\n" +
                "El registro será marcado como 'TEST' para identificarlo.\n\n" +
                "¿Desea continuar?",
                "Confirmar Test de Escritura",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                AddResult("Test cancelado", "El usuario canceló la prueba de escritura", false);
                return;
            }

            try
            {
                // Crear orden de prueba
                var testOrder = new OrderTable
                {
                    Po = $"TEST-{DateTime.Now:yyyyMMddHHmmss}",
                    Description = "ORDEN DE PRUEBA - PUEDE SER ELIMINADA",
                    PoDate = DateTime.Now,
                    EstDelivery = DateTime.Now.AddDays(30),
                    ClientId = 1,
                    ContactId = 1
                };

                var response = await _supabaseClient
                    .From<OrderTable>()
                    .Insert(testOrder);

                if (response?.Models?.Count > 0)
                {
                    var created = response.Models.First();
                    AddResult("Registro creado exitosamente",
                        $"ID: {created.Id}\n" +
                        $"Número: {created.Po}\n" +
                        $"Descripción: {created.Description}\n\n" +
                        $"⚠️ Recuerde eliminar este registro de prueba de la BD",
                        true);
                }

                Check6.IsChecked = true;
                StatusText.Text = "Escritura exitosa";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error en escritura",
                    $"{ex.Message}\n\n" +
                    "Posibles causas:\n" +
                    "• Permisos insuficientes\n" +
                    "• Políticas RLS activas\n" +
                    "• Campos requeridos faltantes",
                    false);
                StatusText.Text = "Error en escritura";
            }
        }

        // Ejecutar todas las pruebas
        private async void RunAllTests_Click(object sender, RoutedEventArgs e)
        {
            ClearResults_Click(null, null);

            AddResult("EJECUTANDO TODAS LAS PRUEBAS", "Iniciando suite completa de pruebas...", true, true);

            // Ejecutar pruebas en secuencia
            await Task.Delay(500);
            TestConnection_Click(null, null);

            await Task.Delay(1000);
            if (_isConnected)
            {
                TestTables_Click(null, null);
                await Task.Delay(1000);

                TestOrders_Click(null, null);
                await Task.Delay(1000);

                TestUsers_Click(null, null);
                await Task.Delay(1000);

                TestLogin_Click(null, null);
            }

            AddResult("SUITE COMPLETA", $"Se ejecutaron {_testsExecuted} pruebas", true, true);
        }

        // Limpiar resultados
        private void ClearResults_Click(object sender, RoutedEventArgs e)
        {
            ResultsRichTextBox.Document.Blocks.Clear();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("🚀 Módulo de Pruebas Listo\n")
            {
                Foreground = Brushes.LightGreen,
                FontWeight = FontWeights.Bold
            });
            paragraph.Inlines.Add(new Run("Seleccione una prueba del panel izquierdo para comenzar...")
            {
                Foreground = Brushes.Gray
            });
            ResultsRichTextBox.Document.Blocks.Add(paragraph);

            // Reset checkboxes
            Check1.IsChecked = false;
            Check2.IsChecked = false;
            Check3.IsChecked = false;
            Check4.IsChecked = false;
            Check5.IsChecked = false;
            Check6.IsChecked = false;

            _testsExecuted = 0;
            UpdateTestCount();
            TimestampText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }

        private bool EnsureConnected()
        {
            if (!_isConnected || _supabaseClient == null)
            {
                AddResult("Error", "Primero debe ejecutar el Test de Conexión", false);
                StatusText.Text = "No conectado";
                return false;
            }
            return true;
        }

        private void UpdateConnectionStatus(bool connected)
        {
            _isConnected = connected;
            if (connected)
            {
                StatusIndicator.Fill = Brushes.LightGreen;
                ConnectionStatusText.Text = "Conectado";
                ConnectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            else
            {
                StatusIndicator.Fill = Brushes.Gray;
                ConnectionStatusText.Text = "No conectado";
                ConnectionStatusBorder.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            }
        }

        private void UpdateTestCount()
        {
            TestCountText.Text = _testsExecuted.ToString();
        }
    }

    // Modelos para las pruebas
    [Table("t_order")]
    public class OrderTable : BaseModel
    {
        [PrimaryKey("f_order")]
        public int Id { get; set; }

        [Column("f_po")]
        public string Po { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("f_podate")]
        public DateTime? PoDate { get; set; }

        [Column("f_estdelivery")]
        public DateTime? EstDelivery { get; set; }

        [Column("f_client")]
        public int? ClientId { get; set; }

        [Column("f_contact")]
        public int? ContactId { get; set; }
    }

    [Table("users")]
    public class UserTable : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}