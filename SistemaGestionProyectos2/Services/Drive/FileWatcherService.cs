using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Drive
{
    /// <summary>
    /// V3-E: Singleton service that monitors locally-opened Drive files
    /// and auto-syncs changes back to R2. Survives DriveV2Window close.
    /// </summary>
    public class FileWatcherService
    {
        private static readonly Lazy<FileWatcherService> _lazy = new(() => new FileWatcherService());
        public static FileWatcherService Instance => _lazy.Value;

        private readonly string _baseDir;
        private readonly string _manifestPath;
        private FileManifest _manifest = new();
        private FileSystemWatcher? _watcher;
        private readonly object _manifestLock = new();
        private readonly SemaphoreSlim _uploadSemaphore = new(1);
        private readonly Dictionary<string, CancellationTokenSource> _debounceTokens = new();
        private readonly Dictionary<int, SyncState> _syncStates = new();
        private bool _initialized;

        // FIX-1: Suppress watcher events caused by our own downloads/writes
        private readonly HashSet<string> _suppressPaths = new(StringComparer.OrdinalIgnoreCase);

        public int CurrentUserId { get; set; }

        // Events
        public event Action<string, string>? FileAutoUploaded; // fileName, status (success/error/conflict)
        public event Action<int, SyncState>? FileSyncStateChanged; // fileId, newState

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private FileWatcherService()
        {
            _baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IMA-Drive", "open");
            _manifestPath = Path.Combine(_baseDir, ".manifest.json");
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                Directory.CreateDirectory(_baseDir);
                LoadManifest();
                CleanupOldFiles();
                StartWatcher();
                Debug.WriteLine($"[FileWatcher] Initialized: {_baseDir}, {_manifest.Files.Count} tracked files");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileWatcher] Init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Download file to local dir (or use cached), register in manifest, return local path.
        /// </summary>
        public async Task<string?> OpenFile(DriveFileDb file, CancellationToken ct = default)
        {
            Initialize();

            var localPath = Path.Combine(_baseDir, $"{file.Id}_{file.FileName}");

            lock (_manifestLock)
            {
                var existing = _manifest.Files.FirstOrDefault(f => f.FileId == file.Id);
                if (existing != null && File.Exists(existing.LocalPath))
                {
                    // Check if remote is newer than what we have
                    var remoteTime = file.UploadedAt ?? DateTime.MinValue;
                    if (remoteTime <= existing.RemoteUploadedAt)
                    {
                        // Local copy is still valid — reuse it
                        existing.Watching = true;
                        SetSyncState(file.Id, SyncState.Opened);
                        SaveManifest();
                        return existing.LocalPath;
                    }
                }
            }

            // Download fresh copy — suppress watcher so it doesn't trigger a false sync
            try
            {
                lock (_suppressPaths) { _suppressPaths.Add(localPath); }

                var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(file.Id, localPath, ct);
                if (!ok) return null;

                // Small delay to let FileSystemWatcher flush its event before we unsuppress
                await Task.Delay(500);

                var entry = new WatchedFileEntry
                {
                    FileId = file.Id,
                    FolderId = file.FolderId,
                    LocalPath = localPath,
                    StoragePath = file.StoragePath,
                    RemoteUploadedAt = file.UploadedAt ?? DateTime.Now,
                    LocalModifiedAt = File.GetLastWriteTime(localPath),
                    Size = new FileInfo(localPath).Length,
                    Watching = true
                };

                lock (_manifestLock)
                {
                    _manifest.Files.RemoveAll(f => f.FileId == file.Id);
                    _manifest.Files.Add(entry);
                    SaveManifest();
                }

                SetSyncState(file.Id, SyncState.Opened);
                return localPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileWatcher] OpenFile error: {ex.Message}");
                return null;
            }
            finally
            {
                lock (_suppressPaths) { _suppressPaths.Remove(localPath); }
            }
        }

        public bool IsFileOpened(int fileId)
        {
            lock (_manifestLock)
            {
                return _manifest.Files.Any(f => f.FileId == fileId && f.Watching);
            }
        }

        public SyncState GetSyncState(int fileId)
        {
            lock (_syncStates)
            {
                return _syncStates.TryGetValue(fileId, out var state) ? state : SyncState.None;
            }
        }

        private void SetSyncState(int fileId, SyncState state)
        {
            lock (_syncStates)
            {
                _syncStates[fileId] = state;
            }
            FileSyncStateChanged?.Invoke(fileId, state);
        }

        private void StartWatcher()
        {
            if (_watcher != null) return;

            _watcher = new FileSystemWatcher(_baseDir)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += (s, e) => OnFileChanged(s, e);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var fileName = Path.GetFileName(e.FullPath);

            // Ignore Office temp files and system files
            if (fileName.StartsWith("~$") || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || fileName == ".manifest.json")
                return;

            // FIX-1: Ignore events caused by our own downloads
            lock (_suppressPaths)
            {
                if (_suppressPaths.Contains(e.FullPath)) return;
            }

            WatchedFileEntry? entry;
            lock (_manifestLock)
            {
                entry = _manifest.Files.FirstOrDefault(f =>
                    string.Equals(f.LocalPath, e.FullPath, StringComparison.OrdinalIgnoreCase) && f.Watching);
            }

            if (entry == null) return;

            // FIX-3: Check if file actually changed (same LastWriteTime = no real change)
            var currentModified = File.GetLastWriteTime(e.FullPath);
            if (Math.Abs((currentModified - entry.LocalModifiedAt).TotalSeconds) < 1)
            {
                Debug.WriteLine($"[FileWatcher] Skipping unchanged file: {fileName}");
                return;
            }

            // Debounce: cancel previous timer for this file, start new 2s delay
            lock (_debounceTokens)
            {
                if (_debounceTokens.TryGetValue(e.FullPath, out var prev))
                {
                    prev.Cancel();
                    prev.Dispose();
                }
                var cts = new CancellationTokenSource();
                _debounceTokens[e.FullPath] = cts;

                _ = DebouncedUpload(entry, cts.Token);
            }
        }

        private async Task DebouncedUpload(WatchedFileEntry entry, CancellationToken ct)
        {
            try
            {
                await Task.Delay(2000, ct);
            }
            catch (TaskCanceledException)
            {
                return; // Another change came in, this debounce is superseded
            }

            await TryAutoUpload(entry);
        }

        private async Task TryAutoUpload(WatchedFileEntry entry)
        {
            // FIX-3: Double-check the file actually changed from what we last uploaded
            if (!File.Exists(entry.LocalPath)) return;
            var currentSize = new FileInfo(entry.LocalPath).Length;
            var currentModified = File.GetLastWriteTime(entry.LocalPath);
            if (currentSize == entry.Size && Math.Abs((currentModified - entry.LocalModifiedAt).TotalSeconds) < 1)
            {
                Debug.WriteLine($"[FileWatcher] Skipping auto-upload, no real change: {Path.GetFileName(entry.LocalPath)}");
                return;
            }

            SetSyncState(entry.FileId, SyncState.Syncing);

            // Wait for file to be unlocked (3 retries x 1s)
            for (int i = 0; i < 3; i++)
            {
                if (IsFileUnlocked(entry.LocalPath)) break;
                await Task.Delay(1000);
            }

            if (!IsFileUnlocked(entry.LocalPath))
            {
                Debug.WriteLine($"[FileWatcher] File still locked after retries: {entry.LocalPath}");
                SetSyncState(entry.FileId, SyncState.Opened);
                return;
            }

            try
            {
                // FIX-2: Check for conflict using server's actual uploaded_at
                // Only flag conflict if someone ELSE uploaded a new version while we had it open
                var serverFile = await SupabaseService.Instance.GetDriveFileById(entry.FileId);
                if (serverFile == null)
                {
                    SetSyncState(entry.FileId, SyncState.Error);
                    FileAutoUploaded?.Invoke(Path.GetFileName(entry.LocalPath), "error");
                    return;
                }

                var serverTime = serverFile.UploadedAt ?? DateTime.MinValue;
                // Use a 5-second tolerance to account for clock skew between client and Supabase server
                if (serverTime > entry.RemoteUploadedAt.AddSeconds(5))
                {
                    // Server has a genuinely newer version — conflict!
                    Debug.WriteLine($"[FileWatcher] CONFLICT: server={serverTime:O} > manifest={entry.RemoteUploadedAt:O} (+5s tolerance)");
                    SetSyncState(entry.FileId, SyncState.Conflict);
                    FileAutoUploaded?.Invoke(serverFile.FileName, "conflict");
                    return;
                }

                await _uploadSemaphore.WaitAsync();
                try
                {
                    var ok = await SupabaseService.Instance.ReuploadDriveFile(
                        entry.FileId, entry.LocalPath, CurrentUserId);

                    if (ok)
                    {
                        // FIX-2: Read back the ACTUAL server timestamp instead of DateTime.Now
                        var updatedFile = await SupabaseService.Instance.GetDriveFileById(entry.FileId);

                        lock (_manifestLock)
                        {
                            entry.RemoteUploadedAt = updatedFile?.UploadedAt ?? DateTime.Now;
                            entry.LocalModifiedAt = File.GetLastWriteTime(entry.LocalPath);
                            entry.Size = new FileInfo(entry.LocalPath).Length;
                            SaveManifest();
                        }

                        SetSyncState(entry.FileId, SyncState.Synced);
                        FileAutoUploaded?.Invoke(serverFile.FileName, "success");

                        // After 5 seconds, revert to Opened state
                        _ = Task.Delay(5000).ContinueWith(_ =>
                        {
                            if (GetSyncState(entry.FileId) == SyncState.Synced)
                                SetSyncState(entry.FileId, SyncState.Opened);
                        });
                    }
                    else
                    {
                        SetSyncState(entry.FileId, SyncState.Error);
                        FileAutoUploaded?.Invoke(serverFile.FileName, "error");
                    }
                }
                finally
                {
                    _uploadSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileWatcher] Auto-upload error: {ex.Message}");
                SetSyncState(entry.FileId, SyncState.Error);
                FileAutoUploaded?.Invoke(Path.GetFileName(entry.LocalPath), "error");
            }
        }

        /// <summary>Force reupload, ignoring conflict check (user chose "replace with mine")</summary>
        public async Task<bool> ForceReupload(int fileId)
        {
            WatchedFileEntry? entry;
            lock (_manifestLock)
            {
                entry = _manifest.Files.FirstOrDefault(f => f.FileId == fileId);
            }
            if (entry == null || !File.Exists(entry.LocalPath)) return false;

            SetSyncState(fileId, SyncState.Syncing);
            try
            {
                await _uploadSemaphore.WaitAsync();
                try
                {
                    var ok = await SupabaseService.Instance.ReuploadDriveFile(fileId, entry.LocalPath, CurrentUserId);
                    if (ok)
                    {
                        // FIX-2: Read server's actual timestamp
                        var updatedFile = await SupabaseService.Instance.GetDriveFileById(fileId);
                        lock (_manifestLock)
                        {
                            entry.RemoteUploadedAt = updatedFile?.UploadedAt ?? DateTime.Now;
                            entry.LocalModifiedAt = File.GetLastWriteTime(entry.LocalPath);
                            entry.Size = new FileInfo(entry.LocalPath).Length;
                            SaveManifest();
                        }
                        SetSyncState(fileId, SyncState.Synced);
                        _ = Task.Delay(5000).ContinueWith(_ =>
                        {
                            if (GetSyncState(fileId) == SyncState.Synced)
                                SetSyncState(fileId, SyncState.Opened);
                        });
                        return true;
                    }
                }
                finally { _uploadSemaphore.Release(); }
            }
            catch (Exception ex) { Debug.WriteLine($"[FileWatcher] ForceReupload error: {ex.Message}"); }

            SetSyncState(fileId, SyncState.Error);
            return false;
        }

        /// <summary>Re-download server version, discarding local changes</summary>
        public async Task<bool> RedownloadServerVersion(int fileId)
        {
            WatchedFileEntry? entry;
            lock (_manifestLock)
            {
                entry = _manifest.Files.FirstOrDefault(f => f.FileId == fileId);
            }
            if (entry == null) return false;

            try
            {
                // Suppress watcher during our download
                lock (_suppressPaths) { _suppressPaths.Add(entry.LocalPath); }
                try
                {
                    var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(fileId, entry.LocalPath);
                    if (ok)
                    {
                        await Task.Delay(500); // let watcher flush
                        var serverFile = await SupabaseService.Instance.GetDriveFileById(fileId);
                        lock (_manifestLock)
                        {
                            entry.RemoteUploadedAt = serverFile?.UploadedAt ?? DateTime.Now;
                            entry.LocalModifiedAt = File.GetLastWriteTime(entry.LocalPath);
                            entry.Size = new FileInfo(entry.LocalPath).Length;
                            SaveManifest();
                        }
                        SetSyncState(fileId, SyncState.Opened);
                        return true;
                    }
                }
                finally
                {
                    lock (_suppressPaths) { _suppressPaths.Remove(entry.LocalPath); }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[FileWatcher] RedownloadServerVersion error: {ex.Message}"); }
            return false;
        }

        public bool HasPendingSyncs()
        {
            lock (_syncStates)
            {
                return _syncStates.Values.Any(s => s == SyncState.Syncing || s == SyncState.Error || s == SyncState.Conflict);
            }
        }

        public List<(int FileId, SyncState State)> GetPendingStates()
        {
            lock (_syncStates)
            {
                return _syncStates
                    .Where(kv => kv.Value == SyncState.Syncing || kv.Value == SyncState.Error || kv.Value == SyncState.Conflict)
                    .Select(kv => (kv.Key, kv.Value))
                    .ToList();
            }
        }

        /// <summary>Get total size of all local cached files</summary>
        public long GetLocalCacheSize()
        {
            try
            {
                if (!Directory.Exists(_baseDir)) return 0;
                return new DirectoryInfo(_baseDir)
                    .GetFiles()
                    .Where(f => f.Name != ".manifest.json")
                    .Sum(f => f.Length);
            }
            catch { return 0; }
        }

        /// <summary>Delete all local cached files and reset manifest</summary>
        public void ClearLocalCache()
        {
            lock (_manifestLock)
            {
                foreach (var entry in _manifest.Files)
                {
                    try { if (File.Exists(entry.LocalPath)) File.Delete(entry.LocalPath); }
                    catch { /* non-critical */ }
                }
                _manifest.Files.Clear();
                _manifest.LastCleanup = DateTime.Now;
                SaveManifest();
            }

            lock (_syncStates) { _syncStates.Clear(); }
            Debug.WriteLine("[FileWatcher] Local cache cleared");
        }

        // ===== Manifest I/O =====

        private void LoadManifest()
        {
            try
            {
                if (File.Exists(_manifestPath))
                {
                    var json = File.ReadAllText(_manifestPath);
                    _manifest = JsonSerializer.Deserialize<FileManifest>(json, _jsonOpts) ?? new FileManifest();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileWatcher] LoadManifest error: {ex.Message}");
                _manifest = new FileManifest();
            }
        }

        private void SaveManifest()
        {
            // Must be called inside _manifestLock
            try
            {
                var json = JsonSerializer.Serialize(_manifest, _jsonOpts);
                File.WriteAllText(_manifestPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileWatcher] SaveManifest error: {ex.Message}");
            }
        }

        private void CleanupOldFiles()
        {
            var cutoff = DateTime.Now.AddDays(-7);
            lock (_manifestLock)
            {
                var toRemove = _manifest.Files
                    .Where(f => f.LocalModifiedAt < cutoff || !File.Exists(f.LocalPath))
                    .ToList();

                foreach (var entry in toRemove)
                {
                    try { if (File.Exists(entry.LocalPath)) File.Delete(entry.LocalPath); }
                    catch { /* non-critical */ }
                    _manifest.Files.Remove(entry);
                }

                if (toRemove.Count > 0)
                {
                    _manifest.LastCleanup = DateTime.Now;
                    SaveManifest();
                    Debug.WriteLine($"[FileWatcher] Cleaned up {toRemove.Count} old files");
                }
            }
        }

        private static bool IsFileUnlocked(string path)
        {
            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch (IOException) { return false; }
        }
    }
}
