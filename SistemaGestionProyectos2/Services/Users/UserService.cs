using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Users
{
    public class UserService : BaseSupabaseService
    {
        public UserService(Client supabaseClient) : base(supabaseClient) { }

        /// <summary>
        /// Autentica un usuario con username y contraseña
        /// </summary>
        public async Task<(bool Success, UserDb User, string Message)> AuthenticateUser(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return (false, null, "Usuario y contraseña son requeridos");
                }

                LogDebug($"Intentando autenticar usuario: {username}");

                // Buscar usuario por username
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(x => x.Username == username)
                    .Single();

                if (response == null)
                {
                    LogDebug($"Usuario no encontrado: {username}");
                    return (false, null, "Usuario no encontrado");
                }

                // Verificar contraseña con BCrypt
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, response.PasswordHash);

                if (!isPasswordValid)
                {
                    LogDebug($"Contraseña incorrecta para usuario: {username}");
                    return (false, null, "Contraseña incorrecta");
                }

                // Verificar si el usuario está activo
                if (!response.IsActive)
                {
                    LogDebug($"Usuario desactivado: {username}");
                    return (false, null, "Usuario desactivado");
                }

                // Actualizar último login
                await SupabaseClient
                    .From<UserDb>()
                    .Where(x => x.Id == response.Id)
                    .Set(x => x.LastLogin, DateTime.UtcNow)
                    .Update();

                LogSuccess($"Login exitoso: {username} ({response.Role})");
                return (true, response, "Login exitoso");
            }
            catch (Exception ex)
            {
                LogError($"Error en autenticación para usuario: {username}", ex);
                return (false, null, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene un usuario por username
        /// </summary>
        public async Task<UserDb> GetUserByUsername(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username)) return null;

                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(x => x.Username == username)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo usuario por username: {username}", ex);
                return null;
            }
        }

        /// <summary>
        /// Obtiene un usuario por ID
        /// </summary>
        public async Task<UserDb> GetUserById(int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(x => x.Id == userId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo usuario {userId}", ex);
                return null;
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios activos
        /// </summary>
        public async Task<List<UserDb>> GetActiveUsers()
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.IsActive == true)
                    .Order("username", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var users = response?.Models ?? new List<UserDb>();
                LogSuccess($"Usuarios activos obtenidos: {users.Count}");
                return users;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo usuarios activos", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios (activos e inactivos)
        /// </summary>
        public async Task<List<UserDb>> GetAllUsers()
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Order("username", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var users = response?.Models ?? new List<UserDb>();
                LogSuccess($"Todos los usuarios obtenidos: {users.Count}");
                return users;
            }
            catch (Exception ex)
            {
                LogError("Error obteniendo todos los usuarios", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtiene usuarios por rol
        /// </summary>
        public async Task<List<UserDb>> GetUsersByRole(string role)
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Role == role)
                    .Where(u => u.IsActive == true)
                    .Order("username", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                var users = response?.Models ?? new List<UserDb>();
                LogSuccess($"Usuarios obtenidos por rol '{role}': {users.Count}");
                return users;
            }
            catch (Exception ex)
            {
                LogError($"Error obteniendo usuarios por rol '{role}'", ex);
                throw;
            }
        }

        /// <summary>
        /// Crea un nuevo usuario
        /// </summary>
        public async Task<UserDb> CreateUser(UserDb user, string plainPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plainPassword))
                {
                    throw new Exception("La contraseña no puede estar vacía");
                }

                // Hash de la contraseña con BCrypt
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
                user.IsActive = true;

                LogDebug($"Creando usuario: {user.Username}");

                var response = await SupabaseClient
                    .From<UserDb>()
                    .Insert(user);

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Usuario creado: {user.Username} ({user.Role})");
                    return response.Models.First();
                }

                throw new Exception("No se pudo crear el usuario");
            }
            catch (Exception ex)
            {
                LogError("Error creando usuario", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza un usuario existente
        /// </summary>
        public async Task<UserDb> UpdateUser(UserDb user)
        {
            try
            {
                LogDebug($"Actualizando usuario: {user.Username}");

                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == user.Id)
                    .Set(u => u.Username, user.Username)
                    .Set(u => u.Email, user.Email)
                    .Set(u => u.FullName, user.FullName)
                    .Set(u => u.Role, user.Role)
                    .Set(u => u.IsActive, user.IsActive)
                    .Update();

                if (response?.Models?.Count > 0)
                {
                    LogSuccess($"Usuario actualizado: {user.Username}");
                    return response.Models.First();
                }

                return null;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando usuario {user.Id}", ex);
                throw;
            }
        }

        /// <summary>
        /// Cambia la contraseña de un usuario
        /// </summary>
        public async Task<bool> ChangePassword(int userId, string newPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    throw new Exception("La nueva contraseña no puede estar vacía");
                }

                // Hash de la nueva contraseña
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == userId)
                    .Set(u => u.PasswordHash, hashedPassword)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Contraseña cambiada para usuario ID: {userId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error cambiando contraseña para usuario {userId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Desactiva un usuario (soft delete)
        /// </summary>
        public async Task<bool> DeactivateUser(int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == userId)
                    .Set(u => u.IsActive, false)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Usuario desactivado: {userId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error desactivando usuario {userId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Reactiva un usuario previamente desactivado
        /// </summary>
        public async Task<bool> ReactivateUser(int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == userId)
                    .Set(u => u.IsActive, true)
                    .Update();

                bool success = response?.Models?.Any() == true;
                if (success) LogSuccess($"Usuario reactivado: {userId}");
                return success;
            }
            catch (Exception ex)
            {
                LogError($"Error reactivando usuario {userId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Elimina un usuario permanentemente (hard delete)
        /// </summary>
        public async Task<bool> DeleteUser(int userId)
        {
            try
            {
                await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == userId)
                    .Delete();

                LogSuccess($"Usuario eliminado permanentemente: {userId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error eliminando usuario {userId}", ex);
                return false;
            }
        }

        /// <summary>
        /// Verifica si existe un usuario con el mismo username
        /// </summary>
        public async Task<bool> UserExists(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username)) return false;

                var response = await SupabaseClient
                    .From<UserDb>()
                    .Filter("username", Postgrest.Constants.Operator.ILike, username.Trim())
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de usuario", ex);
                return false;
            }
        }

        /// <summary>
        /// Verifica si existe un email registrado
        /// </summary>
        public async Task<bool> EmailExists(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return false;

                var response = await SupabaseClient
                    .From<UserDb>()
                    .Filter("email", Postgrest.Constants.Operator.ILike, email.Trim())
                    .Get();

                return response?.Models?.Count > 0;
            }
            catch (Exception ex)
            {
                LogError("Error verificando existencia de email", ex);
                return false;
            }
        }

        /// <summary>
        /// Obtiene estadísticas de usuarios
        /// </summary>
        public async Task<Dictionary<string, object>> GetUserStats()
        {
            try
            {
                var allUsers = await GetAllUsers();
                var activeUsers = allUsers.Where(u => u.IsActive).ToList();

                var roleGroups = activeUsers.GroupBy(u => u.Role ?? "Sin Rol")
                    .ToDictionary(g => g.Key, g => g.Count());

                var stats = new Dictionary<string, object>
                {
                    ["TotalUsuarios"] = allUsers.Count,
                    ["UsuariosActivos"] = activeUsers.Count,
                    ["UsuariosInactivos"] = allUsers.Count - activeUsers.Count,
                    ["UsuariosPorRol"] = roleGroups,
                    ["UltimoLogin"] = activeUsers
                        .Where(u => u.LastLogin.HasValue)
                        .OrderByDescending(u => u.LastLogin)
                        .FirstOrDefault()?.LastLogin
                };

                LogSuccess($"Estadísticas de usuarios calculadas");
                return stats;
            }
            catch (Exception ex)
            {
                LogError("Error calculando estadísticas de usuarios", ex);
                throw;
            }
        }

        /// <summary>
        /// Actualiza el último login de un usuario
        /// </summary>
        public async Task<bool> UpdateLastLogin(int userId)
        {
            try
            {
                var response = await SupabaseClient
                    .From<UserDb>()
                    .Where(u => u.Id == userId)
                    .Set(u => u.LastLogin, DateTime.UtcNow)
                    .Update();

                return response?.Models?.Any() == true;
            }
            catch (Exception ex)
            {
                LogError($"Error actualizando último login para usuario {userId}", ex);
                return false;
            }
        }
    }
}
