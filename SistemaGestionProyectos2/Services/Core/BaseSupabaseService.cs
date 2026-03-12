using Supabase;
using System;

namespace SistemaGestionProyectos2.Services.Core
{
    /// <summary>
    /// Clase base para todos los servicios de Supabase
    /// Proporciona acceso común al cliente de Supabase
    /// </summary>
    public abstract class BaseSupabaseService
    {
        protected static readonly ServiceCache Cache = new();

        protected Client SupabaseClient { get; private set; }

        protected BaseSupabaseService(Client supabaseClient)
        {
            SupabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
        }

        /// <summary>
        /// Log helper para debug
        /// </summary>
        protected void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] {message}");
        }

        /// <summary>
        /// Log helper para errores
        /// </summary>
        protected void LogError(string message, Exception ex = null)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [{GetType().Name}] {message}");
            if (ex != null)
            {
                System.Diagnostics.Debug.WriteLine($"   Exception: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Log helper para éxito
        /// </summary>
        protected void LogSuccess(string message)
        {
            System.Diagnostics.Debug.WriteLine($"✅ [{GetType().Name}] {message}");
        }

        /// <summary>
        /// Invalida cachés de catálogos (vendedores, clientes, proveedores, statuses)
        /// Llamar desde el botón Actualizar para forzar recarga desde BD
        /// </summary>
        public static void InvalidateCatalogCaches()
        {
            Cache.InvalidatePrefix("vendors:");
            Cache.InvalidatePrefix("clients:");
            Cache.InvalidatePrefix("suppliers:");
            Cache.InvalidatePrefix("statuses:");
            System.Diagnostics.Debug.WriteLine("🔄 Cachés de catálogos invalidados");
        }
    }
}
