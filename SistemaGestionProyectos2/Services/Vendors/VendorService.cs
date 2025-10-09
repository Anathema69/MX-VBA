using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Vendors
{
    public class VendorService : BaseSupabaseService
    {
        public VendorService(Client supabaseClient) : base(supabaseClient) { }

        /// <summary>
        /// Obtiene todos los vendedores activos
        /// </summary>
        public async Task<List<VendorTableDb>> GetActiveVendors()
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(x => x.IsActive == true)
                    .Order("f_vendorname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vendors = response?.Models ?? new List<VendorTableDb>();
                LogSuccess($"Vendedores activos obtenidos: {vendors.Count}");
                return vendors;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo vendedores activos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene todos los vendedores (activos e inactivos)
        /// </summary>
        public async Task<List<VendorTableDb>> GetAllVendors()
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Order("f_vendorname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vendors = response?.Models ?? new List<VendorTableDb>();
                LogSuccess($"Todos los vendedores obtenidos: {vendors.Count}");
                return vendors;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo todos los vendedores", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene vendedores como VendorDb (para compatibilidad)
        /// </summary>
        public async Task<List<VendorDb>> GetVendors()
        {
            try
            {
                LogDebug("Obteniendo vendedores de t_vendor...");

                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(x => x.IsActive == true)
                    .Order("f_vendorname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var vendors = response?.Models ?? new List<VendorTableDb>();

                LogSuccess($"Vendedores encontrados: {vendors.Count}");

                // Convertir VendorTableDb a VendorDb para compatibilidad
                return vendors.Select(v => new VendorDb
                {
                    Id = v.Id,
                    VendorName = v.VendorName
                }).ToList();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo vendedores", ex);
                throw new Exception($"Error al cargar vendedores: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtiene un vendedor por ID
        /// </summary>
        public async Task<VendorTableDb> GetVendorById(int vendorId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == vendorId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo vendedor {vendorId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Crea un nuevo vendedor
        /// </summary>
        public async Task<VendorTableDb> CreateVendor(VendorTableDb vendor)
        {
            try
            {
                // Asignar timestamps correctamente
                var now = DateTime.UtcNow;
                vendor.CreatedAt = now;
                vendor.UpdatedAt = now;
                vendor.IsActive = true;

                LogDebug($"Creando vendedor: {vendor.VendorName}");

                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Insert(vendor);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Vendedor creado: {vendor.VendorName}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el vendedor");
            }
            catch (Exception ex)
            {
                LogError("Error creando vendedor", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un vendedor existente
        /// </summary>
        public async Task<VendorTableDb> UpdateVendor(VendorTableDb vendor)
        {
            try
            {
                vendor.UpdatedAt = DateTime.UtcNow;

                LogDebug($"Actualizando vendedor: {vendor.VendorName}");

                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == vendor.Id)
                    .Set(v => v.VendorName, vendor.VendorName)
                    .Set(v => v.Phone, vendor.Phone)
                    .Set(v => v.Email, vendor.Email)
                    .Set(v => v.CommissionRate, vendor.CommissionRate)
                    .Set(v => v.UserId, vendor.UserId)
                    .Set(v => v.UpdatedAt, vendor.UpdatedAt)
                    .Update();

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Vendedor actualizado: {vendor.VendorName}");
                    return response.Models.First();
                }

                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando vendedor {vendor.Id}", ex);
                throw;
            }
        }

        /// <summary>
        /// Elimina un vendedor (soft delete)
        /// </summary>
        public async Task<bool> DeleteVendor(int vendorId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == vendorId)
                    .Set(v => v.IsActive, false)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Vendedor eliminado: {vendorId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando vendedor {vendorId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Desactiva un vendedor
        /// </summary>
        public async Task<bool> DeactivateVendor(int vendorId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == vendorId)
                    .Set(v => v.IsActive, false)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Vendedor desactivado: {vendorId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando vendedor {vendorId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reactiva un vendedor previamente desactivado
        /// </summary>
        public async Task<bool> ReactivateVendor(int vendorId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == vendorId)
                    .Set(v => v.IsActive, true)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Vendedor reactivado: {vendorId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error reactivando vendedor {vendorId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Verifica si existe un vendedor con el mismo nombre
        /// </summary>
        public async Task<bool> VendorExists(string vendorName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vendorName)) return false;

                var normalizedName = vendorName.Trim().ToUpper();

                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Filter("f_vendorname", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de vendedor", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtiene vendedor por ID de usuario asociado
        /// </summary>
        public async Task<VendorTableDb> GetVendorByUserId(int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.UserId == userId)
                    .Where(v => v.IsActive == true)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo vendedor por userId {userId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de vendedores
        /// </summary>
        public async Task<Dictionary<string, object>> GetVendorStats()
        {
            try
            {
                var allVendors = await GetAllVendors();
                var activeVendors = allVendors.Where(v => v.IsActive).ToList();

                var stats = new Dictionary<string, object>
                {
                    ["TotalVendedores"] = allVendors.Count,
                    ["VendedoresActivos"] = activeVendors.Count,
                    ["VendedoresInactivos"] = allVendors.Count - activeVendors.Count,
                    ["PromedioComision"] = activeVendors.Count > 0
                        ? activeVendors.Average(v => v.CommissionRate ?? 0)
                        : 0,
                    ["VendedoresConUsuario"] = activeVendors.Count(v => v.UserId.HasValue)
                };

                LogSuccess($"Estadísticas de vendedores calculadas");
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error calculando estadísticas de vendedores", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza la tasa de comisión de un vendedor
        /// </summary>
        public async Task<bool> UpdateCommissionRate(int vendorId, decimal commissionRate)
        {
            try
            {
                var response = await SupabaseClient
                    .From<VendorTableDb>()
                    .Where(v => v.Id == vendorId)
                    .Set(v => v.CommissionRate, commissionRate)
                    .Set(v => v.UpdatedAt, DateTime.UtcNow)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Tasa de comisión actualizada para vendedor {vendorId}: {commissionRate}%");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando tasa de comisión para vendedor {vendorId}", ex);
                return false;
            }
        }
    }
}
