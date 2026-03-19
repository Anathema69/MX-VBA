using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SistemaGestionProyectos2.Models.DTOs;
using SistemaGestionProyectos2.Services;
using SistemaGestionProyectos2.Services.Drive;

namespace SistemaGestionProyectos2.Tests
{
    public class DriveWorkflowTests
    {
        private readonly SupabaseService _service;
        private int _testUserId = 1;

        /// <summary>Path to fase4/_carpeta_test/ with real sample files</summary>
        private readonly string _testFilesDir;

        public DriveWorkflowTests()
        {
            _service = SupabaseService.Instance;
            // Resolve fase4/_carpeta_test/ from repo root
            // bin/Debug/net8.0-windows/ → go up 4 levels to repo root
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            _testFilesDir = Path.Combine(repoRoot, "fase4", "_carpeta_test");
        }

        /// <summary>Get sample files from _carpeta_test by extension pattern</summary>
        private string[] GetSampleFiles(params string[] extensions)
        {
            if (!Directory.Exists(_testFilesDir)) return Array.Empty<string>();
            return Directory.GetFiles(_testFilesDir)
                .Where(f => extensions.Length == 0 || extensions.Any(ext =>
                    f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        private string GetSampleFile(string extension)
        {
            var files = GetSampleFiles(extension);
            return files.Length > 0 ? files[0] : null;
        }

        public async Task<List<TestResult>> RunAllTests(Action<TestResult> onResult = null)
        {
            var results = new List<TestResult>();

            // 0. Verify test files exist
            await RunTest(results, "Setup: Verificar archivos de prueba", "Workflow", 1000, async () =>
            {
                await Task.CompletedTask;
                if (!Directory.Exists(_testFilesDir))
                    throw new Exception($"Carpeta de test no encontrada: {_testFilesDir}");
                var allFiles = Directory.GetFiles(_testFilesDir);
                if (allFiles.Length == 0) throw new Exception("Carpeta de test vacia");
                var exts = allFiles.Select(f => Path.GetExtension(f).ToLower()).Distinct().OrderBy(e => e);
                Debug.WriteLine($"[WorkflowTest] {allFiles.Length} archivos en {_testFilesDir}: {string.Join(", ", exts)}");
            }, onResult);

            // 1. Folder CRUD: Create → Rename → Move → Verify → Delete
            await RunTest(results, "FolderCRUD: Create→Rename→Move→Delete", "Workflow", 10000, async () =>
            {
                var parent = await _service.CreateDriveFolder($"_test_parent_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (parent == null) throw new Exception("Failed to create parent folder");

                var child = await _service.CreateDriveFolder($"_test_child_{Guid.NewGuid().ToString()[..6]}", parent.Id, _testUserId);
                if (child == null) throw new Exception("Failed to create child folder");

                var renamed = await _service.RenameDriveFolder(child.Id, $"_test_renamed_{Guid.NewGuid().ToString()[..6]}");
                if (!renamed) throw new Exception("Failed to rename folder");

                var target = await _service.CreateDriveFolder($"_test_target_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (target == null) throw new Exception("Failed to create target folder");

                var moved = await _service.MoveDriveFolder(child.Id, target.Id);
                if (!moved) throw new Exception("Failed to move folder");

                var movedFolder = await _service.GetDriveFolderById(child.Id);
                if (movedFolder?.ParentId != target.Id)
                    throw new Exception($"Folder parent mismatch: expected {target.Id}, got {movedFolder?.ParentId}");

                await _service.DeleteDriveFolder(target.Id);
                await _service.DeleteDriveFolder(parent.Id);
            }, onResult);

            // 2. Upload real files (PDF, PNG, SQL, TXT) → Rename → Move → Copy → Delete
            await RunTest(results, "FileCRUD: Upload reales→Rename→Move→Copy→Delete", "Workflow", 20000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage no configurado");

                var folder1 = await _service.CreateDriveFolder($"_test_files1_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                var folder2 = await _service.CreateDriveFolder($"_test_files2_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder1 == null || folder2 == null) throw new Exception("Failed to create test folders");

                var uploadedIds = new List<int>();
                try
                {
                    // Upload one of each type from _carpeta_test
                    var pdf = GetSampleFile(".pdf");
                    var png = GetSampleFile(".png");
                    var sql = GetSampleFile(".sql");
                    var txt = GetSampleFile(".txt");

                    var filesToUpload = new[] { pdf, png, sql, txt }.Where(f => f != null).ToList();
                    if (filesToUpload.Count == 0) throw new Exception("No sample files found in _carpeta_test");

                    foreach (var filePath in filesToUpload)
                    {
                        var uploaded = await _service.UploadDriveFile(filePath, folder1.Id, _testUserId);
                        if (uploaded == null) throw new Exception($"Failed to upload {Path.GetFileName(filePath)}");
                        uploadedIds.Add(uploaded.Id);
                        Debug.WriteLine($"[WorkflowTest] Uploaded: {uploaded.FileName} ({DriveService.FormatFileSize(uploaded.FileSize)})");
                    }

                    // Rename first file
                    var newName = $"_renamed_{Guid.NewGuid().ToString()[..6]}{Path.GetExtension(filesToUpload[0])}";
                    var renamedOk = await _service.RenameDriveFile(uploadedIds[0], newName, default, _testUserId);
                    if (!renamedOk) throw new Exception("Failed to rename file");

                    // Move first file to folder2
                    var movedOk = await _service.MoveDriveFile(uploadedIds[0], folder2.Id, default, _testUserId);
                    if (!movedOk) throw new Exception("Failed to move file");

                    // Copy second file to folder2
                    if (uploadedIds.Count > 1)
                    {
                        var copied = await _service.CopyDriveFile(uploadedIds[1], folder2.Id);
                        if (copied == null) throw new Exception("Failed to copy file");
                        uploadedIds.Add(copied.Id);
                    }

                    // Verify folder2 has files
                    var f2Files = await _service.GetDriveFilesByFolder(folder2.Id);
                    if (f2Files.Count == 0) throw new Exception("folder2 should have files after move+copy");

                    // Delete all
                    foreach (var id in uploadedIds)
                        await _service.DeleteDriveFile(id, default, _testUserId);
                }
                finally
                {
                    await _service.DeleteDriveFolder(folder1.Id);
                    await _service.DeleteDriveFolder(folder2.Id);
                }
            }, onResult);

            // 3. Bulk upload (multiple images) → CollectRecursive → Download → ZIP stream
            await RunTest(results, "BulkUpload: 4 imagenes→Collect→Download→Stream", "Workflow", 25000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage no configurado");

                var folder = await _service.CreateDriveFolder($"_test_bulk_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create bulk test folder");

                var uploadedIds = new List<int>();
                try
                {
                    // Upload up to 4 images
                    var images = GetSampleFiles(".png", ".jpg").Take(4).ToArray();
                    if (images.Length == 0) throw new Exception("No image files in _carpeta_test");

                    foreach (var img in images)
                    {
                        var uploaded = await _service.UploadDriveFile(img, folder.Id, _testUserId);
                        if (uploaded == null) throw new Exception($"Failed to upload {Path.GetFileName(img)}");
                        uploadedIds.Add(uploaded.Id);
                    }
                    Debug.WriteLine($"[WorkflowTest] Bulk uploaded {uploadedIds.Count} images");

                    // CollectRecursive
                    var allFiles = await _service.CollectDriveFilesRecursive(folder.Id);
                    if (allFiles.Count != uploadedIds.Count)
                        throw new Exception($"CollectRecursive: expected {uploadedIds.Count}, got {allFiles.Count}");

                    // Download first file to stream
                    using var ms = new MemoryStream();
                    var ok = await _service.DownloadDriveFileToStream(allFiles[0].StoragePath, ms);
                    if (!ok) throw new Exception("Failed to download to stream");
                    if (ms.Length == 0) throw new Exception("Downloaded stream was empty");
                    Debug.WriteLine($"[WorkflowTest] Downloaded {DriveService.FormatFileSize(ms.Length)} to stream");

                    // Cleanup
                    foreach (var id in uploadedIds)
                        await _service.DeleteDriveFile(id, default, _testUserId);
                }
                finally
                {
                    await _service.DeleteDriveFolder(folder.Id);
                }
            }, onResult);

            // 4. Open-in-Place: upload real PDF → OpenFile → verify local + manifest + state
            await RunTest(results, "OpenInPlace: PDF real→Open→Verify estado", "Workflow", 15000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage no configurado");

                var pdf = GetSampleFile(".pdf");
                if (pdf == null) throw new Exception("No PDF in _carpeta_test");

                var folder = await _service.CreateDriveFolder($"_test_oip_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create test folder");

                try
                {
                    var uploaded = await _service.UploadDriveFile(pdf, folder.Id, _testUserId);
                    if (uploaded == null) throw new Exception("Failed to upload PDF");

                    var watcher = FileWatcherService.Instance;
                    watcher.CurrentUserId = _testUserId;
                    watcher.Initialize();

                    var localPath = await watcher.OpenFile(uploaded);
                    if (localPath == null) throw new Exception("OpenFile returned null");
                    if (!File.Exists(localPath)) throw new Exception($"Local file not found: {localPath}");

                    var localSize = new FileInfo(localPath).Length;
                    Debug.WriteLine($"[WorkflowTest] OpenInPlace: {localPath} ({DriveService.FormatFileSize(localSize)})");

                    var state = watcher.GetSyncState(uploaded.Id);
                    if (state != SyncState.Opened) throw new Exception($"Expected Opened, got {state}");
                    if (!watcher.IsFileOpened(uploaded.Id)) throw new Exception("IsFileOpened = false");

                    // Cleanup
                    try { File.Delete(localPath); } catch { }
                    await _service.DeleteDriveFile(uploaded.Id, default, _testUserId);
                }
                finally
                {
                    await _service.DeleteDriveFolder(folder.Id);
                }
            }, onResult);

            // 5. Conflict auto-resolve: upload → open → simulate server change → force reupload
            await RunTest(results, "ConflictAutoResolve: Open→ServerChange→ForceReupload", "Workflow", 15000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage no configurado");

                var txt = GetSampleFile(".txt");
                if (txt == null) throw new Exception("No TXT in _carpeta_test");

                var folder = await _service.CreateDriveFolder($"_test_conflict_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create test folder");

                try
                {
                    var uploaded = await _service.UploadDriveFile(txt, folder.Id, _testUserId);
                    if (uploaded == null) throw new Exception("Failed to upload TXT");

                    var watcher = FileWatcherService.Instance;
                    watcher.CurrentUserId = _testUserId;
                    watcher.Initialize();

                    var localPath = await watcher.OpenFile(uploaded);
                    if (localPath == null) throw new Exception("OpenFile returned null");

                    // Simulate server change
                    var tempModified = Path.Combine(Path.GetTempPath(), $"_conflict_{Guid.NewGuid().ToString()[..6]}.txt");
                    await File.WriteAllTextAsync(tempModified, $"Server change at {DateTime.Now:O}");
                    var reuploaded = await _service.ReuploadDriveFile(uploaded.Id, tempModified, _testUserId);
                    try { File.Delete(tempModified); } catch { }
                    if (!reuploaded) throw new Exception("Failed to simulate server change");

                    // Force reupload (local wins — auto-resolve)
                    var resolved = await watcher.ForceReupload(uploaded.Id);
                    if (!resolved) throw new Exception("ForceReupload failed");

                    var finalState = watcher.GetSyncState(uploaded.Id);
                    Debug.WriteLine($"[WorkflowTest] After conflict resolve: state={finalState}");

                    // Cleanup
                    try { File.Delete(localPath); } catch { }
                    await _service.DeleteDriveFile(uploaded.Id, default, _testUserId);
                }
                finally
                {
                    await _service.DeleteDriveFolder(folder.Id);
                }
            }, onResult);

            // 6. Subfolder tree: create nested structure → upload files at each level → verify stats
            await RunTest(results, "SubfolderTree: 3 niveles→Upload→Stats→Cleanup", "Workflow", 25000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage no configurado");

                var root = await _service.CreateDriveFolder($"_test_tree_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (root == null) throw new Exception("Failed to create root");
                var sub1 = await _service.CreateDriveFolder("_sub_nivel1", root.Id, _testUserId);
                var sub2 = await _service.CreateDriveFolder("_sub_nivel2", sub1?.Id, _testUserId);
                if (sub1 == null || sub2 == null) throw new Exception("Failed to create subfolders");

                var uploadedIds = new List<int>();
                try
                {
                    // Upload a file at each level
                    var sampleFiles = GetSampleFiles(".sql", ".txt").Take(3).ToArray();
                    if (sampleFiles.Length < 2) throw new Exception("Need at least 2 sample files");

                    var folders = new[] { root, sub1, sub2 };
                    for (int i = 0; i < Math.Min(sampleFiles.Length, folders.Length); i++)
                    {
                        var uploaded = await _service.UploadDriveFile(sampleFiles[i], folders[i].Id, _testUserId);
                        if (uploaded != null) uploadedIds.Add(uploaded.Id);
                    }

                    // Verify recursive collection
                    var allFiles = await _service.CollectDriveFilesRecursive(root.Id);
                    if (allFiles.Count != uploadedIds.Count)
                        throw new Exception($"Tree collect: expected {uploadedIds.Count}, got {allFiles.Count}");

                    // Verify folder stats
                    var stats = await _service.GetDriveFolderStats(root.Id);
                    Debug.WriteLine($"[WorkflowTest] Tree stats: {stats.Count} entries");

                    // Cleanup files
                    foreach (var id in uploadedIds)
                        await _service.DeleteDriveFile(id, default, _testUserId);
                }
                finally
                {
                    // Delete tree (root deletes children recursively)
                    await _service.DeleteDriveFolder(root.Id);
                }
            }, onResult);

            return results;
        }

        private async Task RunTest(
            List<TestResult> results,
            string name,
            string category,
            long thresholdMs,
            Func<Task> test,
            Action<TestResult> onResult)
        {
            var sw = Stopwatch.StartNew();
            string error = null;
            bool passed;

            try
            {
                await test();
                sw.Stop();
                passed = sw.ElapsedMilliseconds <= thresholdMs;
            }
            catch (Exception ex)
            {
                sw.Stop();
                passed = false;
                error = $"{ex.Message}";
                Debug.WriteLine($"[WorkflowTest] FAIL {name}: {ex.Message}\n{ex.StackTrace}");
            }

            var result = new TestResult(name, category, passed, sw.ElapsedMilliseconds, thresholdMs, error);
            results.Add(result);
            onResult?.Invoke(result);
        }
    }
}
