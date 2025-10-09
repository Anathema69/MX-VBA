using Postgrest.Responses;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Contacts
{
    public class ContactService : BaseSupabaseService
    {
        public ContactService(Client supabaseClient) : base(supabaseClient) { }

        public async Task<List<ContactDb>> GetContactsByClient(int clientId)
        {
            try
            {
                LogDebug($"Buscando contactos para cliente ID: {clientId}");

                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Where(x => x.ClientId == clientId)
                    .Where(x => x.IsActive == true)
                    .Get();

                var contacts = response?.Models ?? new List<ContactDb>();
                LogSuccess($"Encontrados {contacts.Count} contactos");

                return contacts;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo contactos del cliente {clientId}", ex);
                return new List<ContactDb>();
            }
        }

        public async Task<List<ContactDb>> GetActiveContactsByClientId(int clientId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Where(c => c.ClientId == clientId)
                    .Where(c => c.IsActive == true)
                    .Order("is_primary", Postgrest.Constants.Ordering.Descending)
                    .Order("f_contactname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ContactDb>();
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo contactos activos del cliente {clientId}", ex);
                return new List<ContactDb>();
            }
        }

        public async Task<List<ContactDb>> GetAllContacts()
        {
            try
            {
                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Where(c => c.IsActive == true)
                    .Order("f_contactname", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return response?.Models ?? new List<ContactDb>();
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo todos los contactos", ex);
                return new List<ContactDb>();
            }
        }

        public async Task<ContactDb> AddContact(ContactDb contact)
        {
            try
            {
                contact.IsActive = true;
                LogDebug($"Creando contacto: {contact.ContactName}");

                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Insert(contact);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Contacto creado: {contact.ContactName}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el contacto");
            }
            catch (Exception ex)
            {
                LogError("Error creando contacto", ex);
                throw;
            }
        }

        public async Task<ContactDb> CreateContact(ContactDb contact)
        {
            try
            {
                contact.IsActive = true;
                LogDebug($"Creando contacto: {contact.ContactName} para cliente {contact.ClientId}");

                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Insert(contact);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Contacto creado: {contact.ContactName}");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el contacto");
            }
            catch (Exception ex)
            {
                LogError("Error creando contacto", ex);
                throw;
            }
        }

        public async Task<ContactDb> UpdateContact(ContactDb contact)
        {
            try
            {
                // Si el contacto se marca como principal, desmarcar todos los otros contactos del cliente primero
                if (contact.IsPrimary)
                {
                    // Desmarcar todos los contactos del cliente como no principales
                    var allContacts = await GetContactsByClient(contact.ClientId);
                    var otherPrimaryContacts = allContacts.Where(c => c.Id != contact.Id && c.IsPrimary).ToList();

                    foreach (var c in otherPrimaryContacts)
                    {
                        try
                        {
                            await SupabaseClient
                                .From<ContactDb>()
                                .Filter("f_contact", Postgrest.Constants.Operator.Equals, c.Id)
                                .Set(x => x.IsPrimary, false)
                                .Update();
                        }
                        catch (Exception ex)
                        {
                            LogError($"Error desmarcando contacto {c.Id} como principal", ex);
                            // Continuar con los demás contactos
                        }
                    }
                }

                // Actualizar el contacto actual
                // Asegurarse de que todos los campos string tengan valores válidos
                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Filter("f_contact", Postgrest.Constants.Operator.Equals, contact.Id)
                    .Set(c => c.ContactName, contact.ContactName ?? "")
                    .Set(c => c.Position, contact.Position ?? "")
                    .Set(c => c.Email, contact.Email ?? "")
                    .Set(c => c.Phone, contact.Phone ?? "")
                    .Set(c => c.IsPrimary, contact.IsPrimary)
                    .Set(c => c.IsActive, contact.IsActive)
                    .Update();

                var result = response?.Models?.FirstOrDefault();
                if (result != null) LogSuccess($"Contacto actualizado: {contact.ContactName}");
                return result;
            }
            catch (Exception ex)
            {
                LogError("Error actualizando contacto", ex);
                throw;
            }
        }

        public async Task<bool> DeleteContact(int contactId)
        {
            try
            {
                LogDebug($"Desactivando contacto ID: {contactId}");

                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Where(c => c.Id == contactId)
                    .Set(c => c.IsActive, false)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success) LogSuccess($"Contacto desactivado: {contactId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando contacto {contactId}", ex);
                return false;
            }
        }

        public async Task<bool> SoftDeleteContact(int contactId)
        {
            try
            {
                LogDebug($"Desactivando contacto ID: {contactId}");

                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Where(c => c.Id == contactId)
                    .Set(c => c.IsActive, false)
                    .Update();

                bool success = response?.Models?.Count > 0;
                if (success) LogSuccess($"Contacto desactivado: {contactId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando contacto {contactId}", ex);
                return false;
            }
        }

        public async Task<int> CountActiveContactsByClientId(int clientId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<ContactDb>()
                    .Where(c => c.ClientId == clientId)
                    .Where(c => c.IsActive == true)
                    .Get();

                return response?.Models?.Count ?? 0;
            }
            catch (Exception ex)
            {
                LogError($"Error contando contactos del cliente {clientId}", ex);
                return 0;
            }
        }
    }
}
