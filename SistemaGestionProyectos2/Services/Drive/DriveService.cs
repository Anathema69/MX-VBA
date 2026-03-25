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
            try
            {
                // Use RPC with recursive CTE - 1 query instead of N sequential queries
                var result = await SupabaseClient.Rpc("get_folder_breadcrumb_full",
                    new Dictionary<string, object> { { "p_folder_id", folderId } });

                if (result?.Content != null)
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<BreadcrumbRpcResult>>(
                        result.Content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (items != null)
                        return items.Select(i => new DriveFolderDb
                        {
                            Id = i.Id, ParentId = i.Parent_id, Name = i.Name,
                            LinkedOrderId = i.Linked_order_id, CreatedBy = i.Created_by,
                            CreatedAt = i.Created_at, UpdatedAt = i.Updated_at
                        }).ToList();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"RPC breadcrumb fallback to sequential: {ex.Message}");
            }

            // Fallback: sequential queries (pre-RPC behavior)
            var breadcrumb = new List<DriveFolderDb>();
            var current = await GetFolderById(folderId, ct);
            while (current != null)
            {
                breadcrumb.Insert(0, current);
                current = current.ParentId.HasValue ? await GetFolderById(current.ParentId.Value, ct) : null;
            }
            return breadcrumb;
        }

        /// <summary>
        /// Get stats (file_count, subfolder_count, total_size) for all child folders of a parent.
        /// Uses a single SQL RPC instead of 2*N individual queries.
        /// </summary>
        public async Task<Dictionary<int, (int fileCount, int subCount, long totalSize)>> GetFolderStats(int parentId, CancellationToken ct = default)
        {
            try
            {
                var result = await SupabaseClient.Rpc("get_folder_stats",
                    new Dictionary<string, object> { { "p_parent_id", parentId } });

                if (result?.Content != null)
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<FolderStatsRpcResult>>(
                        result.Content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (items != null)
                        return items.ToDictionary(
                            i => i.Folder_id,
                            i => ((int)i.File_count, (int)i.Subfolder_count, i.Total_size));
                }
            }
            catch (Exception ex)
            {
                LogError("Error getting folder stats via RPC", ex);
            }
            return new Dictionary<int, (int fileCount, int subCount, long totalSize)>();
        }

        /// <summary>
        /// Get basic order info for multiple order IDs in a single query.
        /// Returns (f_order, f_po, f_client, f_description).
        /// </summary>
        public async Task<List<OrderInfoRpc>> GetOrdersByIds(List<int> orderIds, CancellationToken ct = default)
        {
            try
            {
                var result = await SupabaseClient.Rpc("get_orders_by_ids",
                    new Dictionary<string, object> { { "p_order_ids", orderIds.ToArray() } });

                if (result?.Content != null)
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<OrderInfoRpc>>(
                        result.Content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<OrderInfoRpc>();
                }
            }
            catch (Exception ex)
            {
                LogError("Error getting orders by IDs via RPC", ex);
            }
            return new List<OrderInfoRpc>();
        }

        // RPC result DTOs (internal, only used for deserialization)
        internal class BreadcrumbRpcResult
        {
            public int Id { get; set; }
            public int? Parent_id { get; set; }
            public string Name { get; set; } = "";
            public int? Linked_order_id { get; set; }
            public int? Created_by { get; set; }
            public DateTime? Created_at { get; set; }
            public DateTime? Updated_at { get; set; }
            public int Depth { get; set; }
        }

        internal class FolderStatsRpcResult
        {
            public int Folder_id { get; set; }
            public long File_count { get; set; }
            public long Subfolder_count { get; set; }
            public long Total_size { get; set; }
        }

        internal class SearchResultRpc
        {
            public string Result_type { get; set; } = "";
            public int Id { get; set; }
            public int? Parent_id { get; set; }
            public int? Folder_id { get; set; }
            public string Name { get; set; } = "";
            public int? Linked_order_id { get; set; }
            public long? File_size { get; set; }
            public string? Content_type { get; set; }
            public int? Uploaded_by { get; set; }
            public DateTime? Uploaded_at { get; set; }
            public string? Storage_path { get; set; }
            public DateTime? Created_at { get; set; }
        }

        public class FolderTreeItem
        {
            public int Id { get; set; }
            public int? Parent_id { get; set; }
            public string Name { get; set; } = "";
            public int? Linked_order_id { get; set; }
        }

        public class OrderInfoRpc
        {
            public int F_order { get; set; }
            public string? F_po { get; set; }
            public int? F_client { get; set; }
            public string? F_description { get; set; }
        }

        // ===============================================
        // VINCULACION CON ORDENES
        // ===============================================

        /// <summary>
        /// Validates whether a folder can be linked to an order.
        /// Returns (canLink, blockReason, warningMessage).
        /// R2: blocked if an ancestor is already linked.
        /// R3: blocked if a descendant is already linked.
        /// R5: warning if folder has subcarpetas (they'll be owned by this order).
        /// </summary>
        public async Task<FolderLinkValidation> ValidateFolderLink(int folderId, CancellationToken ct = default)
        {
            try
            {
                var result = await SupabaseClient.Rpc("validate_folder_link",
                    new Dictionary<string, object> { { "p_folder_id", folderId } });

                if (result?.Content != null)
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<FolderLinkValidationRpc>>(
                        result.Content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var item = items?.FirstOrDefault();
                    if (item != null)
                        return new FolderLinkValidation(
                            item.Can_link, item.Block_reason, item.Warning_message,
                            item.Descendant_folder_count, item.Linked_descendant_count);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"ValidateFolderLink RPC failed, allowing link: {ex.Message}");
            }

            // Fallback: allow (fail-open so linking doesn't break if RPC isn't deployed yet)
            return new FolderLinkValidation(true, null, null, 0, 0);
        }

        public record FolderLinkValidation(
            bool CanLink, string? BlockReason, string? WarningMessage,
            int DescendantFolderCount, int LinkedDescendantCount);

        internal class FolderLinkValidationRpc
        {
            public bool Can_link { get; set; }
            public string? Block_reason { get; set; }
            public string? Warning_message { get; set; }
            public int Descendant_folder_count { get; set; }
            public int Linked_descendant_count { get; set; }
        }

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

                // Postgrest In filter needs a list of objects, not List<int>
                var idList = orderIds.Select(id => (object)id).ToList();
                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Filter("linked_order_id", Postgrest.Constants.Operator.In, idList)
                    .Get();

                var folders = response?.Models ?? new List<DriveFolderDb>();
                // Use first folder per order (handles 1:N edge cases like Rack V2.0/V2.1 → same order)
                var result = new Dictionary<int, int>();
                foreach (var f in folders.Where(f => f.LinkedOrderId.HasValue))
                    result.TryAdd(f.LinkedOrderId.Value, f.Id);

                LogDebug($"GetLinkedFolderIds: {orderIds.Count} orders queried, {result.Count} linked found");
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error getting linked folder IDs (count={orderIds?.Count})", ex);
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
            {
                LogSuccess($"DB record created: drive_files.id={inserted.Id}");
                _ = LogActivity(userId, "upload", "file", inserted.Id, fileName, folderId, ct,
                    $"{{\"file_size\":{fileInfo.Length},\"content_type\":\"{contentType}\"}}");

            }

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

        public async Task<bool> RenameFile(int fileId, string newName, CancellationToken ct = default, int? userId = null)
        {
            try
            {
                var file = await GetFileById(fileId, ct);
                if (file == null) return false;

                var oldName = file.FileName;
                file.FileName = newName.Trim();
                await SupabaseClient
                    .From<DriveFileDb>()
                    .Where(f => f.Id == fileId)
                    .Update(file);

                LogSuccess($"File renamed: id={fileId} -> '{newName}'");
                _ = LogActivity(userId, "rename", "file", fileId, newName, file.FolderId, ct,
                    $"{{\"old_name\":\"{oldName}\"}}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error renaming file {fileId}", ex);
                return false;
            }
        }

        public async Task<bool> DeleteFile(int fileId, CancellationToken ct = default, int? userId = null)
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
                _ = LogActivity(userId, "delete", "file", fileId, file.FileName, file.FolderId, ct,
                    $"{{\"file_size\":{file.FileSize ?? 0}}}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error deleting file {fileId}", ex);
                return false;
            }
        }

        // ===============================================
        // MOVE / COPY / DUPLICATE (V3-C)
        // ===============================================

        public async Task<bool> MoveFile(int fileId, int targetFolderId, CancellationToken ct = default, int? userId = null)
        {
            try
            {
                var file = await GetFileById(fileId, ct);
                if (file == null) return false;
                var fromFolder = file.FolderId;
                file.FolderId = targetFolderId;
                await SupabaseClient.From<DriveFileDb>().Where(f => f.Id == fileId).Update(file);
                LogSuccess($"File moved: id={fileId} -> folder={targetFolderId}");
                _ = LogActivity(userId, "move", "file", fileId, file.FileName, targetFolderId, ct,
                    $"{{\"from_folder\":{fromFolder},\"to_folder\":{targetFolderId}}}");
                return true;
            }
            catch (Exception ex) { LogError($"Error moving file {fileId}", ex); return false; }
        }

        public async Task<bool> MoveFolder(int folderId, int targetParentId, CancellationToken ct = default)
        {
            try
            {
                var folder = await GetFolderById(folderId, ct);
                if (folder == null) return false;
                folder.ParentId = targetParentId;
                await SupabaseClient.From<DriveFolderDb>().Where(f => f.Id == folderId).Update(folder);
                LogSuccess($"Folder moved: id={folderId} -> parent={targetParentId}");
                return true;
            }
            catch (Exception ex) { LogError($"Error moving folder {folderId}", ex); return false; }
        }

        public async Task<(bool canMove, string? reason)> ValidateFolderMove(int folderId, int targetId, CancellationToken ct = default)
        {
            try
            {
                var result = await SupabaseClient.Rpc("validate_folder_move",
                    new Dictionary<string, object> { { "p_folder_id", folderId }, { "p_target_id", targetId } });
                if (result?.Content == null) return (false, "Error de validacion");
                var arr = System.Text.Json.JsonSerializer.Deserialize<List<FolderMoveValidation>>(result.Content);
                var first = arr?.FirstOrDefault();
                return first != null ? (first.can_move, first.block_reason) : (false, "Sin respuesta");
            }
            catch (Exception ex) { LogError("Error validating folder move", ex); return (false, ex.Message); }
        }

        private record FolderMoveValidation(bool can_move, string? block_reason);

        public async Task<DriveFileDb?> CopyFile(int fileId, int targetFolderId, CancellationToken ct = default)
        {
            if (!_isStorageConfigured) return null;
            try
            {
                var source = await GetFileById(fileId, ct);
                if (source == null) return null;

                // Generate unique name in target folder
                var targetName = await GetUniqueName(targetFolderId, source.FileName, ct);

                // Copy blob in R2 (R2 no soporta CopyObject, download+reupload)
                var newStoragePath = $"{targetFolderId}/{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{targetName}";
                using var getResp = await _s3Client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = source.StoragePath
                }, ct);
                using var ms = new MemoryStream();
                await getResp.ResponseStream.CopyToAsync(ms, ct);
                ms.Position = 0;
                await _s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = newStoragePath,
                    InputStream = ms,
                    ContentType = source.ContentType ?? GetContentType(source.FileName),
                    DisablePayloadSigning = true
                }, ct);

                // Insert new DB record
                var newFile = new DriveFileDb
                {
                    FolderId = targetFolderId,
                    FileName = targetName,
                    StoragePath = newStoragePath,
                    FileSize = source.FileSize,
                    ContentType = source.ContentType,
                    UploadedBy = source.UploadedBy,
                    UploadedAt = DateTime.Now
                };
                var resp = await SupabaseClient.From<DriveFileDb>().Insert(newFile);
                var inserted = resp?.Models?.FirstOrDefault();
                LogSuccess($"File copied: {source.FileName} -> folder={targetFolderId} as '{targetName}'");
                if (inserted != null) _ = LogActivity(source.UploadedBy, "copy", "file", inserted.Id, targetName, targetFolderId, ct,
                    $"{{\"source_id\":{fileId},\"source_name\":\"{source.FileName}\"}}");

                return inserted;
            }
            catch (Exception ex) { LogError($"Error copying file {fileId}", ex); return null; }
        }

        public async Task<DriveFileDb?> DuplicateFile(int fileId, CancellationToken ct = default)
        {
            var source = await GetFileById(fileId, ct);
            if (source == null) return null;
            return await CopyFile(fileId, source.FolderId, ct);
        }

        private async Task<string> GetUniqueName(int folderId, string fileName, CancellationToken ct)
        {
            var existing = await GetFilesByFolder(folderId, ct);
            var names = new HashSet<string>(existing.Select(f => f.FileName), StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(fileName)) return fileName;

            var nameWithout = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            for (int i = 1; i < 100; i++)
            {
                var candidate = $"{nameWithout} (copia{(i > 1 ? $" {i}" : "")}){ext}";
                if (!names.Contains(candidate)) return candidate;
            }
            return $"{nameWithout} ({Guid.NewGuid().ToString()[..6]}){ext}";
        }

        public async Task<List<DriveFolderDb>> GetAllFoldersFlat(CancellationToken ct = default)
        {
            try
            {
                var result = await SupabaseClient.From<DriveFolderDb>()
                    .Order("name", Postgrest.Constants.Ordering.Ascending)
                    .Get(ct);
                return result.Models ?? new List<DriveFolderDb>();
            }
            catch (Exception ex) { LogError("Error getting all folders", ex); return new List<DriveFolderDb>(); }
        }

        // ===============================================
        // ===============================================
        // ACTIVITY LOG (V3-B)
        // ===============================================

        public async Task LogActivity(int? userId, string action, string targetType, int targetId, string? targetName, int? folderId, CancellationToken ct = default, string? metadata = null)
        {
            try
            {
                var activity = new DriveActivityDb
                {
                    UserId = userId,
                    Action = action,
                    TargetType = targetType,
                    TargetId = targetId,
                    TargetName = targetName,
                    FolderId = folderId,
                    Metadata = metadata,
                    CreatedAt = DateTime.Now
                };
                await SupabaseClient.From<DriveActivityDb>().Insert(activity);
            }
            catch (Exception ex) { LogError($"Error logging activity: {action} {targetType} {targetId}", ex); }
        }

        public async Task<List<DriveFileDb>> GetRecentFiles(int limit = 15, CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFileDb>()
                    .Order("uploaded_at", Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Get(ct);
                return response?.Models ?? new List<DriveFileDb>();
            }
            catch (Exception ex) { LogError("Error getting recent files", ex); return new List<DriveFileDb>(); }
        }

        public async Task<List<DriveActivityDb>> GetRecentActivity(int limit = 10, int? userId = null, CancellationToken ct = default)
        {
            try
            {
                Postgrest.Responses.ModeledResponse<DriveActivityDb> response;
                if (userId.HasValue)
                    response = await SupabaseClient.From<DriveActivityDb>()
                        .Where(a => a.UserId == userId.Value)
                        .Order("created_at", Postgrest.Constants.Ordering.Descending)
                        .Limit(limit)
                        .Get(ct);
                else
                    response = await SupabaseClient.From<DriveActivityDb>()
                        .Order("created_at", Postgrest.Constants.Ordering.Descending)
                        .Limit(limit)
                        .Get(ct);
                return response?.Models ?? new List<DriveActivityDb>();
            }
            catch (Exception ex) { LogError("Error getting recent activity", ex); return new List<DriveActivityDb>(); }
        }

        // ===============================================
        // SEARCH (scoped + global)
        // ===============================================

        /// <summary>
        /// Search folders and files within a folder and its descendants.
        /// If folderId is null, searches globally (root).
        /// </summary>
        public async Task<(List<DriveFolderDb> Folders, List<DriveFileDb> Files)> SearchInFolder(int? folderId, string query, CancellationToken ct = default)
        {
            try
            {
                var rpcParams = new Dictionary<string, object> { { "p_query", query } };
                if (folderId.HasValue)
                    rpcParams["p_folder_id"] = folderId.Value;

                var result = await SupabaseClient.Rpc("search_in_folder", rpcParams);

                if (result?.Content != null)
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<SearchResultRpc>>(
                        result.Content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (items != null)
                    {
                        var folders = items.Where(i => i.Result_type == "folder")
                            .Select(i => new DriveFolderDb
                            {
                                Id = i.Id, ParentId = i.Parent_id, Name = i.Name,
                                LinkedOrderId = i.Linked_order_id, CreatedAt = i.Created_at
                            }).ToList();

                        var files = items.Where(i => i.Result_type == "file")
                            .Select(i => new DriveFileDb
                            {
                                Id = i.Id, FolderId = i.Folder_id ?? 0, FileName = i.Name,
                                FileSize = i.File_size, ContentType = i.Content_type,
                                UploadedBy = i.Uploaded_by, UploadedAt = i.Uploaded_at,
                                StoragePath = i.Storage_path
                            }).ToList();

                        return (folders, files);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"SearchInFolder RPC failed, falling back to global: {ex.Message}");
            }

            // Fallback: global search using existing methods
            var fallbackFolders = await SearchFolders(query, ct);
            var fallbackFiles = await SearchFiles(query, ct);
            return (fallbackFolders, fallbackFiles);
        }

        /// <summary>
        /// Get all folders for building the folder tree (selection dialog).
        /// Returns flat list with id, parent_id, name, linked_order_id.
        /// </summary>
        public async Task<List<FolderTreeItem>> GetFolderTree(CancellationToken ct = default)
        {
            try
            {
                var result = await SupabaseClient.Rpc("get_folder_tree", new Dictionary<string, object>());

                if (result?.Content != null)
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<FolderTreeItem>>(
                        result.Content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new List<FolderTreeItem>();
                }
            }
            catch (Exception ex)
            {
                LogError("Error getting folder tree via RPC", ex);
            }

            // Fallback: load all folders via Postgrest
            try
            {
                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Order("name", Postgrest.Constants.Ordering.Ascending)
                    .Get();

                return (response?.Models ?? new List<DriveFolderDb>())
                    .Select(f => new FolderTreeItem
                    {
                        Id = f.Id, Parent_id = f.ParentId, Name = f.Name,
                        Linked_order_id = f.LinkedOrderId
                    }).ToList();
            }
            catch (Exception ex)
            {
                LogError("Error getting folder tree fallback", ex);
                return new List<FolderTreeItem>();
            }
        }

        public async Task<List<DriveFolderDb>> SearchFolders(string query, CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFolderDb>()
                    .Filter("name", Postgrest.Constants.Operator.ILike, $"%{query}%")
                    .Order("name", Postgrest.Constants.Ordering.Ascending)
                    .Limit(30)
                    .Get();
                return response?.Models ?? new List<DriveFolderDb>();
            }
            catch (Exception ex) { LogError("Error searching folders", ex); return new List<DriveFolderDb>(); }
        }

        public async Task<List<DriveFileDb>> SearchFiles(string query, CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFileDb>()
                    .Filter("file_name", Postgrest.Constants.Operator.ILike, $"%{query}%")
                    .Order("uploaded_at", Postgrest.Constants.Ordering.Descending)
                    .Limit(50)
                    .Get();
                return response?.Models ?? new List<DriveFileDb>();
            }
            catch (Exception ex) { LogError("Error searching files", ex); return new List<DriveFileDb>(); }
        }

        /// <summary>
        /// Get total storage used across ALL drive files (global, not per-folder).
        /// </summary>
        public async Task<long> GetTotalStorageBytes(CancellationToken ct = default)
        {
            try
            {
                var response = await SupabaseClient
                    .From<DriveFileDb>()
                    .Select("file_size")
                    .Get();

                var files = response?.Models ?? new List<DriveFileDb>();
                return files.Sum(f => f.FileSize ?? 0);
            }
            catch (Exception ex)
            {
                LogError("Error getting total storage", ex);
                return -1;
            }
        }

        // ===============================================
        // REUPLOAD (V3-E: Open-in-Place auto-sync)
        // ===============================================

        /// <summary>
        /// Overwrite an existing file's blob in R2 and update DB metadata.
        /// Used by FileWatcherService for auto-sync after local edits.
        /// </summary>
        public async Task<bool> ReuploadFile(int fileId, string localPath, int userId, CancellationToken ct = default)
        {
            if (!_isStorageConfigured)
                throw new InvalidOperationException("R2 Storage no esta configurado.");

            try
            {
                var file = await GetFileById(fileId, ct);
                if (file == null) { LogError($"ReuploadFile: file {fileId} not found"); return false; }

                var fileInfo = new FileInfo(localPath);
                if (!fileInfo.Exists) { LogError($"ReuploadFile: local file not found: {localPath}"); return false; }

                // Overwrite blob in R2 (same key)
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = file.StoragePath,
                    FilePath = localPath,
                    ContentType = file.ContentType ?? GetContentType(file.FileName),
                    DisablePayloadSigning = true
                };
                await _s3Client.PutObjectAsync(putRequest, ct);

                // Update DB: file_size, uploaded_at
                file.FileSize = fileInfo.Length;
                file.UploadedAt = DateTime.Now;
                await SupabaseClient.From<DriveFileDb>().Where(f => f.Id == fileId).Update(file);

                LogSuccess($"File reuploaded: {file.FileName} (id={fileId}, size={fileInfo.Length})");
                _ = LogActivity(userId, "reupload", "file", fileId, file.FileName, file.FolderId, ct,
                    $"{{\"file_size\":{fileInfo.Length}}}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error reuploading file {fileId}", ex);
                return false;
            }
        }

        // ===============================================
        // HELPERS
        // ===============================================

        public async Task<DriveFileDb> GetFileById(int fileId, CancellationToken ct = default)
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

        // ===============================================
        // ZIP DOWNLOAD & HELPERS (V3-D)
        // ===============================================

        /// <summary>Collect all files recursively from a folder (for ZIP download)</summary>
        public async Task<List<DriveFileDb>> CollectAllFilesRecursive(int folderId, CancellationToken ct = default)
        {
            var result = new List<DriveFileDb>();
            try
            {
                var files = await GetFilesByFolder(folderId, ct);
                result.AddRange(files);

                var subfolders = await GetChildFolders(folderId, ct);
                foreach (var sub in subfolders)
                {
                    var subFiles = await CollectAllFilesRecursive(sub.Id, ct);
                    result.AddRange(subFiles);
                }
            }
            catch (Exception ex) { LogError($"Error collecting files from folder {folderId}", ex); }
            return result;
        }

        /// <summary>Download a file to a stream (for ZIP creation)</summary>
        public async Task<bool> DownloadFileToStream(string storagePath, Stream destination, CancellationToken ct = default)
        {
            if (!_isStorageConfigured) return false;
            try
            {
                var getRequest = new GetObjectRequest { BucketName = _bucketName, Key = storagePath };
                using var response = await _s3Client.GetObjectAsync(getRequest, ct);
                await response.ResponseStream.CopyToAsync(destination, ct);
                return true;
            }
            catch (Exception ex) { LogError($"Error downloading to stream: {storagePath}", ex); return false; }
        }

        // ===============================================
        // PREVIEW & THUMBNAILS (V3-A)
        // ===============================================

        /// <summary>Generate a pre-signed URL for file preview (default 15 min)</summary>
        public string? GetPreviewUrl(string storagePath, int expirationMinutes = 15)
        {
            if (!_isStorageConfigured || string.IsNullOrEmpty(storagePath)) return null;
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = storagePath,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    Verb = HttpVerb.GET
                };
                return _s3Client.GetPreSignedURL(request);
            }
            catch (Exception ex)
            {
                LogError("Error generating preview URL", ex);
                return null;
            }
        }

        /// <summary>Download partial file content (for text preview, first N bytes)</summary>
        public async Task<byte[]?> DownloadFilePartial(int fileId, long maxBytes = 102400, CancellationToken ct = default)
        {
            if (!_isStorageConfigured) return null;
            try
            {
                var file = await GetFileById(fileId, ct);
                if (file == null) return null;

                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = file.StoragePath,
                    ByteRange = new ByteRange(0, maxBytes - 1)
                };

                using var response = await _s3Client.GetObjectAsync(getRequest, ct);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                LogError($"Error downloading partial file {fileId}", ex);
                return null;
            }
        }

        /// <summary>Check if file extension is a text-previewable type</summary>
        public static bool IsTextFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".txt" or ".csv" or ".log" or ".json" or ".xml" or ".md" or ".ini" or ".cfg" or ".html" or ".htm" or ".css" or ".js" or ".sql";
        }

        /// <summary>Check if file extension is an Office document</summary>
        public static bool IsOfficeFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx";
        }

        /// <summary>Check if file is a CAD assembly (references other part files)</summary>
        public static bool IsAssemblyFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".iam" or ".sldasm";
        }

        /// <summary>Check if file extension is a CAD file (includes CNC/Mastercam)</summary>
        public static bool IsCadFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".dwg" or ".dxf" or ".step" or ".stp" or ".igs"
                or ".sldprt" or ".sldasm" or ".ipt" or ".iam"
                or ".mcam" or ".mcx-5" or ".mcx-7" or ".mcx-9";
        }

        public static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" or ".jfif" => "image/jpeg",
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
                // CAD
                ".dwg" => "application/acad",
                ".dxf" => "application/dxf",
                ".step" or ".stp" => "application/step",
                ".igs" => "application/iges",
                ".ipt" => "application/x-inventor-part",
                ".iam" => "application/x-inventor-assembly",
                ".sldprt" => "application/x-solidworks-part",
                ".sldasm" => "application/x-solidworks-assembly",
                // CNC / Mastercam
                ".mcam" or ".mcx-5" or ".mcx-7" or ".mcx-9" => "application/x-mastercam",
                _ => "application/octet-stream"
            };
        }

        public static bool IsImageFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".jfif" or ".png" or ".gif" or ".bmp" or ".webp";
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
                ".jpg" or ".jpeg" or ".jfif" or ".png" or ".gif" or ".bmp" or ".webp" => "\uD83D\uDDBC", // framed picture
                ".zip" or ".rar" => "\uD83D\uDCE6", // package
                ".txt" or ".csv" => "\uD83D\uDCC4", // page
                // CAD: piezas + planos + modelos 3D
                ".dwg" or ".dxf" or ".step" or ".stp" or ".igs" or ".ipt" or ".sldprt" => "\uD83D\uDCD0", // triangular ruler
                // CAD: ensambles (gear emoji for distinction)
                ".iam" or ".sldasm" => "\u2699\uFE0F", // gear
                // CNC Mastercam
                ".mcam" or ".mcx-5" or ".mcx-7" or ".mcx-9" => "\uD83D\uDD27", // wrench
                _ => "\uD83D\uDCC1"                  // folder
            };
        }
    }
}
