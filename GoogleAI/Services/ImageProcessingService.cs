using CMSTaskApp.Store;
using GoogleAI.Configuration;
using GoogleAI.Models;
using GoogleAI.Repositories;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace GoogleAI.Services
{
    /// <summary>
    /// 图片处理后台服务 - 负责图片压缩和R2上传
    /// </summary>
    public class ImageProcessingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ImageProcessingService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _environment;
        private readonly IR2StorageService _r2StorageService;
        private readonly IImageProcessingQueue _imageProcessingQueue;
        private readonly TaskProcessorSettings _settings;

        public ImageProcessingService(
            IServiceProvider serviceProvider,
            ILogger<ImageProcessingService> logger,
            HttpClient httpClient,
            IWebHostEnvironment environment,
            IR2StorageService r2StorageService,
            IImageProcessingQueue imageProcessingQueue,
            IOptions<TaskProcessorSettings> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = httpClient;
            _environment = environment;
            _r2StorageService = r2StorageService;
            _imageProcessingQueue = imageProcessingQueue;
            _settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [启动] ImageProcessingService 正在启动\n" +
                $"  - 并发处理数: {_settings.ImageProcessingConcurrency}");

            // 启动多个并发处理器
            var processorTasks = new List<Task>();
            for (int i = 0; i < _settings.ImageProcessingConcurrency; i++)
            {
                int processorId = i + 1;
                processorTasks.Add(Task.Run(async () =>
                {
                    _logger.LogInformation($"[图片处理器-{processorId}] 已启动");
                    await ProcessImagesFromQueueAsync(stoppingToken, processorId);
                }, stoppingToken));
            }

            await Task.WhenAll(processorTasks);

            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [停止] 所有图片处理器已停止");
        }

        private async Task ProcessImagesFromQueueAsync(CancellationToken stoppingToken, int processorId)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ImageProcessingJob? job = null;
                try
                {
                    job = await _imageProcessingQueue.DequeueAsync(stoppingToken);
                    if (job == null) continue;

                    _logger.LogInformation(
                        $"[图片处理器-{processorId}] 开始处理任务 {job.TaskId}");

                    await ProcessSingleImageJobAsync(job, processorId);

                    _logger.LogInformation(
                        $"[图片处理器-{processorId}] 任务 {job.TaskId} 处理完成");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"[图片处理器-{processorId}] 被取消");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"[图片处理器-{processorId}] 处理任务 {job?.TaskId} 时发生异常");
                    // 不抛出异常，继续处理下一个任务
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task ProcessSingleImageJobAsync(ImageProcessingJob job, int processorId)
        {
            using var scope = _serviceProvider.CreateScope();
            var taskRepository = scope.ServiceProvider.GetRequiredService<IDrawingTaskRepository>();
            var historyRepository = scope.ServiceProvider.GetRequiredService<IDrawingHistoryRepository>();

            try
            {
                // 1. 下载并压缩图片
                var (compressedPath, thumbnailPath) = await ProcessImageAsync(job.OriginalUrl);

                _logger.LogInformation(
                    $"[图片处理器-{processorId}] 任务 {job.TaskId} 图片已压缩");

                // 2. 上传到R2存储
                string r2Url, r2ThumbnailUrl;

                using (var originalStream = new FileStream(compressedPath, FileMode.Open))
                {
                    r2Url = await _r2StorageService.UploadFromStreamAsync(
                        originalStream,
                        $"drawings/{job.TaskId}/{Path.GetFileName(compressedPath)}");
                }

                if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
                {
                    using (var thumbnailStream = new FileStream(thumbnailPath, FileMode.Open))
                    {
                        r2ThumbnailUrl = await _r2StorageService.UploadFromStreamAsync(
                            thumbnailStream,
                            $"drawings/{job.TaskId}/thumbnails/{Path.GetFileName(thumbnailPath)}");
                    }
                }
                else
                {
                    r2ThumbnailUrl = r2Url; // 使用原图作为缩略图
                }

                _logger.LogInformation(
                    $"[图片处理器-{processorId}] 任务 {job.TaskId} 已上传到R2");

                // 3. ✅ 使用条件更新 - 避免并发冲突
                var taskUpdated = await taskRepository.UpdateToR2UrlAsync(
                    job.TaskId,
                    r2Url,
                    r2ThumbnailUrl,
                    job.OriginalUrl);

                if (taskUpdated)
                {
                    // 4. 更新历史记录
                    var historyUpdated = await historyRepository.UpdateToR2UrlByTaskIdAsync(
                        job.TaskId,
                        r2Url,
                        r2ThumbnailUrl,
                        job.OriginalUrl);

                    _logger.LogInformation(
                        $"[图片处理器-{processorId}] 任务 {job.TaskId} 数据库已更新为R2 URL");
                }
                else
                {
                    _logger.LogWarning(
                        $"[图片处理器-{processorId}] 任务 {job.TaskId} URL已被其他处理器更新，跳过");
                }

                // 5. 清理本地文件
                CleanupLocalFiles(compressedPath, thumbnailPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"[图片处理器-{processorId}] 处理任务 {job.TaskId} 时发生错误");

                // ✅ 不标记任务失败，因为任务已经完成（用户已看到原始URL）
                // 用户体验不受影响，只是使用的是API原始URL而非R2 URL
            }
        }

        private async Task<(string compressedPath, string thumbnailPath)> ProcessImageAsync(string originalUrl)
        {
            try
            {
                string localImagePath;

                // 下载图片
                if (originalUrl.StartsWith("http://") || originalUrl.StartsWith("https://"))
                {
                    var response = await _httpClient.GetAsync(originalUrl);
                    response.EnsureSuccessStatusCode();

                    var tempFile = Path.GetTempFileName();
                    using (var fileStream = File.Create(tempFile))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                    localImagePath = tempFile;
                }
                else
                {
                    localImagePath = Path.Combine(_environment.WebRootPath, originalUrl.TrimStart('/'));
                }

                // 压缩图片
                var compressedPath = await CompressImageAsync(localImagePath);

                // 生成缩略图
                var thumbnailPath = await GenerateThumbnailAsync(compressedPath);

                // 清理临时文件
                if (localImagePath != originalUrl && File.Exists(localImagePath))
                {
                    File.Delete(localImagePath);
                }

                return (compressedPath, thumbnailPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[图片处理] 处理图片失败: {originalUrl}");
                throw;
            }
        }

        private async Task<string> CompressImageAsync(string originalPath)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "generated");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var compressedFileName = Guid.NewGuid().ToString() + "-compressed.jpg";
            var compressedPath = Path.Combine(uploadsFolder, compressedFileName);

            using var image = await Image.LoadAsync(originalPath);
            await image.SaveAsJpegAsync(compressedPath,
                new JpegEncoder { Quality = _settings.JpegQuality });

            return compressedPath;
        }

        private async Task<string> GenerateThumbnailAsync(string imagePath)
        {
            var thumbnailPath = Path.Combine(
                Path.GetDirectoryName(imagePath)!,
                Path.GetFileNameWithoutExtension(imagePath) + "_thumb.jpg");

            using var image = await Image.LoadAsync(imagePath);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(_settings.ThumbnailWidth, _settings.ThumbnailHeight),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            await image.SaveAsJpegAsync(thumbnailPath,
                new JpegEncoder { Quality = _settings.ThumbnailQuality });

            return thumbnailPath;
        }

        private void CleanupLocalFiles(params string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"[清理] 删除文件失败: {filePath}");
                }
            }
        }
    }
}
