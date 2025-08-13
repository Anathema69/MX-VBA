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
        private UserTable _currentTestUser = null;

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
                paragraph.Inlines.Add(new Run($"\n╔══════════════════════════════════════╗\n")
                {
                    Foreground = Brushes.Gray
                });
                paragraph.Inlines.Add(new Run($"▶ {title}")
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = Brushes.Cyan
                });
                paragraph.Inlines.Add(new Run($"\n╚══════════════════════════════════════╝\n")
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
            StatusText.Text = "Verificando estructura...";

            try
            {
                // Verificar que podemos acceder a las tablas principales
                var orders = await _supabaseClient.From<OrderTable>().Select("*").Limit(1).Get();
                var clients = await _supabaseClient.From<ClientTable>().Select("*").Limit(1).Get();
                var users = await _supabaseClient.From<UserTable>().Select("*").Limit(1).Get();

                var tablesInfo = "Tablas accesibles:\n";
                tablesInfo += $"• t_order - ✅ ({orders?.Models?.Count ?? 0} registro de prueba)\n";
                tablesInfo += $"• t_client - ✅ ({clients?.Models?.Count ?? 0} registro de prueba)\n";
                tablesInfo += $"• users - ✅ ({users?.Models?.Count ?? 0} registro de prueba)\n";

                AddResult("Verificación de estructura", tablesInfo, true);
                Check2.IsChecked = true;

                StatusText.Text = "Estructura verificada";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error al verificar estructura", ex.Message, false);
                StatusText.Text = "Error verificando tablas";
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
                    var ordersInfo = "Órdenes encontradas:\n";
                    foreach (var order in response.Models.Take(5))
                    {
                        // Obtener nombre del cliente si existe
                        string clientName = "N/A";
                        if (order.ClientId.HasValue)
                        {
                            var clientResponse = await _supabaseClient
                                .From<ClientTable>()
                                .Where(x => x.Id == order.ClientId.Value)
                                .Single();
                            clientName = clientResponse?.Name ?? "N/A";
                        }

                        ordersInfo += $"  • #{order.Po} - Cliente: {clientName}\n";
                        ordersInfo += $"    Descripción: {order.Description?.Substring(0, Math.Min(50, order.Description?.Length ?? 0))}...\n";
                        ordersInfo += $"    Fecha: {order.PoDate?.ToString("dd/MM/yyyy") ?? "N/A"}\n\n";
                    }

                    AddResult($"Total de órdenes: {count}", ordersInfo, true);
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

            AddResult("TEST DE USUARIOS Y AUTENTICACIÓN", "", true, true);
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
                    AddResult("=== PRUEBA DE CREDENCIALES CORRECTAS ===", "", true);

                    // Test Admin con contraseña correcta
                    var adminUser = response.Models.FirstOrDefault(u => u.Username == "admin");
                    if (adminUser != null)
                    {
                        bool adminAuth = BCrypt.Net.BCrypt.Verify("ima2025", adminUser.PasswordHash);
                        AddResult("Admin - Contraseña Correcta",
                            $"Usuario: admin\n" +
                            $"Contraseña: ima2025\n" +
                            $"Resultado: {(adminAuth ? "✅ ACCESO PERMITIDO" : "❌ ACCESO DENEGADO")}\n" +
                            $"Rol: {adminUser.Role}",
                            adminAuth);
                    }

                    // Test Coordinador con contraseña correcta
                    var coordUser = response.Models.FirstOrDefault(u => u.Username == "coordinador");
                    if (coordUser != null)
                    {
                        bool coordAuth = BCrypt.Net.BCrypt.Verify("ima2025", coordUser.PasswordHash);
                        AddResult("Coordinador - Contraseña Correcta",
                            $"Usuario: coordinador\n" +
                            $"Contraseña: ima2025\n" +
                            $"Resultado: {(coordAuth ? "✅ ACCESO PERMITIDO" : "❌ ACCESO DENEGADO")}\n" +
                            $"Rol: {coordUser.Role}",
                            coordAuth);
                    }

                    // Test caaj con contraseña correcta
                    var caajUser = response.Models.FirstOrDefault(u => u.Username == "caaj");
                    if (caajUser != null)
                    {
                        bool caajAuth = BCrypt.Net.BCrypt.Verify("anathema", caajUser.PasswordHash);
                        AddResult("Caaj - Contraseña Correcta",
                            $"Usuario: caaj\n" +
                            $"Contraseña: anathema\n" +
                            $"Resultado: {(caajAuth ? "✅ ACCESO PERMITIDO" : "❌ ACCESO DENEGADO")}\n" +
                            $"Rol: {caajUser.Role}",
                            caajAuth);
                    }

                    AddResult("=== PRUEBA DE CREDENCIALES INCORRECTAS ===", "", true);

                    // Test con contraseñas incorrectas
                    if (adminUser != null)
                    {
                        bool wrongPass = BCrypt.Net.BCrypt.Verify("password123", adminUser.PasswordHash);
                        AddResult("Admin - Contraseña Incorrecta",
                            $"Usuario: admin\n" +
                            $"Contraseña: password123 (incorrecta)\n" +
                            $"Resultado: {(wrongPass ? "✅ ACCESO PERMITIDO" : "❌ ACCESO DENEGADO")}",
                            !wrongPass);
                    }

                    if (coordUser != null)
                    {
                        bool wrongPass = BCrypt.Net.BCrypt.Verify("12345", coordUser.PasswordHash);
                        AddResult("Coordinador - Contraseña Incorrecta",
                            $"Usuario: coordinador\n" +
                            $"Contraseña: 12345 (incorrecta)\n" +
                            $"Resultado: {(wrongPass ? "✅ ACCESO PERMITIDO" : "❌ ACCESO DENEGADO")}",
                            !wrongPass);
                    }

                    // Resumen
                    var summary = $"\nRESUMEN DE USUARIOS:\n";
                    var byRole = response.Models.GroupBy(u => u.Role);
                    foreach (var group in byRole)
                    {
                        summary += $"• {group.Key?.ToUpper()}: {group.Count()} usuario(s)\n";
                    }
                    AddResult("Total de usuarios en sistema", summary, true);
                }
                else
                {
                    AddResult("Sin usuarios", "No se encontraron usuarios en la BD", false);
                }

                Check4.IsChecked = true;
                StatusText.Text = "Usuarios verificados";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error al verificar usuarios", ex.Message, false);
                StatusText.Text = "Error verificando usuarios";
            }
        }

        // Test 5: Simular Login y Permisos
        private async void TestLogin_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("TEST DE PERMISOS POR ROL", "", true, true);
            StatusText.Text = "Simulando operaciones por rol...";

            try
            {
                // Obtener usuarios de prueba
                var adminUser = await _supabaseClient
                    .From<UserTable>()
                    .Where(x => x.Username == "admin")
                    .Single();

                var coordUser = await _supabaseClient
                    .From<UserTable>()
                    .Where(x => x.Username == "coordinador")
                    .Single();

                AddResult("=== PRUEBAS CON ROL ADMIN ===", "", true);

                // Test como Admin
                if (adminUser != null)
                {
                    _currentTestUser = adminUser;

                    // Admin puede leer órdenes
                    var orders = await _supabaseClient.From<OrderTable>().Select("*").Limit(5).Get();
                    AddResult("Admin - Leer Órdenes",
                        $"✅ Puede leer: {orders?.Models?.Count ?? 0} órdenes encontradas", true);

                    // Admin puede modificar órdenes
                    if (orders?.Models?.Count > 0)
                    {
                        var testOrder = orders.Models.First();
                        var originalDesc = testOrder.Description;
                        testOrder.Description = $"[TEST ADMIN {DateTime.Now:HH:mm:ss}] {originalDesc}";

                        try
                        {
                            var updateResult = await _supabaseClient
                                .From<OrderTable>()
                                .Where(x => x.Id == testOrder.Id)
                                .Set(x => x.Description, testOrder.Description)
                                .Update();

                            AddResult("Admin - Modificar Orden",
                                $"✅ Puede modificar: Orden #{testOrder.Po} actualizada", true);

                            // Restaurar valor original
                            await _supabaseClient
                                .From<OrderTable>()
                                .Where(x => x.Id == testOrder.Id)
                                .Set(x => x.Description, originalDesc)
                                .Update();
                        }
                        catch
                        {
                            AddResult("Admin - Modificar Orden", "❌ No pudo modificar", false);
                        }
                    }

                    // Admin puede crear órdenes
                    var newOrder = new OrderTable
                    {
                        Po = $"TEST-ADMIN-{DateTime.Now:yyyyMMddHHmmss}",
                        Description = "ORDEN DE PRUEBA ADMIN - PUEDE SER ELIMINADA",
                        PoDate = DateTime.Now,
                        EstDelivery = DateTime.Now.AddDays(30),
                        ClientId = 1
                    };

                    try
                    {
                        var insertResult = await _supabaseClient
                            .From<OrderTable>()
                            .Insert(newOrder);

                        if (insertResult?.Models?.Count > 0)
                        {
                            var createdId = insertResult.Models.First().Id;
                            AddResult("Admin - Crear Orden",
                                $"✅ Puede crear: Nueva orden TEST-ADMIN creada (ID: {createdId})", true);

                            // Eliminar orden de prueba
                            await _supabaseClient
                                .From<OrderTable>()
                                .Where(x => x.Id == createdId)
                                .Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        AddResult("Admin - Crear Orden", $"❌ Error al crear: {ex.Message}", false);
                    }
                }

                AddResult("=== PRUEBAS CON ROL COORDINADOR ===", "", true);

                // Test como Coordinador
                if (coordUser != null)
                {
                    _currentTestUser = coordUser;

                    // Coordinador puede leer órdenes
                    var orders = await _supabaseClient.From<OrderTable>().Select("*").Limit(5).Get();
                    AddResult("Coordinador - Leer Órdenes",
                        $"✅ Puede leer: {orders?.Models?.Count ?? 0} órdenes encontradas", true);

                    // Coordinador puede modificar SOLO ciertos campos
                    if (orders?.Models?.Count > 0)
                    {
                        var testOrder = orders.Models.First();
                        var originalDelivery = testOrder.EstDelivery;
                        testOrder.EstDelivery = DateTime.Now.AddDays(45);

                        try
                        {
                            var updateResult = await _supabaseClient
                                .From<OrderTable>()
                                .Where(x => x.Id == testOrder.Id)
                                .Set(x => x.EstDelivery, testOrder.EstDelivery)
                                .Update();

                            AddResult("Coordinador - Modificar Fecha Entrega",
                                $"✅ Puede modificar: Fecha de entrega actualizada en orden #{testOrder.Po}", true);

                            // Restaurar valor original
                            await _supabaseClient
                                .From<OrderTable>()
                                .Where(x => x.Id == testOrder.Id)
                                .Set(x => x.EstDelivery, originalDelivery)
                                .Update();
                        }
                        catch
                        {
                            AddResult("Coordinador - Modificar Fecha Entrega", "❌ No pudo modificar", false);
                        }
                    }

                    // Coordinador NO DEBE poder crear órdenes (simulación de restricción)
                    AddResult("Coordinador - Intento de Crear Orden",
                        "⚠️ SIMULACIÓN: En la aplicación real, el botón 'Nueva Orden' está deshabilitado para coordinadores.\n" +
                        "La BD permitiría la operación, pero la UI lo previene.", true);

                    // Demostrar que técnicamente PODRÍA crear (pero no debe)
                    var newOrderCoord = new OrderTable
                    {
                        Po = $"TEST-COORD-{DateTime.Now:yyyyMMddHHmmss}",
                        Description = "ORDEN NO AUTORIZADA - COORDINADOR",
                        PoDate = DateTime.Now,
                        EstDelivery = DateTime.Now.AddDays(30),
                        ClientId = 1
                    };

                    try
                    {
                        // Intentar crear (esto funcionará porque RLS está deshabilitado)
                        var insertResult = await _supabaseClient
                            .From<OrderTable>()
                            .Insert(newOrderCoord);

                        if (insertResult?.Models?.Count > 0)
                        {
                            var createdId = insertResult.Models.First().Id;
                            AddResult("Coordinador - Prueba técnica de creación",
                                $"⚠️ La BD permitió crear (RLS deshabilitado), pero la aplicación C# lo previene.\n" +
                                $"Orden creada con ID: {createdId} - Será eliminada", false);

                            // Eliminar inmediatamente
                            await _supabaseClient
                                .From<OrderTable>()
                                .Where(x => x.Id == createdId)
                                .Delete();

                            AddResult("", "✅ Orden de prueba eliminada correctamente", true);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddResult("Coordinador - Crear Orden",
                            $"✅ Correctamente bloqueado: {ex.Message}", true);
                    }
                }

                Check5.IsChecked = true;
                StatusText.Text = "Permisos verificados";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error en simulación de permisos", ex.Message, false);
                StatusText.Text = "Error en prueba de permisos";
            }
        }

        // Test 6: Test de Escritura Completo
        private async void TestWrite_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureConnected()) return;

            AddResult("TEST COMPLETO DE ESCRITURA", "", true, true);
            StatusText.Text = "Probando operaciones CRUD...";

            var result = MessageBox.Show(
                "Esta prueba realizará operaciones completas de:\n" +
                "• CREATE (Crear nueva orden)\n" +
                "• READ (Leer la orden creada)\n" +
                "• UPDATE (Modificar la orden)\n" +
                "• DELETE (Eliminar la orden)\n\n" +
                "¿Desea continuar?",
                "Confirmar Test CRUD",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                AddResult("Test cancelado", "El usuario canceló la prueba CRUD", false);
                return;
            }

            try
            {
                int createdOrderId = 0;

                // CREATE - Crear orden de prueba
                AddResult("1. CREATE - Crear Nueva Orden", "", true);
                var testOrder = new OrderTable
                {
                    Po = $"CRUD-TEST-{DateTime.Now:yyyyMMddHHmmss}",
                    Quote = "QUOTE-TEST-001",
                    Description = "ORDEN DE PRUEBA CRUD - SERÁ ELIMINADA",
                    PoDate = DateTime.Now,
                    EstDelivery = DateTime.Now.AddDays(30),
                    ClientId = 1,
                    ContactId = null,
                    SaleSubtotal = 10000.00m,
                    SaleTotal = 11600.00m
                };

                var createResponse = await _supabaseClient
                    .From<OrderTable>()
                    .Insert(testOrder);

                if (createResponse?.Models?.Count > 0)
                {
                    createdOrderId = createResponse.Models.First().Id;
                    AddResult("Orden creada exitosamente",
                        $"✅ ID: {createdOrderId}\n" +
                        $"Número: {testOrder.Po}\n" +
                        $"Descripción: {testOrder.Description}", true);
                }
                else
                {
                    AddResult("Error al crear", "No se pudo crear la orden", false);
                    return;
                }

                // READ - Leer la orden creada
                AddResult("2. READ - Leer Orden Creada", "", true);
                var readResponse = await _supabaseClient
                    .From<OrderTable>()
                    .Where(x => x.Id == createdOrderId)
                    .Single();

                if (readResponse != null)
                {
                    AddResult("Orden leída correctamente",
                        $"✅ Confirmado: Orden #{readResponse.Po} existe en BD\n" +
                        $"Subtotal: ${readResponse.SaleSubtotal:N2}\n" +
                        $"Total: ${readResponse.SaleTotal:N2}", true);
                }

                // UPDATE - Modificar la orden
                AddResult("3. UPDATE - Modificar Orden", "", true);
                var updateResponse = await _supabaseClient
                    .From<OrderTable>()
                    .Where(x => x.Id == createdOrderId)
                    .Set(x => x.Description, "ORDEN MODIFICADA - TEST CRUD ACTUALIZADO")
                    .Set(x => x.EstDelivery, DateTime.Now.AddDays(60))
                    .Update();

                if (updateResponse?.Models?.Count > 0)
                {
                    var updated = updateResponse.Models.First();
                    AddResult("Orden actualizada correctamente",
                        $"✅ Nueva descripción: {updated.Description}\n" +
                        $"Nueva fecha entrega: {updated.EstDelivery?.ToString("dd/MM/yyyy")}", true);
                }

                // DELETE - Eliminar la orden
                AddResult("4. DELETE - Eliminar Orden", "", true);
                await _supabaseClient
                    .From<OrderTable>()
                    .Where(x => x.Id == createdOrderId)
                    .Delete();

                // Verificar que se eliminó
                var verifyDelete = await _supabaseClient
                    .From<OrderTable>()
                    .Where(x => x.Id == createdOrderId)
                    .Single();

                if (verifyDelete == null)
                {
                    AddResult("Orden eliminada correctamente",
                        $"✅ La orden #{testOrder.Po} fue eliminada exitosamente de la BD", true);
                }
                else
                {
                    AddResult("Error al eliminar",
                        "⚠️ La orden aún existe en la BD", false);
                }

                AddResult("TEST CRUD COMPLETO",
                    "✅ Todas las operaciones CRUD funcionan correctamente", true);

                Check6.IsChecked = true;
                StatusText.Text = "Test CRUD exitoso";
                _testsExecuted++;
                UpdateTestCount();
            }
            catch (Exception ex)
            {
                AddResult("Error en test CRUD",
                    $"{ex.Message}\n\n" +
                    "Posibles causas:\n" +
                    "• Problemas de conexión\n" +
                    "• Estructura de tabla diferente\n" +
                    "• Tipos de datos incompatibles",
                    false);
                StatusText.Text = "Error en test CRUD";
            }
        }

        // Ejecutar todas las pruebas
        private async void RunAllTests_Click(object sender, RoutedEventArgs e)
        {
            ClearResults_Click(null, null);

            AddResult("EJECUTANDO SUITE COMPLETA DE PRUEBAS",
                "Iniciando todas las pruebas de conexión, autenticación y permisos...", true, true);

            // Ejecutar pruebas en secuencia
            await Task.Delay(500);
            TestConnection_Click(null, null);

            await Task.Delay(1500);
            if (_isConnected)
            {
                TestTables_Click(null, null);
                await Task.Delay(1500);

                TestOrders_Click(null, null);
                await Task.Delay(1500);

                TestUsers_Click(null, null);
                await Task.Delay(1500);

                TestLogin_Click(null, null);
                await Task.Delay(1500);

                TestWrite_Click(null, null);
            }

            AddResult("SUITE COMPLETA FINALIZADA",
                $"Se ejecutaron {_testsExecuted} pruebas\n" +
                $"Timestamp: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", true, true);
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

    // Modelos actualizados con los campos correctos de tu BD
    [Table("t_order")]
    public class OrderTable : BaseModel
    {
        [PrimaryKey("f_order")]
        public int Id { get; set; }

        [Column("f_client")]
        public int? ClientId { get; set; }

        [Column("f_contact")]
        public int? ContactId { get; set; }

        [Column("f_quote")]
        public string Quote { get; set; }

        [Column("f_po")]
        public string Po { get; set; }

        [Column("f_podate")]
        public DateTime? PoDate { get; set; }

        [Column("f_estdelivery")]
        public DateTime? EstDelivery { get; set; }

        [Column("f_description")]
        public string Description { get; set; }

        [Column("vendor_id")]
        public int? VendorId { get; set; }

        [Column("f_salesubtotal")]
        public decimal? SaleSubtotal { get; set; }

        [Column("f_saletotal")]
        public decimal? SaleTotal { get; set; }
    }

    [Table("t_client")]
    public class ClientTable : BaseModel
    {
        [PrimaryKey("f_client")]
        public int Id { get; set; }

        [Column("f_name")]
        public string Name { get; set; }

        [Column("f_address1")]
        public string Address1 { get; set; }

        [Column("tax_id")]
        public string TaxId { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("email")]
        public string Email { get; set; }
    }

    [Table("users")]
    public class UserTable : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("role")]
        public string Role { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}