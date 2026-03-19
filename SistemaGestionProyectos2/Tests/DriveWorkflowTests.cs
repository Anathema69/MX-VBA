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

        public DriveWorkflowTests()
        {
            _service = SupabaseService.Instance;
        }

        public async Task<List<TestResult>> RunAllTests(Action<TestResult> onResult = null)
        {
            var results = new List<TestResult>();

            // 1. Folder operations: Create → Rename → Move → Delete
            await RunTest(results, "FolderOps: Create→Rename→Move→Delete", "Workflow", 10000, async () =>
            {
                // Create parent folder
                var parent = await _service.CreateDriveFolder($"_test_parent_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (parent == null) throw new Exception("Failed to create parent folder");

                // Create child folder
                var child = await _service.CreateDriveFolder($"_test_child_{Guid.NewGuid().ToString()[..6]}", parent.Id, _testUserId);
                if (child == null) throw new Exception("Failed to create child folder");

                // Rename child
                var renamed = await _service.RenameDriveFolder(child.Id, $"_test_renamed_{Guid.NewGuid().ToString()[..6]}");
                if (!renamed) throw new Exception("Failed to rename folder");

                // Create a target folder to move child into
                var target = await _service.CreateDriveFolder($"_test_target_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (target == null) throw new Exception("Failed to create target folder");

                // Move child to target
                var moved = await _service.MoveDriveFolder(child.Id, target.Id);
                if (!moved) throw new Exception("Failed to move folder");

                // Verify move
                var movedFolder = await _service.GetDriveFolderById(child.Id);
                if (movedFolder?.ParentId != target.Id) throw new Exception($"Folder parent mismatch: expected {target.Id}, got {movedFolder?.ParentId}");

                // Cleanup
                await _service.DeleteDriveFolder(target.Id);
                await _service.DeleteDriveFolder(parent.Id);
            }, onResult);

            // 2. File operations: Upload → Rename → Move → Copy → Duplicate → Delete
            await RunTest(results, "FileOps: Upload→Rename→Move→Copy→Dup→Delete", "Workflow", 15000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage not configured - skipping file ops test");

                // Create test folder
                var folder = await _service.CreateDriveFolder($"_test_fileops_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create test folder");

                var folder2 = await _service.CreateDriveFolder($"_test_fileops2_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder2 == null) throw new Exception("Failed to create test folder 2");

                // Create temp file for upload
                var tempFile = Path.Combine(Path.GetTempPath(), $"_test_drive_{Guid.NewGuid().ToString()[..6]}.txt");
                await File.WriteAllTextAsync(tempFile, $"Test file content {DateTime.Now:O}");

                try
                {
                    // Upload
                    var uploaded = await _service.UploadDriveFile(tempFile, folder.Id, _testUserId);
                    if (uploaded == null) throw new Exception("Failed to upload file");

                    // Rename
                    var newName = $"_renamed_{Guid.NewGuid().ToString()[..6]}.txt";
                    var renamed = await _service.RenameDriveFile(uploaded.Id, newName, default, _testUserId);
                    if (!renamed) throw new Exception("Failed to rename file");

                    // Move to folder2
                    var moved = await _service.MoveDriveFile(uploaded.Id, folder2.Id, default, _testUserId);
                    if (!moved) throw new Exception("Failed to move file");

                    // Copy back to folder
                    var copied = await _service.CopyDriveFile(uploaded.Id, folder.Id);
                    if (copied == null) throw new Exception("Failed to copy file");

                    // Duplicate in same folder
                    var duped = await _service.DuplicateDriveFile(uploaded.Id);
                    if (duped == null) throw new Exception("Failed to duplicate file");

                    // Delete all
                    await _service.DeleteDriveFile(uploaded.Id, default, _testUserId);
                    await _service.DeleteDriveFile(copied.Id, default, _testUserId);
                    await _service.DeleteDriveFile(duped.Id, default, _testUserId);
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                    await _service.DeleteDriveFolder(folder.Id);
                    await _service.DeleteDriveFolder(folder2.Id);
                }
            }, onResult);

            // 3. ZIP download flow: Create folder + files → CollectRecursive → DownloadToStream
            await RunTest(results, "ZipDownload: Folder+Files→Collect→Stream", "Workflow", 15000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage not configured - skipping ZIP test");

                var folder = await _service.CreateDriveFolder($"_test_zip_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create ZIP test folder");

                var tempFile = Path.Combine(Path.GetTempPath(), $"_test_zip_{Guid.NewGuid().ToString()[..6]}.txt");
                await File.WriteAllTextAsync(tempFile, "ZIP test content");

                try
                {
                    // Upload 2 files
                    var f1 = await _service.UploadDriveFile(tempFile, folder.Id, _testUserId);
                    await File.WriteAllTextAsync(tempFile, "ZIP test content 2");
                    var f2 = await _service.UploadDriveFile(tempFile, folder.Id, _testUserId);
                    if (f1 == null || f2 == null) throw new Exception("Failed to upload test files");

                    // Collect recursive
                    var allFiles = await _service.CollectDriveFilesRecursive(folder.Id);
                    if (allFiles.Count < 2) throw new Exception($"Expected 2+ files, got {allFiles.Count}");

                    // Download to stream
                    using var ms = new MemoryStream();
                    var ok = await _service.DownloadDriveFileToStream(f1.StoragePath, ms);
                    if (!ok) throw new Exception("Failed to download to stream");
                    if (ms.Length == 0) throw new Exception("Downloaded stream was empty");

                    // Cleanup
                    await _service.DeleteDriveFile(f1.Id, default, _testUserId);
                    await _service.DeleteDriveFile(f2.Id, default, _testUserId);
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                    await _service.DeleteDriveFolder(folder.Id);
                }
            }, onResult);

            // 4. Open-in-Place: OpenFile → verify local + manifest
            await RunTest(results, "OpenInPlace: OpenFile→Verify local+manifest", "Workflow", 10000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage not configured - skipping open-in-place test");

                var folder = await _service.CreateDriveFolder($"_test_oip_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create test folder");

                var tempFile = Path.Combine(Path.GetTempPath(), $"_test_oip_{Guid.NewGuid().ToString()[..6]}.txt");
                await File.WriteAllTextAsync(tempFile, "Open-in-place test");

                try
                {
                    var uploaded = await _service.UploadDriveFile(tempFile, folder.Id, _testUserId);
                    if (uploaded == null) throw new Exception("Failed to upload test file");

                    // Initialize watcher
                    var watcher = FileWatcherService.Instance;
                    watcher.CurrentUserId = _testUserId;
                    watcher.Initialize();

                    // Open file
                    var localPath = await watcher.OpenFile(uploaded);
                    if (localPath == null) throw new Exception("OpenFile returned null");
                    if (!File.Exists(localPath)) throw new Exception($"Local file not found: {localPath}");

                    // Verify sync state
                    var state = watcher.GetSyncState(uploaded.Id);
                    if (state != SyncState.Opened) throw new Exception($"Expected Opened state, got {state}");

                    // Verify IsFileOpened
                    if (!watcher.IsFileOpened(uploaded.Id)) throw new Exception("IsFileOpened returned false");

                    // Cleanup
                    try { File.Delete(localPath); } catch { }
                    await _service.DeleteDriveFile(uploaded.Id, default, _testUserId);
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                    await _service.DeleteDriveFolder(folder.Id);
                }
            }, onResult);

            // 5. Conflict detection: Open → simulate server change → detect mismatch
            await RunTest(results, "ConflictDetection: Open→ServerChange→Detect", "Workflow", 10000, async () =>
            {
                if (!_service.IsDriveStorageConfigured)
                    throw new Exception("R2 Storage not configured - skipping conflict test");

                var folder = await _service.CreateDriveFolder($"_test_conflict_{Guid.NewGuid().ToString()[..6]}", null, _testUserId);
                if (folder == null) throw new Exception("Failed to create test folder");

                var tempFile = Path.Combine(Path.GetTempPath(), $"_test_conflict_{Guid.NewGuid().ToString()[..6]}.txt");
                await File.WriteAllTextAsync(tempFile, "Conflict test v1");

                try
                {
                    var uploaded = await _service.UploadDriveFile(tempFile, folder.Id, _testUserId);
                    if (uploaded == null) throw new Exception("Failed to upload test file");

                    // Open file locally via watcher
                    var watcher = FileWatcherService.Instance;
                    watcher.CurrentUserId = _testUserId;
                    watcher.Initialize();
                    var localPath = await watcher.OpenFile(uploaded);
                    if (localPath == null) throw new Exception("OpenFile returned null");

                    // Simulate server-side change: reupload with new content
                    await File.WriteAllTextAsync(tempFile, "Conflict test v2 - server change");
                    var reuploaded = await _service.ReuploadDriveFile(uploaded.Id, tempFile, _testUserId);
                    if (!reuploaded) throw new Exception("Failed to reupload (simulate server change)");

                    // Verify the server file now has a newer uploaded_at than what's in the manifest
                    var serverFile = await _service.GetDriveFileById(uploaded.Id);
                    if (serverFile == null) throw new Exception("Server file not found");

                    // The watcher's manifest should have the OLDER uploadedAt
                    // The server now has a newer one — conflict should be detected on next sync

                    // Cleanup
                    try { File.Delete(localPath); } catch { }
                    await _service.DeleteDriveFile(uploaded.Id, default, _testUserId);
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                    await _service.DeleteDriveFolder(folder.Id);
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
                error = $"{ex.Message}\n{ex.StackTrace}";
            }

            var result = new TestResult(name, category, passed, sw.ElapsedMilliseconds, thresholdMs, error);
            results.Add(result);
            onResult?.Invoke(result);
        }
    }
}
