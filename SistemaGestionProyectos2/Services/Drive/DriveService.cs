using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using Supabase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Drive
{
    public class DriveService : BaseSupabaseService
    {
        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly bool _isStorageConfigured;

        public bool IsStorageConfigured => _isStorageConfigured;

        public DriveService(Client supabaseClient, string accountId, string accessKeyId, string secretAccessKey, string bucketName = "ima-drive")
            : base(supabaseClient)
        {
            _bucketName = bucketName;

            if (!string.IsNullOrEmpty(accountId) && !string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey))
            {
                var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
                var config = new AmazonS3Config
                {
                    ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                    ForcePathStyle = true
                };
                _s3Client = new AmazonS3Client(credentials, config);
                _isStorageConfigured = true;
                LogSuccess($"R2 Storage configured: bucket={bucketName}");
            }
            else
            {
                _isStorageConfigured = false;
                LogDebug("R2 Storage NOT configured - file upload/download disabled. Configure CloudflareR2 in appsettings.json");
            }
        }

        // ===============================================
        // CARPETAS
        // ===============================================

        public async Task<List<DriveFolderDb>> GetChildFolders(int? parentId, CancellationToken ct = default)
        {
            try
            {
                Postgrest.Responses.ModeledResponse<DriveFolderDb> response;

                if (parentId.HasValue)
                {
                    response = await SupabaseClient
                        .From<DriveFolderDb>()
                        .Where(f => f.ParentId == parentId.Value)
                        .Order("name", Postgrest.Constants.Ordering.Ascending)
                        .Get();
                }
                else
                {
                    response = await SupabaseClient
                        .From<DriveFolderDb>()
                        .Filter("parent_id", Postgrest.Constants.Operator.Is, "null")
                        .Order("name", Postgrest.Constants.Ordering.Ascending)
                        .Get();
                }

                return response?.Models ?? new List<DriveFolderDb>();
            }
            catch (Exception ex)
            {
                LogError("Error getting child folders", ex);
                return new List<DriveFolderDb>();
            }
        }

        public async Task<DriveFolderDb> GetFolderById(int folderId, CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Where(f => f.Id == folderId)
                    .Single();

                return response;
            }
            catch (Exception ex)
            {
                LogError($"Error getting folder {folderId}", ex);
                return null;
            }
        }

        public async Task<DriveFolderDb> CreateFolder(string name, int? parentId, int userId, CancellationToken ct = default)
        {
            try
            {
                var folder = new DriveFolderDb
                {
                    Name = name.Trim(),
                    ParentId = parentId,
                    CreatedBy = userId
                };

                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Insert(folder);

                var created = response?.Models?.FirstOrDefault();
                if (created != null)
                    LogSuccess($"Folder created: {name} (id={created.Id}, parent={parentId})");

                return created;
            }
            catch (Exception ex)
            {
                LogError($"Error creating folder '{name}'", ex);
                return null;
            }
        }

        public async Task<bool> RenameFolder(int folderId, string newName, CancellationToken ct = default)
        {
            try
            {
                var folder = await GetFolderById(folderId, ct);
                if (folder == null) return false;

                folder.Name = newName.Trim();
                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Where(f => f.Id == folderId)
                    .Update(folder);

                LogSuccess($"Folder renamed: id={folderId} -> '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error renaming folder {folderId}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteFolder(int folderId, CancellationToken ct = default)
        {
            try
            {
                // Collect ALL file storage paths recursively before deleting from DB
                var allStoragePaths = new List<string>();
                await CollectAllFilePaths(folderId, allStoragePaths, ct);

                // Delete from DB first (CASCADE handles child folders + file records)
                await SupabaseClient
                    .From<DriveFolderDb>()
                    .Where(f => f.Id == folderId)
                    .Delete();

                LogSuccess($"Folder deleted from DB: id={folderId}");

                // Then batch-delete blobs from R2 (fire-and-forget, don't block on window CancellationToken)
                if (_isStorageConfigured && allStoragePaths.Count > 0)
                {
                    _ = Task.Run(() => BatchDeleteFromR2(allStoragePaths));
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error deleting folder {folderId}", ex);
                return false;
            }
        }

        public async Task<List<DriveFolderDb>> GetBreadcrumb(int folderId, CancellationToken ct = default)
        {
            var breadcrumb = new List<DriveFolderDb>();
            var current = await GetFolderById(folderId, ct);

            while (current != null)
            {
                breadcrumb.Insert(0, current);
                if (current.ParentId.HasValue)
                    current = await GetFolderById(current.ParentId.Value, ct);
                else
                    current = null;
            }

            return breadcrumb;
        }

        // ===============================================
        // VINCULACION CON ORDENES
        // ===============================================

        public async Task<bool> LinkFolderToOrder(int folderId, int orderId, CancellationToken ct = default)
        {
            try
            {
                var folder = await GetFolderById(folderId, ct);
                if (folder == null) return false;

                folder.LinkedOrderId = orderId;
                await SupabaseClient
                    .From<DriveFolderDb>()
                    .Where(f => f.Id == folderId)
                    .Update(folder);

                LogSuccess($"Folder {folderId} linked to order {orderId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error linking folder {folderId} to order {orderId}", ex);
                return false;
            }
        }

        public async Task<bool> UnlinkFolder(int folderId, CancellationToken ct = default)
        {
            try
            {
                // Postgrest doesn't handle setting nullable FK to null via .Update() well,
                // so use raw filter + set approach
                var folder = await GetFolderById(folderId, ct);
                if (folder == null) return false;

                folder.LinkedOrderId = null;
                await SupabaseClient
                    .From<DriveFolderDb>()
                    .Where(f => f.Id == folderId)
                    .Update(folder);

                LogSuccess($"Folder {folderId} unlinked from order");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error unlinking folder {folderId}", ex);
                return false;
            }
        }

        public async Task<DriveFolderDb> GetFolderByOrder(int orderId, CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Where(f => f.LinkedOrderId == orderId)
                    .Single();

                return response;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Dictionary<int, int>> GetLinkedFolderIds(List<int> orderIds, CancellationToken ct = default)
        {
            try
            {
                if (orderIds == null || orderIds.Count == 0)
                    return new Dictionary<int, int>();

                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Filter("linked_order_id", Postgrest.Constants.Operator.In, orderIds)
                    .Get();

                var folders = response?.Models ?? new List<DriveFolderDb>();
                return folders
                    .Where(f => f.LinkedOrderId.HasValue)
                    .ToDictionary(f => f.LinkedOrderId.Value, f => f.Id);
            }
            catch (Exception ex)
            {
                LogError("Error getting linked folder IDs", ex);
                return new Dictionary<int, int>();
            }
        }

        // ===============================================
        // ARCHIVOS
        // ===============================================

        public async Task<List<DriveFileDb>> GetFilesByFolder(int folderId, CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFileDb>()
                    .Where(f => f.FolderId == folderId)
                    .Order("uploaded_at", Postgrest.Constants.Ordering.Descending)
                    .Get();

                return response?.Models ?? new List<DriveFileDb>();
            }
            catch (Exception ex)
            {
                LogError($"Error getting files for folder {folderId}", ex);
                return new List<DriveFileDb>();
            }
        }

        public async Task<DriveFileDb> UploadFile(string localFilePath, int folderId, int userId, CancellationToken ct = default)
        {
            if (!_isStorageConfigured)
                throw new InvalidOperationException("R2 Storage no esta configurado. Configure CloudflareR2 en appsettings.json");

            var fileName = Path.GetFileName(localFilePath);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var storagePath = $"{folderId}/{timestamp}_{fileName}";
            var contentType = GetContentType(fileName);
            var fileInfo = new FileInfo(localFilePath);

            LogDebug($"Uploading {fileName} to R2: {storagePath}...");

            // Upload to R2 (DisablePayloadSigning required - R2 doesn't support STREAMING-AWS4-HMAC-SHA256-PAYLOAD)
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = storagePath,
                FilePath = localFilePath,
                ContentType = contentType,
                DisablePayloadSigning = true
            };

            await _s3Client.PutObjectAsync(putRequest, ct);
            LogSuccess($"File uploaded to R2: {storagePath}");

            // Save metadata to DB
            var driveFile = new DriveFileDb
            {
                FolderId = folderId,
                FileName = fileName,
                StoragePath = storagePath,
                FileSize = fileInfo.Length,
                ContentType = contentType,
                UploadedBy = userId
            };

            var response = await SupabaseClient
                .From<DriveFileDb>()
                .Insert(driveFile);

            var inserted = response?.Models?.FirstOrDefault();
            if (inserted != null)
                LogSuccess($"DB record created: drive_files.id={inserted.Id}");

            return inserted;
        }

        public async Task<byte[]> DownloadFile(int fileId, CancellationToken ct = default)
        {
            if (!_isStorageConfigured)
                throw new InvalidOperationException("R2 Storage no esta configurado.");

            var file = await GetFileById(fileId, ct);
            if (file == null) throw new FileNotFoundException($"File {fileId} not found in DB");

            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = file.StoragePath
            };

            using var response = await _s3Client.GetObjectAsync(getRequest, ct);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }

        public async Task<bool> DownloadFileToLocal(int fileId, string localPath, CancellationToken ct = default)
        {
            try
            {
                var bytes = await DownloadFile(fileId, ct);
                await File.WriteAllBytesAsync(localPath, bytes, ct);
                LogSuccess($"File downloaded to: {localPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error downloading file {fileId}", ex);
                return false;
            }
        }

        public async Task<bool> RenameFile(int fileId, string newName, CancellationToken ct = default)
        {
            try
            {
                var file = await GetFileById(fileId, ct);
                if (file == null) return false;

                file.FileName = newName.Trim();
                await SupabaseClient
                    .From<DriveFileDb>()
                    .Where(f => f.Id == fileId)
                    .Update(file);

                LogSuccess($"File renamed: id={fileId} -> '{newName}'");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error renaming file {fileId}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteFile(int fileId, CancellationToken ct = default)
        {
            try
            {
                var file = await GetFileById(fileId, ct);
                if (file == null) return false;

                // Delete from R2
                if (_isStorageConfigured)
                {
                    var deleteRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = file.StoragePath
                    };
                    await _s3Client.DeleteObjectAsync(deleteRequest, ct);
                }

                // Delete from DB
                await SupabaseClient
                    .From<DriveFileDb>()
                    .Where(f => f.Id == fileId)
                    .Delete();

                LogSuccess($"File deleted: {file.FileName} (id={fileId})");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error deleting file {fileId}", ex);
                return false;
            }
        }

        // ===============================================
        // HELPERS
        // ===============================================

        private async Task<DriveFileDb> GetFileById(int fileId, CancellationToken ct)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFileDb>()
                    .Where(f => f.Id == fileId)
                    .Single();
                return response;
            }
            catch { return null; }
        }

        private async Task CollectAllFilePaths(int folderId, List<string> paths, CancellationToken ct)
        {
            var files = await GetFilesByFolder(folderId, ct);
            paths.AddRange(files.Select(f => f.StoragePath));

            var subfolders = await GetChildFolders(folderId, ct);
            foreach (var sub in subfolders)
            {
                await CollectAllFilePaths(sub.Id, paths, ct);
            }
        }

        private async Task BatchDeleteFromR2(List<string> storagePaths)
        {
            try
            {
                // S3 DeleteObjects supports up to 1000 keys per request
                for (int i = 0; i < storagePaths.Count; i += 1000)
                {
                    var batch = storagePaths.Skip(i).Take(1000).ToList();
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = batch.Select(p => new KeyVersion { Key = p }).ToList()
                    };

                    var response = await _s3Client.DeleteObjectsAsync(deleteRequest);
                    LogSuccess($"R2 batch delete: {response.DeletedObjects.Count} files removed");

                    if (response.DeleteErrors.Count > 0)
                    {
                        foreach (var err in response.DeleteErrors)
                            LogError($"R2 delete error: {err.Key} - {err.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in R2 batch delete ({storagePaths.Count} files)", ex);
            }
        }

        /// <summary>
        /// Purge ALL files from the R2 bucket. Use for cleanup/reset.
        /// </summary>
        public async Task<int> PurgeAllR2Files()
        {
            if (!_isStorageConfigured) return 0;

            try
            {
                var listRequest = new ListObjectsV2Request { BucketName = _bucketName };
                var allKeys = new List<string>();

                ListObjectsV2Response listResponse;
                do
                {
                    listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                    allKeys.AddRange(listResponse.S3Objects.Select(o => o.Key));
                    listRequest.ContinuationToken = listResponse.NextContinuationToken;
                } while (listResponse.IsTruncated);

                if (allKeys.Count == 0) return 0;

                await BatchDeleteFromR2(allKeys);
                LogSuccess($"R2 purge complete: {allKeys.Count} files removed");
                return allKeys.Count;
            }
            catch (Exception ex)
            {
                LogError("Error purging R2 bucket", ex);
                return -1;
            }
        }

        public static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".html" or ".htm" => "text/html",
                ".xml" => "application/xml",
                ".json" => "application/json",
                ".log" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".dwg" => "application/acad",
                ".dxf" => "application/dxf",
                ".step" or ".stp" => "application/step",
                _ => "application/octet-stream"
            };
        }

        public static bool IsImageFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
        }

        public static string FormatFileSize(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value == 0) return "0 B";
            var sizes = new[] { "B", "KB", "MB", "GB" };
            var i = (int)Math.Floor(Math.Log(bytes.Value) / Math.Log(1024));
            if (i >= sizes.Length) i = sizes.Length - 1;
            return $"{bytes.Value / Math.Pow(1024, i):F1} {sizes[i]}";
        }

        public static string GetFileIcon(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "\uD83D\uDCC4",          // page
                ".doc" or ".docx" => "\uD83D\uDCC3", // page with curl
                ".xls" or ".xlsx" => "\uD83D\uDCCA", // bar chart
                ".ppt" or ".pptx" => "\uD83D\uDCCA", // bar chart
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\uD83D\uDDBC", // framed picture
                ".zip" or ".rar" => "\uD83D\uDCE6", // package
                ".txt" or ".csv" => "\uD83D\uDCC4", // page
                ".dwg" or ".dxf" or ".step" or ".stp" => "\uD83D\uDCD0", // triangular ruler
                _ => "\uD83D\uDCC1"                  // folder
            };
        }
    }
}
