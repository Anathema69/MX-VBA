using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Suppliers
{
    public class SupplierService : BaseSupabaseService
    {
        public SupplierService(Client supabaseClient) : base(supabaseClient) { }

        /// <summary>
        /// Obtiene todos los proveedores activos
        /// </summary>
        public async Task<List<SupplierDb>> GetActiveSuppliers()
        {
            try
            {
                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.IsActive == true)
                    .Order("f_suppliername", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var suppliers = response?.Models ?? new List<SupplierDb>();
                LogSuccess($"Proveedores activos obtenidos: {suppliers.Count}");
                return suppliers;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo proveedores", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene todos los proveedores (activos e inactivos)
        /// </summary>
        public async Task<List<SupplierDb>> GetAllSuppliers()
        {
            try
            {
                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Order("f_suppliername", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var suppliers = response?.Models ?? new List<SupplierDb>();
                LogSuccess($"Todos los proveedores obtenidos: {suppliers.Count}");
                return suppliers;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo todos los proveedores", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene un proveedor por ID
        /// </summary>
        public async Task<SupplierDb> GetSupplierById(int supplierId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplierId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo proveedor {supplierId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Crea un nuevo proveedor
        /// </summary>
        public async Task<SupplierDb> CreateSupplier(SupplierDb supplier)
        {
            try
            {
                // Asignar timestamps correctamente
                var now = DateTime.UtcNow;
                supplier.CreatedAt = now;
                supplier.UpdatedAt = now;
                supplier.IsActive = true;

                LogDebug($"Creando proveedor: {supplier.SupplierName}");

                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Insert(supplier);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Proveedor creado: {supplier.SupplierName}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el proveedor");
            }
            catch (Exception ex)
            {
                LogError("Error creando proveedor", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un proveedor existente
        /// </summary>
        public async Task<bool> UpdateSupplier(SupplierDb supplier)
        {
            try
            {
                supplier.UpdatedAt = DateTime.UtcNow;

                LogDebug($"Actualizando proveedor: {supplier.SupplierName}");

                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplier.Id)
                    .Update(supplier);

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Proveedor actualizado: {supplier.SupplierName}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando proveedor {supplier.Id}", ex);
                return false;
            }
        }

        /// <summary>
        /// Elimina un proveedor (hard delete)
        /// </summary>
        public async Task<bool> DeleteSupplier(int supplierId)
        {
            try
            {
                await SupabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplierId)
                    .Delete();

                LogSuccess($"Proveedor eliminado: {supplierId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando proveedor {supplierId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Desactiva un proveedor (soft delete)
        /// </summary>
        public async Task<bool> DeactivateSupplier(int supplierId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Where(s => s.Id == supplierId)
                    .Set(s => s.IsActive, false)
                    .Set(s => s.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Proveedor desactivado: {supplierId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando proveedor {supplierId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Verifica si existe un proveedor con el mismo nombre
        /// </summary>
        public async Task<bool> SupplierExists(string supplierName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(supplierName)) return false;

                var normalizedName = supplierName.Trim().ToUpper();

                var response = await SupabaseClient
                    .From<SupplierDb>()
                    .Filter("f_suppliername", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de proveedor", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de proveedores
        /// </summary>
        public async Task<Dictionary<string, int>> GetSupplierStats()
        {
            try
            {
                var allSuppliers = await GetAllSuppliers();

                var stats = new Dictionary<string, int>
                {
                    ["Total"] = allSuppliers.Count,
                    ["Activos"] = allSuppliers.Count(s => s.IsActive),
                    ["Inactivos"] = allSuppliers.Count(s => !s.IsActive)
                };

                LogSuccess($"Estadísticas de proveedores calculadas");
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error calculando estadísticas de proveedores", ex);
                throw;
            }
        }
    }
}
