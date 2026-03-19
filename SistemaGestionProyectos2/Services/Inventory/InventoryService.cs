using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Models.DTOs;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Inventory
{
    public class InventoryService : BaseSupabaseService
    {
        public InventoryService(Client supabaseClient) : base(supabaseClient) { }

        // ==========================================
        // CATEGORIAS
        // ==========================================

        public async Task<List<InventoryCategoryDb>> GetCategories()
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryCategoryDb>()
                    .Filter("is_active", Postgrest.Constants.Operator.Equals, "true")
                    .Order("display_order", Postgrest.Constants.Ordering.Ascending)
                    .Order("name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var list = response?.Models ?? new List<InventoryCategoryDb>();
                LogSuccess($"Categorias obtenidas: {list.Count}");
                return list;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo categorias", ex);
                return new List<InventoryCategoryDb>();
            }
        }

        /// <summary>
        /// Obtiene el resumen por categoria desde la vista v_inventory_category_summary.
        /// Postgrest expone vistas como tablas read-only.
        /// </summary>
        public async Task<List<CategorySummaryDto>> GetCategorySummary()
        {
            try
            {
                var response = await SupabaseClient
                    .From<CategorySummaryView>()
                    .Order("display_order", Postgrest.Constants.Ordering.Ascending)
                    .Order("name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var models = response?.Models ?? new List<CategorySummaryView>();
                var summaries = models.Select(m => new CategorySummaryDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Description,
                    Color = m.Color,
                    Icon = m.Icon,
                    DisplayOrder = m.DisplayOrder,
                    TotalProducts = m.TotalProducts,
                    TotalStock = m.TotalStock,
                    LowStockCount = m.LowStockCount,
                    TotalValue = m.TotalValue,
                    HealthPercent = m.HealthPercent
                }).ToList();

                LogSuccess($"Category summary obtenido: {summaries.Count} categorias");
                return summaries;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo category summary", ex);
                return new List<CategorySummaryDto>();
            }
        }

        public async Task<InventoryCategoryDb> CreateCategory(InventoryCategoryDb category)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryCategoryDb>()
                    .Insert(category);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Categoria creada: {category.Name}");
                    DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear la categoria");
            }
            catch (Exception ex)
            {
                LogError("Error creando categoria", ex);
                throw;
            }
        }

        public async Task<bool> UpdateCategory(InventoryCategoryDb category)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryCategoryDb>()
                    .Where(c => c.Id == category.Id)
                    .Update(category);

                bool success = response?.Models?.Any() == true;
                if (success)
                {
                    LogSuccess($"Categoria actualizada: {category.Name}");
                    DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                }
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando categoria {category.Id}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteCategory(int categoryId, int userId)
        {
            try
            {
                // Soft delete
                var cat = await SupabaseClient
                    .From<InventoryCategoryDb>()
                    .Where(c => c.Id == categoryId)
                    .Single();

                if (cat == null) return false;

                cat.IsActive = false;
                cat.UpdatedBy = userId;

                var response = await SupabaseClient
                    .From<InventoryCategoryDb>()
                    .Where(c => c.Id == categoryId)
                    .Update(cat);

                bool success = response?.Models?.Any() == true;
                if (success)
                {
                    LogSuccess($"Categoria eliminada (soft): {categoryId}");
                    DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                }
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando categoria {categoryId}", ex);
                return false;
            }
        }

        // ==========================================
        // PRODUCTOS
        // ==========================================

        public async Task<List<InventoryProductDb>> GetProductsByCategory(int categoryId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryProductDb>()
                    .Filter("category_id", Postgrest.Constants.Operator.Equals, categoryId.ToString())
                    .Filter("is_active", Postgrest.Constants.Operator.Equals, "true")
                    .Order("code", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var list = response?.Models ?? new List<InventoryProductDb>();
                LogSuccess($"Productos obtenidos para categoria {categoryId}: {list.Count}");
                return list;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo productos de categoria {categoryId}", ex);
                return new List<InventoryProductDb>();
            }
        }

        public async Task<InventoryProductDb> CreateProduct(InventoryProductDb product)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryProductDb>()
                    .Insert(product);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Producto creado: {product.Code} - {product.Name}");
                    DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el producto");
            }
            catch (Exception ex)
            {
                LogError("Error creando producto", ex);
                throw;
            }
        }

        public async Task<bool> UpdateProduct(InventoryProductDb product)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryProductDb>()
                    .Where(p => p.Id == product.Id)
                    .Update(product);

                bool success = response?.Models?.Any() == true;
                if (success)
                {
                    LogSuccess($"Producto actualizado: {product.Code}");
                    DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                }
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando producto {product.Id}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteProduct(int productId, int userId)
        {
            try
            {
                // Soft delete
                var prod = await SupabaseClient
                    .From<InventoryProductDb>()
                    .Where(p => p.Id == productId)
                    .Single();

                if (prod == null) return false;

                prod.IsActive = false;
                prod.UpdatedBy = userId;

                var response = await SupabaseClient
                    .From<InventoryProductDb>()
                    .Where(p => p.Id == productId)
                    .Update(prod);

                bool success = response?.Models?.Any() == true;
                if (success)
                {
                    LogSuccess($"Producto eliminado (soft): {productId}");
                    DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                }
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando producto {productId}", ex);
                return false;
            }
        }

        // ==========================================
        // RPCs
        // ==========================================

        /// <summary>
        /// Ajuste seguro de stock via fn_adjust_stock (transaccional, con validacion server-side)
        /// </summary>
        public async Task<StockAdjustResult> AdjustStock(int productId, decimal newStock, int userId, string notes = null)
        {
            try
            {
                var response = await SupabaseClient.Rpc<List<StockAdjustResult>>(
                    "fn_adjust_stock",
                    new Dictionary<string, object>
                    {
                        { "p_product_id", productId },
                        { "p_new_stock", newStock },
                        { "p_user_id", userId },
                        { "p_notes", notes ?? "" }
                    });

                if (response?.Count > 0)
                {
                    var result = response[0];
                    if (result.Success)
                    {
                        LogSuccess($"Stock ajustado: producto {productId}, {result.PreviousStock} -> {result.NewStock}");
                        DataChangedEvent.Publish(DataChangedEvent.Topics.Inventory);
                    }
                    return result;
                }

                return new StockAdjustResult { Success = false, Error = "Sin respuesta del servidor" };
            }
            catch (Exception ex)
            {
                LogError($"Error ajustando stock de producto {productId}", ex);
                return new StockAdjustResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Stats globales para KPIs via fn_get_inventory_stats
        /// </summary>
        public async Task<InventoryStatsDto> GetStats()
        {
            try
            {
                var response = await SupabaseClient.Rpc<List<InventoryStatsDto>>(
                    "fn_get_inventory_stats",
                    new Dictionary<string, object>());

                if (response?.Count > 0)
                {
                    LogSuccess("Inventory stats obtenidos");
                    return response[0];
                }

                return new InventoryStatsDto();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo inventory stats", ex);
                return new InventoryStatsDto();
            }
        }

        /// <summary>
        /// Ubicaciones distintas para filtro dinamico via fn_get_inventory_locations
        /// </summary>
        public async Task<List<string>> GetLocations(int? categoryId = null)
        {
            try
            {
                var parameters = new Dictionary<string, object>();
                if (categoryId.HasValue)
                    parameters["p_category_id"] = categoryId.Value;
                else
                    parameters["p_category_id"] = null;

                var response = await SupabaseClient.Rpc<List<string>>(
                    "fn_get_inventory_locations", parameters);

                return response ?? new List<string>();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo locations", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// Movimientos de un producto (historial)
        /// </summary>
        public async Task<List<InventoryMovementDb>> GetMovements(int productId, int limit = 50)
        {
            try
            {
                var response = await SupabaseClient
                    .From<InventoryMovementDb>()
                    .Filter("product_id", Postgrest.Constants.Operator.Equals, productId.ToString())
                    .Order("created_at", Postgrest.Constants.Ordering.Descending)
                    .Range(0, limit - 1)
                    .Get();

                return response?.Models ?? new List<InventoryMovementDb>();
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo movimientos de producto {productId}", ex);
                return new List<InventoryMovementDb>();
            }
        }
    }
}
