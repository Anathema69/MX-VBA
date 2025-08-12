// Crear archivo: Models/UserSession.cs
namespace SistemaGestionProyectos2.Models
{
    public class UserSession
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public DateTime LoginTime { get; set; }
    }
}