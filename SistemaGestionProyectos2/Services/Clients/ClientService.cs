using Postgrest.Responses;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Clients
{
    public class ClientService : BaseSupabaseService
    {
        public ClientService(Client supabaseClient) : base(supabaseClient) { }

        public async Task<List<ClientDb>> GetClients()
        {
            try
            {
                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Where(c => c.IsActive == true)
                    .Order("f_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var clients = response?.Models ?? new List<ClientDb>();
                LogSuccess($"Clientes obtenidos: {clients.Count}");
                return clients;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo clientes", ex);
                return new List<ClientDb>();
            }
        }

        public async Task<List<ClientDb>> GetActiveClients()
        {
            try
            {
                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Where(c => c.IsActive == true)
                    .Order("f_name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var clients = response?.Models ?? new List<ClientDb>();
                LogSuccess($"Clientes activos: {clients.Count}");
                return clients;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo clientes activos", ex);
                return new List<ClientDb>();
            }
        }

        public async Task<ClientDb> GetClientById(int clientId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Where(x => x.Id == clientId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo cliente {clientId}", ex);
                return null;
            }
        }

        public async Task<ClientDb> GetClientByName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return null;

                string normalizedName = name.Trim().ToUpper();

                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Filter("f_name", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Single();

                return response;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ClientDb> CreateClient(ClientDb client, int userId = 0)
        {
            try
            {
                var now = DateTime.UtcNow;
                client.CreatedBy = userId > 0 ? userId : 1;
                client.UpdatedBy = userId > 0 ? userId : 1;
                client.CreatedAt = now;
                client.UpdatedAt = now;

                if (client.Credit == 0)
                    client.Credit = 30;

                client.IsActive = true;

                LogDebug($"Creando cliente: {client.Name}");

                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Insert(client);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Cliente creado: {client.Name}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el cliente");
            }
            catch (Exception ex)
            {
                LogError("Error creando cliente", ex);
                throw;
            }
        }

        public async Task<bool> UpdateClient(ClientDb client, int userId)
        {
            try
            {
                LogDebug($"Actualizando cliente: {client.Name}");

                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Where(c => c.Id == client.Id)
                    .Set(c => c.Name, client.Name)
                    .Set(c => c.TaxId, client.TaxId ?? "")
                    .Set(c => c.Phone, client.Phone ?? "")
                    .Set(c => c.Address1, client.Address1 ?? "")
                    .Set(c => c.Credit, client.Credit)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow)
                    .Set(c => c.UpdatedBy, userId)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success) LogSuccess($"Cliente actualizado: {client.Name}");
                return success;
            }
            catch (Exception ex)
            {
                LogError("Error actualizando cliente", ex);
                throw;
            }
        }

        public async Task<bool> SoftDeleteClient(int clientId)
        {
            try
            {
                LogDebug($"Desactivando cliente ID: {clientId}");

                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Where(c => c.Id == clientId)
                    .Set(c => c.IsActive, false)
                    .Set(c => c.UpdatedAt, DateTime.Now)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success) LogSuccess($"Cliente desactivado: {clientId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando cliente {clientId}", ex);
                return false;
            }
        }

        public async Task<bool> ClientExists(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return false;

                string normalizedName = name.Trim().ToUpper();

                var response = await SupabaseClient
                    .From<ClientDb>()
                    .Filter("f_name", Postgrest.Constants.Operator.ILike, normalizedName)
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de cliente", ex);
                return false;
            }
        }
    }
}
