// Services/SessionTimeoutService.cs - Gestión de timeout de sesión por inactividad

using System;
using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;

namespace SistemaGestionProyectos2.Services
{
    public class SessionTimeoutService
    {
        private static SessionTimeoutService _instance;
        private static readonly object _lock = new object();

        private DispatcherTimer _inactivityTimer;
        private DateTime _lastActivity;
        private SessionTimeoutConfig _config;
        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _warningShown = false;

        private readonly JsonLoggerService _logger;

        // Eventos públicos
        public event EventHandler OnWarning;
        public event EventHandler OnTimeout;
        public event EventHandler<int> OnTimerTick; // Para actualizar UI con segundos restantes

        // Singleton
        public static SessionTimeoutService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SessionTimeoutService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SessionTimeoutService()
        {
            _logger = JsonLoggerService.Instance;
            _config = LoadConfiguration();

            // Crear timer que verifica cada 1 segundo
            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _inactivityTimer.Tick += CheckInactivity;

            System.Diagnostics.Debug.WriteLine($"🔐 SessionTimeoutService inicializado");
            System.Diagnostics.Debug.WriteLine($"   ⏱️  Timeout: {_config.InactivityMinutes} minutos");
            System.Diagnostics.Debug.WriteLine($"   ⚠️  Advertencia: {_config.WarningBeforeMinutes} minutos antes");
            System.Diagnostics.Debug.WriteLine($"   ✅ Habilitado: {_config.Enabled}");
        }

        private SessionTimeoutConfig LoadConfiguration()
        {
            try
            {
                // Usar el directorio base de la aplicación (donde está el .exe)
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var configPath = Path.Combine(basePath, "appsettings.json");

                System.Diagnostics.Debug.WriteLine($"📁 Cargando configuración desde: {configPath}");
                System.Diagnostics.Debug.WriteLine($"   Archivo existe: {File.Exists(configPath)}");

                // Leer el archivo JSON directamente para verificar
                string jsonContent = "";
                if (File.Exists(configPath))
                {
                    jsonContent = File.ReadAllText(configPath);
                    System.Diagnostics.Debug.WriteLine($"📄 Contenido del archivo (primeras 500 chars):");
                    System.Diagnostics.Debug.WriteLine(jsonContent.Substring(0, Math.Min(500, jsonContent.Length)));
                }

                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

                var configuration = configBuilder.Build();
                var timeoutSection = configuration.GetSection("SessionTimeout");

                // Leer valores individuales
                var enabledValue = timeoutSection["Enabled"];
                var inactivityValue = timeoutSection["InactivityMinutes"];
                var warningValue = timeoutSection["WarningBeforeMinutes"];

                System.Diagnostics.Debug.WriteLine($"🔍 Valores leídos del archivo:");
                System.Diagnostics.Debug.WriteLine($"   Enabled (raw): '{enabledValue}'");
                System.Diagnostics.Debug.WriteLine($"   InactivityMinutes (raw): '{inactivityValue}'");
                System.Diagnostics.Debug.WriteLine($"   WarningBeforeMinutes (raw): '{warningValue}'");

                var config = new SessionTimeoutConfig
                {
                    Enabled = timeoutSection.GetValue<bool>("Enabled", true),
                    InactivityMinutes = timeoutSection.GetValue<double>("InactivityMinutes", 15),
                    WarningBeforeMinutes = timeoutSection.GetValue<double>("WarningBeforeMinutes", 2)
                };

                System.Diagnostics.Debug.WriteLine($"✅ Configuración parseada:");
                System.Diagnostics.Debug.WriteLine($"   InactivityMinutes: {config.InactivityMinutes}");
                System.Diagnostics.Debug.WriteLine($"   WarningBeforeMinutes: {config.WarningBeforeMinutes}");
                System.Diagnostics.Debug.WriteLine($"   Enabled: {config.Enabled}");

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando configuración: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"⚠️ Usando valores por defecto: 15 min inactividad, 2 min advertencia");

                // Configuración por defecto
                return new SessionTimeoutConfig
                {
                    Enabled = true,
                    InactivityMinutes = 15.0,
                    WarningBeforeMinutes = 2.0
                };
            }
        }

        public void Start()
        {
            System.Diagnostics.Debug.WriteLine($"🔍 SessionTimeoutService.Start() llamado");
            System.Diagnostics.Debug.WriteLine($"   Enabled: {_config.Enabled}");
            System.Diagnostics.Debug.WriteLine($"   InactivityMinutes: {_config.InactivityMinutes}");
            System.Diagnostics.Debug.WriteLine($"   WarningBeforeMinutes: {_config.WarningBeforeMinutes}");

            if (!_config.Enabled)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ SessionTimeout deshabilitado en configuración");
                return;
            }

            _lastActivity = DateTime.Now;
            _isRunning = true;
            _isPaused = false;
            _warningShown = false;
            _inactivityTimer.Start();

            _logger.LogInfo("SESSION", "TIMEOUT_MONITORING_STARTED", new
            {
                inactivityMinutes = _config.InactivityMinutes,
                warningMinutes = _config.WarningBeforeMinutes
            });

