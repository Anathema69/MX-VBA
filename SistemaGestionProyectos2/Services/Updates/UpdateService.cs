using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Updates
{
    /// <summary>
    /// Servicio para gestionar actualizaciones automáticas de la aplicación
    /// </summary>
    public class UpdateService : BaseSupabaseService
    {
        private readonly JsonLoggerService _logger;
        private readonly string _currentVersion;

        public UpdateService(Client supabaseClient, string currentVersion) : base(supabaseClient)
        {
            _logger = JsonLoggerService.Instance;
            _currentVersion = currentVersion;
        }

        /// <summary>
        /// Verifica si hay una actualización disponible
        /// </summary>
        public async Task<(bool Available, AppVersionDb NewVersion, string Message)> CheckForUpdate()
        {
            try
            {
                _logger.LogInfo("UPDATE", "CHECK_START", new
                {
                    currentVersion = _currentVersion
                });

                // Obtener la última versión disponible
                var response = await SupabaseClient
                    .From<AppVersionDb>()
                    .Where(x => x.IsLatest == true && x.IsActive == true)
                    .Limit(1)
                    .Get();

                if (response?.Models == null || !response.Models.Any())
                {
                    _logger.LogWarning("UPDATE", "NO_LATEST_VERSION", new
                    {
                        message = "No se encontró versión latest en la base de datos"
                    });
                    return (false, null, "No se pudo verificar actualizaciones");
                }

                var latestVersion = response.Models.First();

                _logger.LogInfo("UPDATE", "LATEST_VERSION_FOUND", new
                {
                    latestVersion = latestVersion.Version,
                    currentVersion = _currentVersion,
                    isMandatory = latestVersion.IsMandatory
                });

                // Comparar versiones
                if (IsNewerVersion(latestVersion.Version, _currentVersion))
                {
                    _logger.LogInfo("UPDATE", "UPDATE_AVAILABLE", new
                    {
                        from = _currentVersion,
                        to = latestVersion.Version,
                        mandatory = latestVersion.IsMandatory,
                        sizeMb = latestVersion.FileSizeMb
                    });

                    return (true, latestVersion, $"Nueva versión {latestVersion.Version} disponible");
                }
                else
                {
                    _logger.LogInfo("UPDATE", "UP_TO_DATE", new
                    {
                        version = _currentVersion
                    });

                    return (false, null, "Aplicación actualizada");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("UPDATE", "CHECK_ERROR", new
                {
                    error = ex.Message,
                    currentVersion = _currentVersion,
                    stackTrace = ex.StackTrace
                });

                return (false, null, $"Error verificando actualizaciones: {ex.Message}");
            }
        }

        /// <summary>
        /// Descarga el instalador de la actualización
        /// </summary>
        public async Task<(bool Success, string FilePath, string Message)> DownloadUpdate(AppVersionDb version, IProgress<int> progress = null)
        {
            try
            {
                _logger.LogInfo("UPDATE", "DOWNLOAD_START", new
                {
                    version = version.Version,
                    url = version.DownloadUrl,
                    sizeMb = version.FileSizeMb
                });

                // Crear carpeta temporal
                var tempFolder = Path.Combine(Path.GetTempPath(), "SistemaGestionProyectos_Updates");
                Directory.CreateDirectory(tempFolder);

                var fileName = $"SistemaGestionProyectos-v{version.Version}-Setup.exe";
                var filePath = Path.Combine(tempFolder, fileName);

                // Si ya existe, eliminarlo
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Descargar archivo
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10); // Timeout de 10 minutos

                    using (var response = await httpClient.GetAsync(version.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var bytesRead = 0L;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            int read;

                            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read);
                                bytesRead += read;

                                // Reportar progreso
                                if (totalBytes > 0 && progress != null)
                                {
                                    var percentage = (int)((bytesRead * 100) / totalBytes);
                                    progress.Report(percentage);
                                }
                            }
                        }
                    }
                }

                _logger.LogInfo("UPDATE", "DOWNLOAD_SUCCESS", new
                {
                    version = version.Version,
                    filePath,
                    sizeMb = new FileInfo(filePath).Length / (1024.0 * 1024.0)
                });

                // Incrementar contador de descargas en la BD
                await IncrementDownloadCount(version.Id);

                return (true, filePath, "Descarga completada");
            }
            catch (Exception ex)
            {
                _logger.LogError("UPDATE", "DOWNLOAD_ERROR", new
                {
                    version = version.Version,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });

                return (false, null, $"Error descargando actualización: {ex.Message}");
            }
        }

        /// <summary>
        /// Ejecuta el instalador de la actualización
        /// </summary>
        public void InstallUpdate(string installerPath, bool silent = false)
        {
            try
            {
                _logger.LogInfo("UPDATE", "INSTALL_START", new
                {
                    installerPath,
                    silent
                });

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true,
                    Verb = "runas" // Ejecutar como administrador
                };

                if (silent)
                {
                    startInfo.Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";
                }

                Process.Start(startInfo);

                _logger.LogInfo("UPDATE", "INSTALLER_LAUNCHED", new
                {
                    installerPath
                });

                // Cerrar la aplicación actual para permitir la instalación
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("UPDATE", "INSTALL_ERROR", new
                {
                    installerPath,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });

                throw new Exception($"Error ejecutando instalador: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Compara dos versiones en formato "X.Y.Z"
        /// </summary>
        private bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                var newParts = newVersion.Split('.').Select(int.Parse).ToArray();
                var currentParts = currentVersion.Split('.').Select(int.Parse).ToArray();

                // Comparar major
                if (newParts[0] > currentParts[0]) return true;
                if (newParts[0] < currentParts[0]) return false;

                // Comparar minor
                if (newParts[1] > currentParts[1]) return true;
                if (newParts[1] < currentParts[1]) return false;

                // Comparar patch
                if (newParts[2] > currentParts[2]) return true;

                return false; // Son iguales o la nueva es menor
            }
            catch
            {
                // Si hay error parseando, asumir que no hay actualización
                return false;
            }
        }

        /// <summary>
        /// Incrementa el contador de descargas
        /// </summary>
        private async Task IncrementDownloadCount(int versionId)
        {
            try
            {
                // Obtener versión actual
                var response = await SupabaseClient
                    .From<AppVersionDb>()
                    .Where(x => x.Id == versionId)
                    .Single();

                if (response != null)
                {
                    // Incrementar contador
                    response.DownloadsCount++;

                    // Actualizar en la base de datos
                    await SupabaseClient
                        .From<AppVersionDb>()
                        .Update(response);

                    _logger.LogInfo("UPDATE", "DOWNLOAD_COUNT_INCREMENTED", new
                    {
                        versionId,
                        count = response.DownloadsCount
                    });
                }
            }
            catch (Exception ex)
            {
                // No es crítico si falla, solo logear
                _logger.LogWarning("UPDATE", "DOWNLOAD_COUNT_ERROR", new
                {
                    versionId,
                    error = ex.Message
                });
            }
        }
    }
}
