using Postgrest.Attributes;
using Postgrest.Models;
using System;
using System.Text.Json.Serialization;

namespace SistemaGestionProyectos2.Models.Database
{
    /// <summary>
    /// Modelo de base de datos para versiones de la aplicación
    /// </summary>
    [Table("app_versions")]
    public class AppVersionDb : BaseModel
    {
        [PrimaryKey("id", shouldInsert: false)]
        public int Id { get; set; }

        [Column("version")]
        public string Version { get; set; }

        [Column("release_date")]
        public DateTime ReleaseDate { get; set; }

        [Column("is_latest")]
        public bool IsLatest { get; set; }

        [Column("is_mandatory")]
        public bool IsMandatory { get; set; }

        [Column("download_url")]
        public string DownloadUrl { get; set; }

        [Column("file_size_mb")]
        public decimal? FileSizeMb { get; set; }

        [Column("release_notes")]
        public string ReleaseNotes { get; set; }

        [Column("min_version")]
        public string MinVersion { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("downloads_count")]
        public int DownloadsCount { get; set; }

        /// <summary>
        /// Changelog estructurado en formato JSON
        /// </summary>
        [Column("changelog")]
        public string ChangelogJson { get; set; }

        /// <summary>
        /// Parsea el changelog desde JSON
        /// </summary>
        [JsonIgnore]
        public ChangelogData Changelog
        {
            get
            {
                if (string.IsNullOrEmpty(ChangelogJson))
                    return new ChangelogData();

                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<ChangelogData>(ChangelogJson);
                }
                catch
                {
                    return new ChangelogData();
                }
            }
        }
    }

    /// <summary>
    /// Estructura del changelog
    /// </summary>
    public class ChangelogData
    {
        [JsonPropertyName("added")]
        public string[] Added { get; set; } = Array.Empty<string>();

        [JsonPropertyName("improved")]
        public string[] Improved { get; set; } = Array.Empty<string>();

        [JsonPropertyName("fixed")]
        public string[] Fixed { get; set; } = Array.Empty<string>();
    }
}
