// Crear nuevo archivo: Services/JsonLoggerService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services
{
    public class JsonLoggerService
    {
        private static JsonLoggerService _instance;
        private static readonly object _lock = new object();

        private string _logFilePath;
        private string _sessionId;
        private DateTime _sessionStart;
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;

        // Singleton
        public static JsonLoggerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new JsonLoggerService();
                        }
                    }
                }
                return _instance;
            }
        }

        private JsonLoggerService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            InitializeSession();
        }

        private void InitializeSession()
        {
            _sessionStart = DateTime.Now;
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

            // Crear estructura de carpetas: logs/2025/08/13/
            var dateFolder = Path.Combine(
                "logs",
                _sessionStart.Year.ToString(),
                _sessionStart.Month.ToString("00"),
                _sessionStart.Day.ToString("00")
            );

            Directory.CreateDirectory(dateFolder);

            // Nombre del archivo: session_20250813_193500_abc123.json
            var fileName = $"session_{_sessionStart:yyyyMMdd_HHmmss}_{_sessionId}.json";
            _logFilePath = Path.Combine(dateFolder, fileName);

            // Escribir encabezado del log
            var header = new LogSession
            {
                SessionId = _sessionId,
                StartTime = _sessionStart,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                Events = new List<LogEvent>()
            };

            WriteToFile(JsonSerializer.Serialize(header, _jsonOptions));

            System.Diagnostics.Debug.WriteLine($"LOG: Session iniciada - {_logFilePath}");
        }

        // Método principal para loggear eventos
        public async Task LogEventAsync(string category, string action, object data = null, string userId = null, LogLevel level = LogLevel.Info)
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                var logEvent = new LogEvent
                {
                    Timestamp = DateTime.Now,
                    Category = category,
                    Action = action,
                    Level = level.ToString(),
                    UserId = userId,
                    Data = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };

                // Leer el archivo existente
                var jsonContent = File.ReadAllText(_logFilePath);
                var session = JsonSerializer.Deserialize<LogSession>(jsonContent, _jsonOptions);

                if (session.Events == null)
                    session.Events = new List<LogEvent>();

                session.Events.Add(logEvent);

                // Escribir de vuelta
                WriteToFile(JsonSerializer.Serialize(session, _jsonOptions));

                // También escribir a Debug para desarrollo
                System.Diagnostics.Debug.WriteLine($"[{level}] {category}.{action}: {data}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en logging: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        // Versión sincrónica para casos críticos
        public void LogEvent(string category, string action, object data = null, string userId = null, LogLevel level = LogLevel.Info)
        {
            Task.Run(async () => await LogEventAsync(category, action, data, userId, level)).Wait();
        }

        // Métodos de conveniencia
        public void LogInfo(string category, string action, object data = null, string userId = null)
        {
            _ = LogEventAsync(category, action, data, userId, LogLevel.Info);
        }

        public void LogWarning(string category, string action, object data = null, string userId = null)
        {
            _ = LogEventAsync(category, action, data, userId, LogLevel.Warning);
        }

        public void LogError(string category, string action, object data = null, string userId = null)
        {
            _ = LogEventAsync(category, action, data, userId, LogLevel.Error);
        }

        public void LogDebug(string category, string action, object data = null, string userId = null)
        {
            _ = LogEventAsync(category, action, data, userId, LogLevel.Debug);
        }

        // Log de login
        public void LogLogin(string username, bool success, string userId = null, string role = null)
        {
            _ = LogEventAsync("AUTH", success ? "LOGIN_SUCCESS" : "LOGIN_FAILED", new
            {
                username,
                role,
                success,
                timestamp = DateTime.Now
            }, userId, success ? LogLevel.Info : LogLevel.Warning);
        }

        // Log de operaciones CRUD
        public void LogCrud(string entity, string operation, object entityData, string userId = null, bool success = true)
        {
            _ = LogEventAsync("CRUD", $"{entity.ToUpper()}_{operation.ToUpper()}", new
            {
                entity,
                operation,
                success,
                data = entityData
            }, userId, success ? LogLevel.Info : LogLevel.Error);
        }

        // Cerrar sesión
        public async Task CloseSessionAsync()
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                var jsonContent = File.ReadAllText(_logFilePath);
                var session = JsonSerializer.Deserialize<LogSession>(jsonContent, _jsonOptions);

                session.EndTime = DateTime.Now;
                session.Duration = session.EndTime.Value - session.StartTime;

                WriteToFile(JsonSerializer.Serialize(session, _jsonOptions));

                System.Diagnostics.Debug.WriteLine($"LOG: Session cerrada - Duración: {session.Duration}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cerrando log: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private void WriteToFile(string content)
        {
            try
            {
                File.WriteAllText(_logFilePath, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error escribiendo log: {ex.Message}");
            }
        }

        // Obtener ruta del log actual
        public string GetCurrentLogPath()
        {
            return _logFilePath;
        }
    }

    // Modelos para el log
    public class LogSession
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string AppVersion { get; set; }
        public List<LogEvent> Events { get; set; }
    }

    public class LogEvent
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public string Action { get; set; }
        public string Level { get; set; }
        public string UserId { get; set; }
        public string Data { get; set; }
        public int ThreadId { get; set; }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}