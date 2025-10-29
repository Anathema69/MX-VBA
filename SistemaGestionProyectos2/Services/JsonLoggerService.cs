// Services/JsonLoggerService.cs - Sistema profesional de logging con JSONL

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SistemaGestionProyectos2.Services
{
    public class JsonLoggerService
    {
        private static JsonLoggerService _instance;
        private static readonly object _lock = new object();

        private string _sessionFolder;
        private string _sessionId;
        private DateTime _sessionStart;
        private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly LoggingConfig _config;

        private string _eventsFilePath;
        private string _errorsFilePath;
        private string _metadataFilePath;
        private string _summaryFilePath;

        private int _totalEvents = 0;
        private int _errorCount = 0;
        private int _warningCount = 0;

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
                WriteIndented = false, // JSONL no usa indentación
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Cargar configuración
            _config = LoadConfiguration();

            if (_config.Enabled)
            {
                InitializeSession();
                CleanupOldLogs();
            }
        }

        private LoggingConfig LoadConfiguration()
        {
            try
            {
                // Usar el directorio base de la aplicación (donde está el .exe en Program Files o donde esté instalada)
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

                var configuration = configBuilder.Build();
                var loggingSection = configuration.GetSection("Logging");

                return new LoggingConfig
                {
                    Enabled = loggingSection.GetValue<bool>("Enabled", true),
                    LogLevel = Enum.Parse<LogLevel>(loggingSection.GetValue<string>("LogLevel", "Info"), true),
                    RetentionDays = loggingSection.GetValue<int>("RetentionDays", 30),
                    Format = loggingSection.GetValue<string>("Format", "JSONL"),
                    IncludeStackTrace = loggingSection.GetValue<bool>("IncludeStackTrace", true),
                    CompressOldLogs = loggingSection.GetValue<bool>("CompressOldLogs", false),
                    SeparateErrorLog = loggingSection.GetValue<bool>("SeparateErrorLog", true)
                };
            }
            catch
            {
                // Si falla, usar configuración por defecto
                return new LoggingConfig
                {
                    Enabled = true,
                    LogLevel = LogLevel.Info,
                    RetentionDays = 30,
                    Format = "JSONL",
                    IncludeStackTrace = true,
                    CompressOldLogs = false,
                    SeparateErrorLog = true
                };
            }
        }

        private void InitializeSession()
        {
            _sessionStart = DateTime.Now;
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

            // Usar AppData para logs (tiene permisos de escritura incluso si la app está en Program Files)
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appLogsPath = Path.Combine(appDataPath, "SistemaGestionProyectos", "logs");

            // Crear estructura: %LocalAppData%/SistemaGestionProyectos/logs/sessions/2025-01-13_193500_abc123/
            var sessionFolderName = $"{_sessionStart:yyyy-MM-dd_HHmmss}_{_sessionId}";
            _sessionFolder = Path.Combine(appLogsPath, "sessions", sessionFolderName);

            Directory.CreateDirectory(_sessionFolder);
            Directory.CreateDirectory(Path.Combine(appLogsPath, "daily"));
            Directory.CreateDirectory(Path.Combine(appLogsPath, "errors"));

            // Definir rutas de archivos
            _metadataFilePath = Path.Combine(_sessionFolder, "metadata.json");
            _eventsFilePath = Path.Combine(_sessionFolder, "events.jsonl");
            _errorsFilePath = Path.Combine(_sessionFolder, "errors.jsonl");
            _summaryFilePath = Path.Combine(_sessionFolder, "summary.json");

            // Crear metadata inicial
            var metadata = new SessionMetadata
            {
                SessionId = _sessionId,
                StartTime = _sessionStart,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                LogLevel = _config.LogLevel.ToString(),
                Format = _config.Format
            };

            WriteJsonFile(_metadataFilePath, metadata);

            System.Diagnostics.Debug.WriteLine($"📋 LOG SESSION INICIADA");
            System.Diagnostics.Debug.WriteLine($"   📁 Carpeta: {_sessionFolder}");
            System.Diagnostics.Debug.WriteLine($"   🆔 Session ID: {_sessionId}");
            System.Diagnostics.Debug.WriteLine($"   ⚙️  Log Level: {_config.LogLevel}");
        }

        // Método principal para loggear eventos
        public async Task LogEventAsync(string category, string action, object data = null, string userId = null, LogLevel level = LogLevel.Info)
        {
            if (!_config.Enabled || level < _config.LogLevel)
                return;

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
                    Data = data,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };

                // Escribir a events.jsonl (formato JSONL: una línea por evento)
                var jsonLine = JsonSerializer.Serialize(logEvent, _jsonOptions);
                await File.AppendAllTextAsync(_eventsFilePath, jsonLine + Environment.NewLine);

                _totalEvents++;

                // Si es error o warning, también escribir a errors.jsonl
                if (_config.SeparateErrorLog && (level == LogLevel.Error || level == LogLevel.Critical || level == LogLevel.Warning))
                {
                    await File.AppendAllTextAsync(_errorsFilePath, jsonLine + Environment.NewLine);

                    // También agregar al log de errores diario
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var appLogsPath = Path.Combine(appDataPath, "SistemaGestionProyectos", "logs");
                    var dailyErrorFile = Path.Combine(appLogsPath, "errors", $"{_sessionStart:yyyy-MM-dd}_errors.jsonl");
                    await File.AppendAllTextAsync(dailyErrorFile, jsonLine + Environment.NewLine);

                    if (level == LogLevel.Error || level == LogLevel.Critical)
                        _errorCount++;
                    else
                        _warningCount++;
                }

                // Debug output con colores según nivel
                var emoji = level switch
                {
                    LogLevel.Debug => "🔍",
                    LogLevel.Info => "ℹ️",
                    LogLevel.Warning => "⚠️",
                    LogLevel.Error => "❌",
                    LogLevel.Critical => "🔥",
                    _ => "📝"
                };

                System.Diagnostics.Debug.WriteLine($"{emoji} [{level}] {category}.{action}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en logging: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        // Versión sincrónica
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

        public void LogCritical(string category, string action, object data = null, string userId = null)
        {
            _ = LogEventAsync(category, action, data, userId, LogLevel.Critical);
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

        // Cerrar sesión y generar resúmenes
        public async Task CloseSessionAsync()
        {
            if (!_config.Enabled)
                return;

            await _writeSemaphore.WaitAsync();
            try
            {
                var endTime = DateTime.Now;
                var duration = endTime - _sessionStart;

                // Actualizar metadata
                var metadata = await ReadJsonFileAsync<SessionMetadata>(_metadataFilePath);
                metadata.EndTime = endTime;
                metadata.Duration = duration.ToString(@"hh\:mm\:ss");
                WriteJsonFile(_metadataFilePath, metadata);

                // Generar resumen de sesión
                var summary = new SessionSummary
                {
                    SessionId = _sessionId,
                    StartTime = _sessionStart,
                    EndTime = endTime,
                    Duration = duration.ToString(@"hh\:mm\:ss"),
                    TotalEvents = _totalEvents,
                    ErrorCount = _errorCount,
                    WarningCount = _warningCount,
                    TopCategories = await GetTopCategories(),
                    TopActions = await GetTopActions()
                };

                WriteJsonFile(_summaryFilePath, summary);

                // Actualizar resumen diario
                await UpdateDailySummary();

                System.Diagnostics.Debug.WriteLine($"📋 LOG SESSION CERRADA");
                System.Diagnostics.Debug.WriteLine($"   ⏱️  Duración: {duration:hh\\:mm\\:ss}");
                System.Diagnostics.Debug.WriteLine($"   📊 Eventos: {_totalEvents} ({_errorCount} errores, {_warningCount} warnings)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cerrando sesión de log: {ex.Message}");
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        private async Task<Dictionary<string, int>> GetTopCategories()
        {
            try
            {
                if (!File.Exists(_eventsFilePath))
                    return new Dictionary<string, int>();

                var lines = await File.ReadAllLinesAsync(_eventsFilePath);
                return lines
                    .Select(line => JsonSerializer.Deserialize<LogEvent>(line, _jsonOptions))
                    .Where(e => e != null)
                    .GroupBy(e => e.Category)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch
            {
                return new Dictionary<string, int>();
            }
        }

        private async Task<Dictionary<string, int>> GetTopActions()
        {
            try
            {
                if (!File.Exists(_eventsFilePath))
                    return new Dictionary<string, int>();

                var lines = await File.ReadAllLinesAsync(_eventsFilePath);
                return lines
                    .Select(line => JsonSerializer.Deserialize<LogEvent>(line, _jsonOptions))
                    .Where(e => e != null)
                    .GroupBy(e => e.Action)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
            catch
            {
                return new Dictionary<string, int>();
            }
        }

        private async Task UpdateDailySummary()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appLogsPath = Path.Combine(appDataPath, "SistemaGestionProyectos", "logs");
                var dailySummaryFile = Path.Combine(appLogsPath, "daily", $"{_sessionStart:yyyy-MM-dd}_summary.json");

                DailySummary dailySummary;
                if (File.Exists(dailySummaryFile))
                {
                    dailySummary = await ReadJsonFileAsync<DailySummary>(dailySummaryFile);
                }
                else
                {
                    dailySummary = new DailySummary
                    {
                        Date = _sessionStart.Date,
                        Sessions = new List<SessionInfo>()
                    };
                }

                // Agregar info de esta sesión
                dailySummary.Sessions.Add(new SessionInfo
                {
                    SessionId = _sessionId,
                    StartTime = _sessionStart,
                    EndTime = DateTime.Now,
                    TotalEvents = _totalEvents,
                    ErrorCount = _errorCount,
                    WarningCount = _warningCount,
                    SessionFolder = _sessionFolder
                });

                dailySummary.TotalSessions = dailySummary.Sessions.Count;
                dailySummary.TotalEvents = dailySummary.Sessions.Sum(s => s.TotalEvents);
                dailySummary.TotalErrors = dailySummary.Sessions.Sum(s => s.ErrorCount);
                dailySummary.TotalWarnings = dailySummary.Sessions.Sum(s => s.WarningCount);

                WriteJsonFile(dailySummaryFile, dailySummary);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ No se pudo actualizar resumen diario: {ex.Message}");
            }
        }

        private void CleanupOldLogs()
        {
            if (_config.RetentionDays <= 0)
                return;

            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appLogsPath = Path.Combine(appDataPath, "SistemaGestionProyectos", "logs");
                var logsFolder = Path.Combine(appLogsPath, "sessions");
                if (!Directory.Exists(logsFolder))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-_config.RetentionDays);
                var oldSessionFolders = Directory.GetDirectories(logsFolder)
                    .Where(dir =>
                    {
                        var dirName = Path.GetFileName(dir);
                        // Formato: 2025-01-13_193500_abc123
                        if (DateTime.TryParseExact(dirName.Substring(0, 10), "yyyy-MM-dd", null,
                            System.Globalization.DateTimeStyles.None, out var date))
                        {
                            return date < cutoffDate;
                        }
                        return false;
                    })
                    .ToList();

                foreach (var folder in oldSessionFolders)
                {
                    try
                    {
                        Directory.Delete(folder, recursive: true);
                        System.Diagnostics.Debug.WriteLine($"🗑️ Log antiguo eliminado: {Path.GetFileName(folder)}");
                    }
                    catch
                    {
                        // Ignorar errores al borrar
                    }
                }

                if (oldSessionFolders.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Limpieza completada: {oldSessionFolders.Count} sesiones antiguas eliminadas");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error en limpieza de logs: {ex.Message}");
            }
        }

        // Helpers
        private void WriteJsonFile<T>(string path, T data)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error escribiendo JSON: {ex.Message}");
            }
        }

        private async Task<T> ReadJsonFileAsync<T>(string path) where T : new()
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        // Obtener ruta de la carpeta de sesión actual
        public string GetCurrentSessionFolder()
        {
            return _sessionFolder;
        }

        public string GetCurrentLogPath()
        {
            return _eventsFilePath;
        }

        public SessionStats GetCurrentStats()
        {
            return new SessionStats
            {
                SessionId = _sessionId,
                StartTime = _sessionStart,
                TotalEvents = _totalEvents,
                ErrorCount = _errorCount,
                WarningCount = _warningCount,
                SessionFolder = _sessionFolder
            };
        }
    }

    // ==================== MODELOS ====================

    public class LoggingConfig
    {
        public bool Enabled { get; set; }
        public LogLevel LogLevel { get; set; }
        public int RetentionDays { get; set; }
        public string Format { get; set; }
        public bool IncludeStackTrace { get; set; }
        public bool CompressOldLogs { get; set; }
        public bool SeparateErrorLog { get; set; }
    }

    public class SessionMetadata
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Duration { get; set; }
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public string AppVersion { get; set; }
        public string LogLevel { get; set; }
        public string Format { get; set; }
    }

    public class LogEvent
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public string Action { get; set; }
        public string Level { get; set; }
        public string UserId { get; set; }
        public object Data { get; set; }
        public int ThreadId { get; set; }
    }

    public class SessionSummary
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Duration { get; set; }
        public int TotalEvents { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public Dictionary<string, int> TopCategories { get; set; }
        public Dictionary<string, int> TopActions { get; set; }
    }

    public class DailySummary
    {
        public DateTime Date { get; set; }
        public int TotalSessions { get; set; }
        public int TotalEvents { get; set; }
        public int TotalErrors { get; set; }
        public int TotalWarnings { get; set; }
        public List<SessionInfo> Sessions { get; set; }
    }

    public class SessionInfo
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalEvents { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public string SessionFolder { get; set; }
    }

    public class SessionStats
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public int TotalEvents { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public string SessionFolder { get; set; }
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }
}
