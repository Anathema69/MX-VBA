using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Supabase;

namespace SistemaGestionProyectos2.Services
{
    public class TestSupabaseService
    {
        private IConfiguration _configuration;

        public async Task<(bool Success, string Message, string Details)> TestConnection()
        {
            try
            {
                // Paso 1: Cargar configuración
                var builder = new ConfigurationBuilder()
                    .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                _configuration = builder.Build();

                // Paso 2: Obtener credenciales
                var url = _configuration["Supabase:Url"];
                var key = _configuration["Supabase:AnonKey"];

                // Validar que existan
                if (string.IsNullOrEmpty(url))
                {
                    return (false, "URL de Supabase no configurada", "Falta 'Supabase:Url' en appsettings.json");
                }

                if (string.IsNullOrEmpty(key))
                {
                    return (false, "API Key no configurada", "Falta 'Supabase:AnonKey' en appsettings.json");
                }

                // Paso 3: Intentar crear cliente
                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = false // No conectar realtime para el test
                };

                var client = new Client(url, key, options);

                // Paso 4: Inicializar
                await client.InitializeAsync();

                // Paso 5: Hacer una consulta simple para verificar
                var response = await client
                    .From<TestTable>()
                    .Select("*")
                    .Limit(1)
                    .Get();

                return (true,
                    "✅ Conexión exitosa",
                    $"URL: {url}\n" +
                    $"Cliente inicializado correctamente\n" +
                    $"Prueba de consulta: OK");
            }
            catch (System.IO.FileNotFoundException)
            {
                return (false,
                    "Archivo de configuración no encontrado",
                    "No se encuentra 'appsettings.json' en el directorio de la aplicación");
            }
            catch (Exception ex)
            {
                return (false,
                    "Error de conexión",
                    $"Tipo: {ex.GetType().Name}\n" +
                    $"Mensaje: {ex.Message}\n" +
                    $"Stack: {ex.StackTrace?.Split('\n')[0]}");
            }
        }

        // Clase temporal para la prueba
        [Postgrest.Attributes.Table("t_order")]
        private class TestTable : Postgrest.Models.BaseModel
        {
            [Postgrest.Attributes.PrimaryKey("f_order")]
            public int Id { get; set; }
        }
    }
}