using Supabase;
using SistemaGestionProyectos2.Models.Database;
using SistemaGestionProyectos2.Services.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaGestionProyectos2.Services.Storage
{
    public class StorageService : BaseSupabaseService
    {
        private const string BucketName = "order-files";

        public StorageService(Supabase.Client supabaseClient) : base(supabaseClient)
        {
        }

        public async Task<OrderFileDb> UploadFile(string localFilePath, int orderId, int uploadedBy, int? vendorId = null, int? commissionId = null)
        {
            var fileName = Path.GetFileName(localFilePath);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var storagePath = $"{orderId}/{timestamp}_{fileName}";
            var contentType = GetContentType(fileName);
            var fileInfo = new FileInfo(localFilePath);

            LogDebug($"Uploading {fileName} to {storagePath}...");

            var fileOptions = new Supabase.Storage.FileOptions
            {
                ContentType = contentType
            };

            await SupabaseClient.Storage.From(BucketName).Upload(localFilePath, storagePath, fileOptions);

            LogSuccess($"File uploaded: {storagePath}");

            var orderFile = new OrderFileDb
            {
                OrderId = orderId,
                FileName = fileName,
                StoragePath = storagePath,
                FileSize = fileInfo.Length,
                ContentType = contentType,
                UploadedBy = uploadedBy,
                VendorId = vendorId,
                CommissionId = commissionId
            };

            var result = await SupabaseClient
                .From<OrderFileDb>()
                .Insert(orderFile);

            var inserted = result?.Models?.FirstOrDefault();
            if (inserted != null)
            {
                LogSuccess($"DB record created: order_files.id={inserted.Id}");
            }

            return inserted;
        }

        public async Task<byte[]> DownloadFile(string storagePath)
        {
            LogDebug($"Downloading {storagePath}...");
            var bytes = await SupabaseClient.Storage.From(BucketName).Download(storagePath, null);
            return bytes;
        }

        public async Task<string> GetSignedUrl(string storagePath, int expiresInSeconds = 3600)
        {
            var url = await SupabaseClient.Storage.From(BucketName).CreateSignedUrl(storagePath, expiresInSeconds);
            return url;
        }

        public async Task<List<OrderFileDb>> GetFilesByOrder(int orderId)
        {
            var response = await SupabaseClient
                .From<OrderFileDb>()
                .Where(f => f.OrderId == orderId)
                .Order("created_at", Postgrest.Constants.Ordering.Descending)
                .Get();

            return response?.Models ?? new List<OrderFileDb>();
        }

        public async Task<List<OrderFileDb>> GetFilesByCommission(int commissionId)
        {
            var response = await SupabaseClient
                .From<OrderFileDb>()
                .Where(f => f.CommissionId == commissionId)
                .Order("created_at", Postgrest.Constants.Ordering.Descending)
                .Get();

            return response?.Models ?? new List<OrderFileDb>();
        }

        public async Task<bool> DeleteFile(int fileId, string storagePath)
        {
            try
            {
                await SupabaseClient.Storage.From(BucketName).Remove(new List<string> { storagePath });

                await SupabaseClient
                    .From<OrderFileDb>()
                    .Where(f => f.Id == fileId)
                    .Delete();

                LogSuccess($"File deleted: {storagePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error deleting file: {storagePath}", ex);
                return false;
            }
        }

        public async Task<int> GetFileCountByCommission(int commissionId)
        {
            var files = await GetFilesByCommission(commissionId);
            return files.Count;
        }

        public async Task<Dictionary<int, int>> GetFileCountsByCommissions(List<int> commissionIds)
        {
            if (commissionIds == null || commissionIds.Count == 0)
                return new Dictionary<int, int>();

            var response = await SupabaseClient
                .From<OrderFileDb>()
                .Filter("commission_id", Postgrest.Constants.Operator.In, commissionIds)
                .Get();

            var files = response?.Models ?? new List<OrderFileDb>();
            return files
                .Where(f => f.CommissionId.HasValue)
                .GroupBy(f => f.CommissionId.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        public static bool IsImageFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif";
        }

        public static string FormatFileSize(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value == 0) return "0 B";
            var sizes = new[] { "B", "KB", "MB", "GB" };
            var i = (int)Math.Floor(Math.Log(bytes.Value) / Math.Log(1024));
            if (i >= sizes.Length) i = sizes.Length - 1;
            return $"{bytes.Value / Math.Pow(1024, i):F1} {sizes[i]}";
        }
    }
}
