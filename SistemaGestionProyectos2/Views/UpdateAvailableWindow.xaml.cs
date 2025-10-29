using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Updates;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SistemaGestionProyectos2.Views
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly UpdateService _updateService;
        private readonly AppVersionDb _newVersion;
        private readonly JsonLoggerService _logger;
        private bool _isDownloading = false;

        public bool UpdatePostponed { get; private set; } = false;

        public UpdateAvailableWindow(UpdateService updateService, AppVersionDb newVersion)
        {
            InitializeComponent();
            _updateService = updateService;
            _newVersion = newVersion;
            _logger = JsonLoggerService.Instance;

            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            try
            {
                // Título y versión
                TitleText.Text = _newVersion.IsMandatory
                    ? "Actualización obligatoria disponible"
                    : "Nueva versión disponible";

                var sizeText = _newVersion.FileSizeMb.HasValue
                    ? $"Tamaño: {_newVersion.FileSizeMb:F1} MB"
                    : "";

                VersionText.Text = $"Versión {_newVersion.Version} - {sizeText}";

                // Notas de la versión
                if (!string.IsNullOrEmpty(_newVersion.ReleaseNotes))
                {
                    ReleaseNotesText.Text = _newVersion.ReleaseNotes;
                }

                // Changelog detallado
                var changelog = _newVersion.Changelog;
                if (changelog != null)
                {
                    ChangelogPanel.Visibility = Visibility.Visible;

                    // Agregado
                    if (changelog.Added?.Any() == true)
                    {
                        AddedPanel.Visibility = Visibility.Visible;
                        AddedItemsControl.ItemsSource = changelog.Added.Select(item => $"• {item}");
                    }

                    // Mejorado
                    if (changelog.Improved?.Any() == true)
                    {
                        ImprovedPanel.Visibility = Visibility.Visible;
                        ImprovedItemsControl.ItemsSource = changelog.Improved.Select(item => $"• {item}");
                    }

                    // Corregido
                    if (changelog.Fixed?.Any() == true)
                    {
                        FixedPanel.Visibility = Visibility.Visible;
                        FixedItemsControl.ItemsSource = changelog.Fixed.Select(item => $"• {item}");
                    }
                }

                // Mostrar advertencia si es obligatoria
                if (_newVersion.IsMandatory)
                {
                    MandatoryWarning.Visibility = Visibility.Visible;
                    RemindLaterButton.Visibility = Visibility.Collapsed; // No permitir postponer
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("UPDATE_WINDOW", "LOAD_INFO_ERROR", new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
                return;

            _isDownloading = true;

            try
            {
                _logger.LogInfo("UPDATE_WINDOW", "DOWNLOAD_STARTED", new
                {
                    version = _newVersion.Version
                });

                // Deshabilitar botones
                UpdateNowButton.IsEnabled = false;
                RemindLaterButton.IsEnabled = false;

                // Mostrar progreso
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressText.Text = "Descargando actualización...";

                // Progreso
                var progress = new Progress<int>(percentage =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = percentage;
                        ProgressPercentageText.Text = $"{percentage}%";
                    });
                });

                // Descargar
                var (success, filePath, message) = await _updateService.DownloadUpdate(_newVersion, progress);

                if (success)
                {
                    _logger.LogInfo("UPDATE_WINDOW", "DOWNLOAD_COMPLETED", new
                    {
                        version = _newVersion.Version,
                        filePath
                    });

                    ProgressText.Text = "Descarga completada. Iniciando instalación...";

                    await Task.Delay(1000); // Pequeña pausa para que el usuario vea el mensaje

                    // Cerrar ventana
                    DialogResult = true;

                    // Ejecutar instalador
                    _updateService.InstallUpdate(filePath);
                }
                else
                {
                    _logger.LogError("UPDATE_WINDOW", "DOWNLOAD_FAILED", new
                    {
                        version = _newVersion.Version,
                        error = message
                    });

                    MessageBox.Show(
                        this,
                        $"Error al descargar la actualización:\n\n{message}\n\nPor favor, intenta nuevamente más tarde.",
                        "Error de Descarga",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // Re-habilitar botones
                    UpdateNowButton.IsEnabled = true;
                    RemindLaterButton.IsEnabled = true;
                    ProgressPanel.Visibility = Visibility.Collapsed;
                    _isDownloading = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("UPDATE_WINDOW", "UPDATE_ERROR", new
                {
                    version = _newVersion.Version,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });

                MessageBox.Show(
                    this,
                    $"Error inesperado:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                UpdateNowButton.IsEnabled = true;
                RemindLaterButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
                _isDownloading = false;
            }
        }

        private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInfo("UPDATE_WINDOW", "UPDATE_POSTPONED", new
            {
                version = _newVersion.Version
            });

            UpdatePostponed = true;
            DialogResult = false;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Si es obligatoria, no permitir cerrar sin actualizar
            if (_newVersion.IsMandatory && !DialogResult.HasValue)
            {
                var result = MessageBox.Show(
                    this,
                    "Esta actualización es obligatoria y no puede ser omitida.\n\n¿Deseas actualizar ahora?",
                    "Actualización Obligatoria",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true; // Cancelar el cierre
                }
            }

            base.OnClosing(e);
        }
    }
}
