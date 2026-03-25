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
    /// V3-F2: Subdirectory per file (clean names) + "Save As" detection.
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

        // Suppress watcher events caused by our own downloads/writes
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
                MigrateOldFiles(); // V3-F2: migrate {id}_{name} → {id}/{name}
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
        /// Download file to local subdirectory (or use cached), register in manifest, return local path.
        /// Files are stored as {baseDir}/{fileId}/{originalFileName} to preserve clean names.
        /// </summary>
        public async Task<string?> OpenFile(DriveFileDb file, CancellationToken ct = default)
        {
            Initialize();

            // V3-F2: Subdirectory per file ID → clean file name
            var subDir = Path.Combine(_baseDir, file.Id.ToString());
            Directory.CreateDirectory(subDir);
            var localPath = Path.Combine(subDir, file.FileName);

            lock (_manifestLock)
            {
                var existing = _manifest.Files.FirstOrDefault(f => f.FileId == file.Id);
                if (existing != null && File.Exists(existing.LocalPath))
                {
                    var remoteTime = file.UploadedAt ?? DateTime.MinValue;
                    if (remoteTime <= existing.RemoteUploadedAt)
                    {
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

        /// <summary>MEJORA-5: Get local path for a file if it exists in cache (opened or context-downloaded).</summary>
        public string? GetCachedLocalPath(int fileId)
        {
            lock (_manifestLock)
            {
                var entry = _manifest.Files.FirstOrDefault(f => f.FileId == fileId);
                if (entry != null && File.Exists(entry.LocalPath)) return entry.LocalPath;
                return null;
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
                // V3-F2: Watch subdirectories to detect "Save As" new files
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += (s, e) => OnFileChanged(s, e);
            _watcher.Created += OnFileCreated; // V3-F2: detect "Save As" new files
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var fileName = Path.GetFileName(e.FullPath);

            // Ignore temp files and system files
            if (IsIgnoredFile(fileName)) return;

            // Ignore events caused by our own downloads
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

            // Check if file actually changed (same LastWriteTime = no real change)
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

        /// <summary>
        /// V3-F2: Detect new files created via "Save As" in a tracked subdirectory.
        /// If user opens file.ipt and does "Save As" file.dwg in the same folder,
        /// we detect the new .dwg and upload it as a new file to the same Drive folder.
        /// </summary>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var fileName = Path.GetFileName(e.FullPath);
            if (IsIgnoredFile(fileName)) return;

            // Ignore events caused by our own downloads
            lock (_suppressPaths)
            {
                if (_suppressPaths.Contains(e.FullPath)) return;
            }

            // Check if this file is already tracked (normal open)
            lock (_manifestLock)
            {
                if (_manifest.Files.Any(f => string.Equals(f.LocalPath, e.FullPath, StringComparison.OrdinalIgnoreCase)))
                    return;
            }

            // Determine which tracked file's subdirectory this belongs to
            var parentDir = Path.GetDirectoryName(e.FullPath);
            if (parentDir == null || string.Equals(parentDir, _baseDir, StringComparison.OrdinalIgnoreCase))
                return; // File in root of open/ — not a subdirectory, ignore

            var subDirName = Path.GetFileName(parentDir);
            if (!int.TryParse(subDirName, out var originFileId))
                return; // Not a numeric subdirectory

            WatchedFileEntry? originEntry;
            lock (_manifestLock)
            {
                originEntry = _manifest.Files.FirstOrDefault(f => f.FileId == originFileId && f.Watching);
            }

            if (originEntry == null) return;

            Debug.WriteLine($"[FileWatcher] 'Save As' detected: {fileName} in folder of file #{originFileId} (Drive folder={originEntry.FolderId})");

            // Debounce the upload of the new file (apps may write in chunks)
            lock (_debounceTokens)
            {
                if (_debounceTokens.TryGetValue(e.FullPath, out var prev))
                {
                    prev.Cancel();
                    prev.Dispose();
                }
                var cts = new CancellationTokenSource();
                _debounceTokens[e.FullPath] = cts;

                _ = DebouncedSaveAsUpload(e.FullPath, fileName, originEntry, cts.Token);
            }
        }

        private async Task DebouncedSaveAsUpload(string localPath, string fileName, WatchedFileEntry originEntry, CancellationToken ct)
        {
            try
            {
                // Longer debounce for "Save As" — apps may take time to finish writing
                await Task.Delay(4000, ct);
            }
            catch (TaskCanceledException) { return; }

            // Wait for file to be unlocked
            for (int i = 0; i < 5; i++)
            {
                if (IsFileUnlocked(localPath)) break;
                await Task.Delay(1000);
            }
            if (!IsFileUnlocked(localPath))
            {
                Debug.WriteLine($"[FileWatcher] SaveAs file still locked: {localPath}");
                return;
            }

            try
            {
                await _uploadSemaphore.WaitAsync();
                try
                {
                    Debug.WriteLine($"[FileWatcher] Uploading 'Save As' file: {fileName} → Drive folder {originEntry.FolderId}");
                    var uploaded = await SupabaseService.Instance.UploadDriveFile(localPath, originEntry.FolderId, CurrentUserId);

                    if (uploaded != null)
                    {
                        // Register the new file in manifest so edits are also tracked
                        var newEntry = new WatchedFileEntry
                        {
                            FileId = uploaded.Id,
                            FolderId = uploaded.FolderId,
                            LocalPath = localPath,
                            StoragePath = uploaded.StoragePath,
                            RemoteUploadedAt = uploaded.UploadedAt ?? DateTime.Now,
                            LocalModifiedAt = File.GetLastWriteTime(localPath),
                            Size = new FileInfo(localPath).Length,
                            Watching = true
                        };

                        lock (_manifestLock)
                        {
                            _manifest.Files.RemoveAll(f => f.FileId == uploaded.Id);
                            _manifest.Files.Add(newEntry);
                            SaveManifest();
                        }

                        SetSyncState(uploaded.Id, SyncState.Synced);
                        FileAutoUploaded?.Invoke(fileName, "success");
                        Debug.WriteLine($"[FileWatcher] 'Save As' uploaded: {fileName} → drive_files.id={uploaded.Id}");

                        _ = Task.Delay(5000).ContinueWith(_ =>
                        {
                            if (GetSyncState(uploaded.Id) == SyncState.Synced)
                                SetSyncState(uploaded.Id, SyncState.Opened);
                        });
                    }
                    else
                    {
                        FileAutoUploaded?.Invoke(fileName, "error");
                        Debug.WriteLine($"[FileWatcher] 'Save As' upload failed: {fileName}");
                    }
                }
                finally { _uploadSemaphore.Release(); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileWatcher] SaveAs upload error: {ex.Message}");
                FileAutoUploaded?.Invoke(fileName, "error");
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
            // Double-check the file actually changed from what we last uploaded
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
                // Check for conflict using server's actual uploaded_at
                var serverFile = await SupabaseService.Instance.GetDriveFileById(entry.FileId);
                if (serverFile == null)
                {
                    SetSyncState(entry.FileId, SyncState.Error);
                    FileAutoUploaded?.Invoke(Path.GetFileName(entry.LocalPath), "error");
                    return;
                }

                var serverTime = serverFile.UploadedAt ?? DateTime.MinValue;
                if (serverTime > entry.RemoteUploadedAt.AddSeconds(5))
                {
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

        /// <summary>MEJORA-6: Download sibling files to the same local directory as the assembly.
        /// This ensures CAD software finds referenced parts when opening an assembly.
        /// Files are registered in manifest with Watching=false (not auto-synced).</summary>
        public async Task<int> DownloadContext(List<DriveFileDb> siblings, string contextDir, CancellationToken ct = default, Action<int, int>? onProgress = null)
        {
            Initialize();
            int downloaded = 0;
            int processed = 0;
            var total = siblings.Count;
            var semaphore = new SemaphoreSlim(3);
            var tasks = siblings.Select(async sib =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var localPath = Path.Combine(contextDir, sib.FileName);

                    // Skip if already cached and up-to-date
                    bool needsDownload = true;
                    lock (_manifestLock)
                    {
                        var existing = _manifest.Files.FirstOrDefault(f => f.FileId == sib.Id);
                        if (existing != null && File.Exists(existing.LocalPath))
                        {
                            var remoteTime = sib.UploadedAt ?? DateTime.MinValue;
                            if (remoteTime <= existing.RemoteUploadedAt)
                                needsDownload = false;
                        }
                    }
                    if (!needsDownload)
                    {
                        var p = Interlocked.Increment(ref processed);
                        onProgress?.Invoke(p, total);
                        return;
                    }

                    // Suppress watcher events for this download
                    lock (_suppressPaths) { _suppressPaths.Add(localPath); }
                    try
                    {
                        var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(sib.Id, localPath, ct);
                        if (!ok) return;
                        await Task.Delay(300, ct); // Let watcher flush

                        // Register in manifest with Watching=false (cached, not auto-synced)
                        lock (_manifestLock)
                        {
                            _manifest.Files.RemoveAll(f => f.FileId == sib.Id);
                            _manifest.Files.Add(new WatchedFileEntry
                            {
                                FileId = sib.Id,
                                FolderId = sib.FolderId,
                                LocalPath = localPath,
                                StoragePath = sib.StoragePath,
                                RemoteUploadedAt = sib.UploadedAt ?? DateTime.Now,
                                LocalModifiedAt = File.GetLastWriteTime(localPath),
                                Size = new FileInfo(localPath).Length,
                                Watching = false // Not auto-synced, just cached for assembly context
                            });
                            SaveManifest();
                        }
                        Interlocked.Increment(ref downloaded);
                        var p = Interlocked.Increment(ref processed);
                        onProgress?.Invoke(p, total);
                    }
                    finally
                    {
                        lock (_suppressPaths) { _suppressPaths.Remove(localPath); }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileWatcher] DownloadContext skip {sib.FileName}: {ex.Message}");
                    var p = Interlocked.Increment(ref processed);
                    onProgress?.Invoke(p, total);
                }
                finally { semaphore.Release(); }
            });
            await Task.WhenAll(tasks);
            return downloaded;
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
                lock (_suppressPaths) { _suppressPaths.Add(entry.LocalPath); }
                try
                {
                    var ok = await SupabaseService.Instance.DownloadDriveFileToLocal(fileId, entry.LocalPath);
                    if (ok)
                    {
                        await Task.Delay(500);
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
                    .GetFiles("*", SearchOption.AllDirectories)
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
                // Clean up subdirectories
                try
                {
                    foreach (var dir in Directory.GetDirectories(_baseDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
                catch { }
                _manifest.Files.Clear();
                _manifest.LastCleanup = DateTime.Now;
                SaveManifest();
            }

            lock (_syncStates) { _syncStates.Clear(); }
            Debug.WriteLine("[FileWatcher] Local cache cleared");
        }

        // ===== Helpers =====

        private static bool IsIgnoredFile(string fileName)
        {
            return fileName.StartsWith("~$")
                || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith(".", StringComparison.Ordinal)
                || fileName == ".manifest.json";
        }

        // ===== Migration: {id}_{name} → {id}/{name} =====

        private void MigrateOldFiles()
        {
            lock (_manifestLock)
            {
                var migrated = 0;
                foreach (var entry in _manifest.Files.ToList())
                {
                    if (!File.Exists(entry.LocalPath)) continue;
                    var dir = Path.GetDirectoryName(entry.LocalPath);

                    // Already in subdirectory format
                    if (dir != null && !string.Equals(dir, _baseDir, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Old format: {baseDir}/{id}_{filename}
                    var oldFileName = Path.GetFileName(entry.LocalPath);
                    var prefix = $"{entry.FileId}_";
                    if (!oldFileName.StartsWith(prefix)) continue;

                    var cleanName = oldFileName.Substring(prefix.Length);
                    var subDir = Path.Combine(_baseDir, entry.FileId.ToString());
                    Directory.CreateDirectory(subDir);
                    var newPath = Path.Combine(subDir, cleanName);

                    try
                    {
                        File.Move(entry.LocalPath, newPath);
                        entry.LocalPath = newPath;
                        migrated++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[FileWatcher] Migration failed for {oldFileName}: {ex.Message}");
                    }
                }

                if (migrated > 0)
                {
                    SaveManifest();
                    Debug.WriteLine($"[FileWatcher] Migrated {migrated} files to subdirectory format");
                }
            }
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
                    try
                    {
                        if (File.Exists(entry.LocalPath)) File.Delete(entry.LocalPath);
                        // Clean up empty subdirectory
                        var dir = Path.GetDirectoryName(entry.LocalPath);
                        if (dir != null && !string.Equals(dir, _baseDir, StringComparison.OrdinalIgnoreCase)
                            && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                        }
                    }
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
