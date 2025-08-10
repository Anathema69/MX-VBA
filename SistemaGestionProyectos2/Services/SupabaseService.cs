using Supabase;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using System.IO;


namespace SistemaGestionProyectos2.Services
{
    public class SupabaseService
    {
        private static SupabaseService _instance;
        private Client _supabaseClient;
        private IConfiguration _configuration;

        public static SupabaseService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SupabaseService();
                return _instance;
            }
        }

        private SupabaseService()
        {
            LoadConfiguration();
            InitializeClient();
        }

        private IConfiguration Get_configuration()
        {
            return _configuration;
        }

        private void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Cambiar esta línea
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        private void InitializeClient()
        {
            var url = _configuration["Supabase:Url"];
            var key = _configuration["Supabase:AnonKey"];

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            _supabaseClient = new Client(url, key, options);
        }

        public Client GetClient()
        {
            return _supabaseClient;
        }

        // Método de autenticación
        public async Task<bool> SignIn(string email, string password)
        {
            try
            {
                var session = await _supabaseClient.Auth.SignIn(email, password);
                return session != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al iniciar sesión: {ex.Message}");
                return false;
            }
        }

        // Método para cerrar sesión
        public async Task SignOut()
        {
            await _supabaseClient.Auth.SignOut();
        }

        // Verificar si hay una sesión activa
        public bool IsAuthenticated()
        {
            return _supabaseClient.Auth.CurrentSession != null;
        }

        // Obtener el usuario actual
        public Supabase.Gotrue.User GetCurrentUser()
        {
            return _supabaseClient.Auth.CurrentUser;
        }
        public IConfiguration GetConfiguration()
        {
            return _configuration;
        }
    }
}