            System.Diagnostics.Debug.WriteLine($"✅ Monitoreo de inactividad iniciado ({_config.InactivityMinutes} min)");
            System.Diagnostics.Debug.WriteLine($"   Timer IsEnabled: {_inactivityTimer.IsEnabled}");
            System.Diagnostics.Debug.WriteLine($"   Timer Interval: {_inactivityTimer.Interval}");
        }

        public void Stop()
        {
            _isRunning = false;
            _inactivityTimer.Stop();

            _logger.LogInfo("SESSION", "TIMEOUT_MONITORING_STOPPED", null);
            System.Diagnostics.Debug.WriteLine("⏹️ Monitoreo de inactividad detenido");
        }

        public void ResetTimer()
        {
            if (!_isRunning || _isPaused)
                return;

            var oldActivity = _lastActivity;
            _lastActivity = DateTime.Now;
            _warningShown = false;

            // Solo log si han pasado más de 30 segundos desde la última actividad
            var inactiveSeconds = (DateTime.Now - oldActivity).TotalSeconds;
            if (inactiveSeconds > 30)
            {
                _logger.LogDebug("SESSION", "ACTIVITY_DETECTED", new
                {
                    inactiveSeconds = (int)inactiveSeconds
                });
            }
        }

        public void PauseMonitoring()
        {
            if (!_isRunning)
                return;

            _isPaused = true;
            _logger.LogDebug("SESSION", "TIMEOUT_MONITORING_PAUSED", null);
            System.Diagnostics.Debug.WriteLine("⏸️ Monitoreo pausado (diálogo modal activo)");
        }

        public void ResumeMonitoring()
        {
            if (!_isRunning || !_isPaused)
                return;

            _isPaused = false;
            _lastActivity = DateTime.Now; // Resetear al reanudar
            _warningShown = false;

            _logger.LogDebug("SESSION", "TIMEOUT_MONITORING_RESUMED", null);
            System.Diagnostics.Debug.WriteLine("▶️ Monitoreo reanudado");
        }

        private void CheckInactivity(object sender, EventArgs e)
        {
            if (!_isRunning || _isPaused)
                return;

            var inactiveTime = DateTime.Now - _lastActivity;
            var inactiveMinutes = inactiveTime.TotalMinutes;
            var inactiveSeconds = inactiveTime.TotalSeconds;
            var remainingSeconds = (int)((_config.InactivityMinutes * 60) - inactiveSeconds);

            // Debug cada 10 segundos
            if ((int)inactiveSeconds % 10 == 0)
            {
                System.Diagnostics.Debug.WriteLine($"⏱️ Inactividad: {inactiveSeconds:F0}s / {_config.InactivityMinutes * 60}s (Restante: {remainingSeconds}s)");
            }

            // Notificar a suscriptores (para UI)
            OnTimerTick?.Invoke(this, remainingSeconds);

            // Tiempo para mostrar advertencia
            var warningThresholdMinutes = _config.InactivityMinutes - _config.WarningBeforeMinutes;

            // Mostrar advertencia
            if (!_warningShown && inactiveMinutes >= warningThresholdMinutes)
            {
                _warningShown = true;

                _logger.LogWarning("SESSION", "INACTIVITY_WARNING", new
                {
                    inactiveMinutes = (int)inactiveMinutes,
                    remainingMinutes = _config.WarningBeforeMinutes
                });

                System.Diagnostics.Debug.WriteLine($"⚠️ ADVERTENCIA: Sesión cerrará en {_config.WarningBeforeMinutes} minutos por inactividad");

                OnWarning?.Invoke(this, EventArgs.Empty);
            }

            // Timeout alcanzado
            if (inactiveMinutes >= _config.InactivityMinutes)
            {
                System.Diagnostics.Debug.WriteLine("🔒🔒🔒 ========================================");
                System.Diagnostics.Debug.WriteLine($"🔒 TIMEOUT ALCANZADO - Inactividad: {inactiveMinutes:F1} minutos");
                System.Diagnostics.Debug.WriteLine($"🔒 Umbral configurado: {_config.InactivityMinutes} minutos");
                System.Diagnostics.Debug.WriteLine("🔒🔒🔒 ========================================");

                _logger.LogWarning("SESSION", "TIMEOUT_TRIGGERED", new
                {
                    inactiveMinutes = (int)inactiveMinutes,
                    timeoutMinutes = _config.InactivityMinutes
                });

                System.Diagnostics.Debug.WriteLine("🔒 Deteniendo timer de inactividad...");
                Stop();

                // Verificar si hay suscriptores al evento
                if (OnTimeout != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🔒 Hay {OnTimeout.GetInvocationList().Length} suscriptor(es) al evento OnTimeout");
                    System.Diagnostics.Debug.WriteLine("🔒 Invocando evento OnTimeout...");
                    OnTimeout.Invoke(this, EventArgs.Empty);
                    System.Diagnostics.Debug.WriteLine("🔒 Evento OnTimeout invocado exitosamente");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌❌❌ ERROR: No hay suscriptores al evento OnTimeout!");
                }
            }
        }

        // Propiedades públicas
        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public double InactivityMinutes => _config.InactivityMinutes;
        public double WarningBeforeMinutes => _config.WarningBeforeMinutes;
        public TimeSpan TimeSinceLastActivity => DateTime.Now - _lastActivity;

        public SessionTimeoutConfig GetConfig()
        {
            return _config;
        }
    }

    // Modelo de configuración
    public class SessionTimeoutConfig
    {
        public bool Enabled { get; set; }
        public double InactivityMinutes { get; set; }
        public double WarningBeforeMinutes { get; set; }
    }
}
