using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Options;
using CloudflareR2.NET.Configuration;
using CloudflareR2.NET;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CMSTaskApp.Store
{
    public interface IR2StorageService
    {
        Task<string> UploadFromStreamAsync(Stream fileStream, string weburlpath, string zoonename = "draw");
        Task<string> UploadFromUrlAsync(string imagelocalpath, string zoonename = "draw");
    }

    public class R2StorageService : IR2StorageService
    {
        private readonly IOptions<CloudflareR2Options> _r2Options;
        private readonly ILogger<CloudflareR2Client> _logger;
        private readonly HttpClient _httpClient;

        public R2StorageService(
            IOptions<CloudflareR2Options> r2Options,
            ILogger<CloudflareR2Client> logger,
            HttpClient httpClient)
        {
            _r2Options = r2Options;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<string> UploadFromStreamAsync(Stream fileStream, string weburlpath, string zoonename = "draw")
        {
            try
            {
                var client = new CloudflareR2Client(_r2Options, _logger);

                var ext = Path.GetExtension(weburlpath);
                var filename = Guid.NewGuid().ToString() + ext;
                var mime = GetMimeType(ext);
                var blobUrl = await client.UploadBlobAsync(fileStream, $"{zoonename}/{filename}", new CancellationToken(), mime);

                if (!string.IsNullOrWhiteSpace(blobUrl))
                {
                    return $"{_r2Options.Value.HostUrl}/{zoonename}/{filename}";
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"上传流时发生错误: {ex.Message}");
                return "";
            }
        }

        public async Task<string> UploadFromUrlAsync(string imagelocalpath, string zoonename = "draw")
        {
            var client = new CloudflareR2Client(_r2Options, _logger);

            using var fileStream = await GetImageStreamFromUrlAsync(imagelocalpath);
            if (fileStream == null)
            {
                return string.Empty;
            }

            var ext = Path.GetExtension(new Uri(imagelocalpath).LocalPath);
            var filename = Guid.NewGuid().ToString() + ext;
            var mime = GetMimeType(ext);
            var blobUrl = await client.UploadBlobAsync(fileStream, $"{zoonename}/{filename}", new CancellationToken(), mime);

            if (!string.IsNullOrWhiteSpace(blobUrl))
            {
                return $"{_r2Options.Value.HostUrl}/{zoonename}/{filename}";
            }
            return string.Empty;
        }

        private static string GetMimeType(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        private async Task<Stream> GetImageStreamFromUrlAsync(string imageUrl)
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(200);
                var response = await _httpClient.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError($"HTTP 请求错误: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"发生错误: {ex.Message}");
                return null;
            }
        }
    }

    // 保持向后兼容的静态类
    public class ClientUsage
    {
        private static IServiceProvider _serviceProvider;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static async Task<string> RunAsync(Stream fileStream, string weburlpath)
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ClientUsage 尚未初始化，请先调用 Initialize 方法");

            var r2Service = _serviceProvider.GetRequiredService<IR2StorageService>();
            return await r2Service.UploadFromStreamAsync(fileStream, weburlpath);
        }

        public static async Task<string> RunAsync(string imagelocalpath)
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ClientUsage 尚未初始化，请先调用 Initialize 方法");

            var r2Service = _serviceProvider.GetRequiredService<IR2StorageService>();
            return await r2Service.UploadFromUrlAsync(imagelocalpath);
        }
    }
}