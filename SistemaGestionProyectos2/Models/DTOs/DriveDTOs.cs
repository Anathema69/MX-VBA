using System;
using System.Collections.Generic;

namespace SistemaGestionProyectos2.Models.DTOs
{
    /// <summary>V3-E: Sync state for a file opened in-place</summary>
    public enum SyncState
    {
        None,
        Opened,
        Syncing,
        Synced,
        Error,
        Conflict
    }

    /// <summary>V3-E: Tracks a file opened locally for auto-sync</summary>
    public class WatchedFileEntry
    {
        public int FileId { get; set; }
        public int FolderId { get; set; }
        public string LocalPath { get; set; } = "";
        public string StoragePath { get; set; } = "";
        public DateTime RemoteUploadedAt { get; set; }
        public DateTime LocalModifiedAt { get; set; }
        public long Size { get; set; }
        public bool Watching { get; set; } = true;
    }

    /// <summary>V3-E: Persisted manifest of opened files</summary>
    public class FileManifest
    {
        public List<WatchedFileEntry> Files { get; set; } = new();
        public DateTime LastCleanup { get; set; } = DateTime.Now;
    }
}
