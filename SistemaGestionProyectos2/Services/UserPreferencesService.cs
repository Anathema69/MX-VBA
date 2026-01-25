using System;
using System.IO;
using System.Text.Json;

namespace SistemaGestionProyectos2.Services
{
    /// <summary>
    /// Servicio para manejar preferencias de usuario en archivo local.
    /// Solo aplica para el rol "administracion".
    /// </summary>
    public static class UserPreferencesService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SistemaGestionProyectos2"
        );

        private static readonly string PreferencesFile = Path.Combine(AppDataFolder, "preferences_admin.json");

        /// <summary>
        /// Preferencias del usuario administrador
        /// </summary>
        public class AdminPreferences
        {
            public string OrdersStatusFilter { get; set; }
            public DateTime? LastUpdated { get; set; }
        }

        /// <summary>
        /// Guarda el filtro de estado de órdenes (solo para rol administracion)
        /// </summary>
        public static void SaveOrdersStatusFilter(string role, string filterValue)
        {
            if (role != "administracion")
                return;

            try
            {
                // Asegurar que existe el directorio
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                var preferences = new AdminPreferences
                {
                    OrdersStatusFilter = filterValue,
                    LastUpdated = DateTime.Now
                };

                var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(PreferencesFile, json);
                System.Diagnostics.Debug.WriteLine($"[Preferences] Guardado filtro: {filterValue}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Preferences] Error guardando: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene el filtro de estado guardado (solo para rol administracion)
        /// Retorna null si no existe o si el rol no es administracion
        /// </summary>
        public static string GetOrdersStatusFilter(string role)
        {
            if (role != "administracion")
                return null;

            try
            {
                if (!File.Exists(PreferencesFile))
                {
                    System.Diagnostics.Debug.WriteLine("[Preferences] Archivo no existe, usando default");
                    return null;
                }

                var json = File.ReadAllText(PreferencesFile);
                var preferences = JsonSerializer.Deserialize<AdminPreferences>(json);

                System.Diagnostics.Debug.WriteLine($"[Preferences] Cargado filtro: {preferences?.OrdersStatusFilter}");
                return preferences?.OrdersStatusFilter;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Preferences] Error leyendo: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Elimina el archivo de preferencias (para testing o reset)
        /// </summary>
        public static void ClearPreferences()
        {
            try
            {
                if (File.Exists(PreferencesFile))
                {
                    File.Delete(PreferencesFile);
                    System.Diagnostics.Debug.WriteLine("[Preferences] Archivo eliminado");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Preferences] Error eliminando: {ex.Message}");
            }
        }
    }
}
