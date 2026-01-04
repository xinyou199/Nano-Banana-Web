using GoogleAI.Configuration;
using GoogleAI.Models;
using GoogleAI.Repositories;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GoogleAI.Services
{
    /// <summary>
    /// 任务处理服务 - 负责从队列中取任务并调用绘图API
    /// </summary>
    public class TaskProcessorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskProcessorService> _logger;
        private readonly TaskProcessorSettings _settings;
        private readonly IImageProcessingQueue _imageProcessingQueue;
        private readonly HttpClient _httpClient;               // ✅ 新增
        private readonly IWebHostEnvironment _environment;         // ✅ 新增

        // TaskProcessorService 构造函数需要注入 HttpClient 和 IWebHostEnvironment
        public TaskProcessorService(
            IServiceProvider serviceProvider,
            ILogger<TaskProcessorService> logger,
            IOptions<TaskProcessorSettings> options,
            IImageProcessingQueue imageProcessingQueue,
            HttpClient httpClient,                    // ✅ 新增
            IWebHostEnvironment environment)          // ✅ 新增
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = options.Value;
            _imageProcessingQueue = imageProcessingQueue;
            _httpClient = httpClient;                 // ✅ 新增
            _httpClient.Timeout = TimeSpan.FromMinutes(_settings.TaskTimeoutMinutes);
            _environment = environment;               // ✅ 新增
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [启动] TaskProcessorService 正在启动\n" +
                $"  - 最大并发任务数: {_settings.MaxConcurrentTasks}");

            // 启动多个并发处理器
            var processorTasks = new List<Task>();
            for (int i = 0; i < _settings.MaxConcurrentTasks; i++)
            {
                int processorId = i + 1;
                processorTasks.Add(Task.Run(async () =>
                {
                    _logger.LogInformation($"[处理器-{processorId}] 已启动");
                    await ProcessTasksFromQueueAsync(stoppingToken, processorId);
                }, stoppingToken));
            }

            await Task.WhenAll(processorTasks);

            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [停止] 所有处理器已停止");
        }

        private async Task ProcessTasksFromQueueAsync(CancellationToken stoppingToken, int processorId)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                DrawingTask? task = null;
                try
                {
                    // 从队列中获取任务（阻塞等待）
                    using var scope = _serviceProvider.CreateScope();
                    var taskQueueService = scope.ServiceProvider.GetRequiredService<ITaskQueueService>();

                    task = await taskQueueService.DequeueAndMarkProcessingAsync(stoppingToken);
                    if (task == null)
                    {
                        _logger.LogInformation($"[处理器-{processorId}] 队列中无任务，等待中...");
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation(
                        $"[处理器-{processorId}] 开始处理任务 {task.Id}");

                    await ProcessTaskExecutionAsync(task, scope.ServiceProvider, processorId, stoppingToken);

                    _logger.LogInformation(
                        $"[处理器-{processorId}] 任务 {task.Id} 处理完成");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"[处理器-{processorId}] 被取消");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[处理器-{processorId}] 处理任务 {task?.Id} 时发生异常");

                    if (task != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var taskQueueService = scope.ServiceProvider.GetRequiredService<ITaskQueueService>();
                        await taskQueueService.MarkTaskFailedAsync(task.Id, ex.Message);
                    }

                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task ProcessTaskExecutionAsync(
            DrawingTask task,
            IServiceProvider scopedProvider,
            int processorId,
            CancellationToken cancellationToken)
        {
            var taskQueueService = scopedProvider.GetRequiredService<ITaskQueueService>();
            var taskRepository = scopedProvider.GetRequiredService<IDrawingTaskRepository>();
            var historyRepository = scopedProvider.GetRequiredService<IDrawingHistoryRepository>();
            var modelRepository = scopedProvider.GetRequiredService<IModelConfigurationRepository>();

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 1. 获取模型信息
                var model = await modelRepository.GetByIdAsync(task.ModelId);
                if (model == null)
                {
                    throw new InvalidOperationException($"模型 {task.ModelId} 不存在");
                }

                await taskQueueService.UpdateTaskProgressAsync(
                    task.Id, 10, "正在准备生成图片...");

                // 2. 调用API生成图片
                var generatedImagePaths = await GenerateImagesAsync(
                    task, model, taskQueueService, processorId, cancellationToken);

                if (generatedImagePaths == null || generatedImagePaths.Count == 0)
                {
                    throw new InvalidOperationException("API未返回任何图片");
                }

                var originalImageUrl = generatedImagePaths[0];
                var originalThumbnailUrl = generatedImagePaths[0]; // 初始使用原图

                await taskQueueService.UpdateTaskProgressAsync(
                    task.Id, 80, "图片生成完成，正在保存...");

                // 3. ✅ 先标记任务完成（使用原始URL）
                await taskQueueService.MarkTaskCompletedWithOriginalUrlAsync(
                    task.Id,
                    originalImageUrl,
                    originalThumbnailUrl);

                // 4. ✅ 创建历史记录（同时保存原始URL）
                var history = new DrawingHistory
                {
                    UserId = task.UserId,
                    TaskId = task.Id,
                    ModelName = model.ModelName,
                    Prompt = task.Prompt,
                    ImageUrl = originalImageUrl,
                    ThumbnailUrl = originalThumbnailUrl,
                    TaskMode = task.TaskMode,
                    IsR2Uploaded = false,
                    OriginalImageUrl = originalImageUrl
                };

                var historyId = await historyRepository.CreateAsync(history);

                stopwatch.Stop();
                _logger.LogInformation(
                    $"[处理器-{processorId}] 任务 {task.Id} 主流程完成，" +
                    $"耗时: {stopwatch.ElapsedMilliseconds}ms");

                // 5. ✅ 加入图片处理队列（不是Task.Run）
                await _imageProcessingQueue.EnqueueAsync(new ImageProcessingJob
                {
                    TaskId = task.Id,
                    HistoryId = historyId,
                    OriginalUrl = originalImageUrl,
                    AllImageUrls = generatedImagePaths
                });

                _logger.LogInformation(
                    $"[处理器-{processorId}] 任务 {task.Id} 已加入图片处理队列");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    $"[处理器-{processorId}] 任务 {task.Id} 执行失败，" +
                    $"耗时: {stopwatch.ElapsedMilliseconds}ms");

                await taskQueueService.MarkTaskFailedAsync(task.Id, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 调用实际的绘图API生成图片
        /// </summary>
        private async Task<List<string>> GenerateImagesAsync(
            DrawingTask task,
            ModelConfiguration model,
            ITaskQueueService taskQueueService,
            int processorId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"[处理器-{processorId}] 任务 {task.Id} 开始调用 {model.ModelName} API");

            await taskQueueService.UpdateTaskProgressAsync(
                task.Id, 30, $"正在使用 {model.ModelName} 生成图片...");

            try
            {
                // ✅ 根据API地址判断调用方式
                if (model.ApiUrl.Contains("grsai.dakka.com.cn"))
                {
                    // 调用 nano-banana API（流式响应，带进度）
                    var taskimage = await CallNanoBananaApiAsync(task, model, taskQueueService, cancellationToken);
                    if (taskimage.Status)
                    {
                        return taskimage.Images;
                    }
                    throw new Exception(taskimage.Message);
                }

                // 调用标准OpenAI兼容API
                var imageUrls = await CallStandardApiAsync(task, model, cancellationToken);
                if (imageUrls == null || imageUrls.Count == 0)
                {
                    throw new Exception("API未返回任何图片");
                }

                return imageUrls;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[处理器-{processorId}] 任务 {task.Id} 调用API失败");
                throw;
            }
        }

        /// <summary>
        /// 调用 nano-banana API（专用流式API，支持实时进度）
        /// </summary>
        private async Task<TaskRequest> CallNanoBananaApiAsync(
            DrawingTask task,
            ModelConfiguration model,
            ITaskQueueService taskQueueService,
            CancellationToken cancellationToken)
        {
            try
            {
                // 1️⃣ 准备参考图片（转Base64）
                var imagePaths = string.IsNullOrEmpty(task.ReferenceImages)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(task.ReferenceImages);

                var base64Images = new List<string>();
                if (imagePaths != null && imagePaths.Count > 0)
                {
                    foreach (var imagePath in imagePaths)
                    {
                        var base64Image = await ReadAndCompressImageAsBase64(imagePath);
                        if (!string.IsNullOrEmpty(base64Image))
                        {
                            base64Images.Add($"data:image/jpeg;base64,{base64Image}");
                        }
                    }
                }

                // 2️⃣ 构建请求体
                var requestBody = new
                {
                    model = model.ModelName.ToLower(),
                    prompt = task.Prompt,
                    aspectRatio = string.IsNullOrWhiteSpace(task.AspectRatio) ? "auto" : task.AspectRatio,
                    imageSize = model.ModelName.ToLower().Contains("pro")
                        ? model.ImageSize?.ToUpper()
                        : null,
                    urls = base64Images?.Count > 0 ? base64Images : null,
                    webHook = (string?)null,
                    shutProgress = false  // 开启进度推送
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                // 3️⃣ 检查请求体大小
                var requestSizeKB = (double)json.Length / 1024;
                _logger.LogInformation($"[API] nano-banana 请求体大小: {requestSizeKB:F2}KB");

                if (json.Length > 20 * 1024 * 1024)
                {
                    throw new Exception($"请求体过大: {requestSizeKB:F2}KB，超过20MB限制");
                }

                // 4️⃣ 发送HTTP请求
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, model.ApiUrl);
                request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
                request.Content = content;

                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[API] nano-banana 调用失败: {response.StatusCode}, {errorContent}");
                    throw new Exception($"nano-banana API request failed: {response.StatusCode}");
                }

                // 5️⃣ 处理流式响应（带进度更新）
                var imagetask = await ProcessNanoBananaStreamResponseAsync(
                    response,
                    task.Id,
                    taskQueueService,
                    cancellationToken);
                var imageUrl = imagetask.Image;
                if (imagetask.Status==false)
                {
                    throw new Exception(imagetask.Message);
                }

                return imagetask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[API] CallNanoBananaApiAsync 失败");
                throw;
            }
        }

        /// <summary>
        /// 处理 nano-banana 流式响应，实时更新进度
        /// </summary>
        private async Task<TaskRequest> ProcessNanoBananaStreamResponseAsync(
            HttpResponseMessage response,
            int taskId,
            ITaskQueueService taskQueueService,
            CancellationToken cancellationToken)
        {
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                var readStartTime = DateTime.UtcNow;
                var lastProgressUpdate = DateTime.UtcNow;
                var lastProgressValue = 0;

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 超时检测
                    if (DateTime.UtcNow - readStartTime > TimeSpan.FromMinutes(_settings.TaskTimeoutMinutes))
                    {
                        throw new TimeoutException($"绘图超时（{_settings.TaskTimeoutMinutes}分钟）");
                    }

                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // 去除 SSE 前缀
                    if (line.StartsWith("data: "))
                        line = line.Substring(6);

                    // 检测结束标记
                    if (line == "[DONE]")
                    {
                        _logger.LogInformation("[API] nano-banana 流式响应结束");
                        break;
                    }

                    if (line.Trim().Length == 0)
                        continue;

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(line);
                        var root = jsonDoc.RootElement;
                        //检查成功状态"status": "failed",
                        if (root.TryGetProperty("status", out var status))
                        {
                            if (status.GetString() == "failed")
                            {
                                var errorMessage = root.TryGetProperty("error", out var errorEl)
                                    ? errorEl.GetString()
                                    : "未知错误";
                                //throw new Exception($"nano-banana API 返回失败状态: {errorMessage}");
                                return new TaskRequest()
                                {
                                    Image = string.Empty,
                                    Status = false,
                                    Message = errorMessage
                                };
                            }
                        }
                        // ✅ 检查是否有最终结果（图片URL）
                        if (root.TryGetProperty("results", out var resultsElement))
                        {
                            if (resultsElement.ValueKind != JsonValueKind.Null &&
                                resultsElement.GetArrayLength() > 0)
                            {
                                return new TaskRequest()
                                {
                                    Image = resultsElement[0].GetProperty("url").GetString() ?? string.Empty,
                                    Status = true
                                };
                            }
                        }

                        // ✅ 更新任务进度（节流控制）
                        if (root.TryGetProperty("progress", out var progressElement))
                        {
                            var progress = progressElement.GetInt32();

                            // 进度变化足够大 且 距上次更新时间足够长
                            if (progress > lastProgressValue + 5 &&
                                (DateTime.UtcNow - lastProgressUpdate).TotalSeconds >= 2)
                            {
                                var progressMessage = GetProgressMessage(progress);
                                await taskQueueService.UpdateTaskProgressAsync(
                                    taskId,
                                    progress,
                                    progressMessage);

                                lastProgressUpdate = DateTime.UtcNow;
                                lastProgressValue = progress;

                                _logger.LogInformation($"[进度] 任务 {taskId}: {progress}% - {progressMessage}");
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        _logger.LogDebug($"[API] 无法解析JSON行: {line}");
                    }
                }

                _logger.LogWarning("[API] 未在流式响应中找到图片URL");
                return new TaskRequest() { Image = string.Empty, Status = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] 处理 nano-banana 流式响应时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 调用标准 OpenAI 兼容 API
        /// </summary>
        private async Task<List<string>> CallStandardApiAsync(
            DrawingTask task,
            ModelConfiguration model,
            CancellationToken cancellationToken)
        {
            try
            {
                var imageUrls = new List<string>();

                // 1️⃣ 处理参考图片（多模态输入）
                var imagePaths = string.IsNullOrEmpty(task.ReferenceImages)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(task.ReferenceImages);

                object contentObject;

                if (imagePaths != null && imagePaths.Count > 0)
                {
                    // 限制最多12张图片
                    if (imagePaths.Count > 12)
                    {
                        _logger.LogWarning($"[API] 图片数量过多({imagePaths.Count})，仅使用前12张");
                        imagePaths = imagePaths.Take(12).ToList();
                    }
                    // 构建多模态content
                    var drawPrompt = task.Prompt;
                    if (!string.IsNullOrWhiteSpace(task.AspectRatio))
                    {
                        drawPrompt += $" 图片比例：{task.AspectRatio}";
                    }
                    var contentList = new List<object>
                    {
                        new { type = "text", text = drawPrompt }
                    };

                    // 添加图片（Base64编码）
                    foreach (var imagePath in imagePaths)
                    {
                        var base64Image = await ReadAndCompressImageAsBase64(imagePath);
                        if (!string.IsNullOrEmpty(base64Image))
                        {
                            contentList.Add(new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:image/jpeg;base64,{base64Image}" }
                            });
                        }
                    }
                    contentObject = contentList;
                }
                else
                {
                    var drawPrompt = task.Prompt;
                    if (!string.IsNullOrWhiteSpace(task.AspectRatio))
                    {
                        drawPrompt += $" 图片比例：{task.AspectRatio}";
                    }
                    // 纯文本prompt
                    contentObject = drawPrompt;
                }

                // 2️⃣ 构建OpenAI格式请求体
                var requestBody = new
                {
                    model = model.ModelName.ToLower(),
                    messages = new[]
                    {
                new { role = "user", content = contentObject }
            },
                    max_tokens = model.MaxTokens,
                    temperature = (double)model.Temperature,
                    stream = true
                };

                var json = JsonSerializer.Serialize(requestBody);
                var requestSizeKB = json.Length / 1024;
                _logger.LogInformation($"[API] 标准API请求体大小: {requestSizeKB}KB");

                if (json.Length > 20 * 1024 * 1024)
                {
                    throw new Exception($"请求体过大: {requestSizeKB}KB，超过20MB限制");
                }

                // 3️⃣ 发送HTTP请求
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, model.ApiUrl);
                request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
                request.Content = content;

                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[API] 标准API调用失败: {response.StatusCode}, {errorContent}");
                    throw new Exception($"API request failed: {response.StatusCode}");
                }

                // 4️⃣ 处理流式响应并提取图片URL
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);
                var readStartTime = DateTime.UtcNow;

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (DateTime.UtcNow - readStartTime > TimeSpan.FromMinutes(_settings.TaskTimeoutMinutes))
                    {
                        _logger.LogWarning("[API] 读取流式响应超时");
                        break;
                    }

                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("data: "))
                        line = line.Substring(6);

                    if (line == "[DONE]")
                    {
                        _logger.LogInformation("[API] 标准API流式响应结束");
                        break;
                    }

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(line);
                        var root = jsonDoc.RootElement;

                        // ✅ 方式1：从content中解析Markdown图片链接
                        if (root.TryGetProperty("choices", out var choicesEl) &&
                            choicesEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var choice in choicesEl.EnumerateArray())
                            {
                                if (choice.TryGetProperty("delta", out var deltaEl) &&
                                    deltaEl.TryGetProperty("content", out var contentEl))
                                {
                                    var contentText = contentEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(contentText))
                                    {
                                        var match = Regex.Match(
                                            contentText,
                                            @"!\[[^\]]*\]\((?<url>https?://[^\s)]+)\)",
                                            RegexOptions.IgnoreCase);

                                        if (match.Success)
                                        {
                                            var imageUrl = match.Groups["url"].Value;
                                            _logger.LogInformation($"[API] 解析到Markdown图片URL: {imageUrl}");
                                            imageUrls.Add(imageUrl);
                                        }
                                    }
                                }
                            }
                        }

                        // ✅ 方式2-4：从不同字段提取URL
                        if (root.TryGetProperty("url", out var urlEl))
                        {
                            var imageUrl = urlEl.GetString();
                            if (!string.IsNullOrWhiteSpace(imageUrl))
                            {
                                _logger.LogInformation($"[API] 找到图片URL: {imageUrl}");
                                imageUrls.Add(imageUrl);
                            }
                        }

                        if (root.TryGetProperty("image", out var imageEl))
                        {
                            var imageUrl = imageEl.GetString();
                            if (!string.IsNullOrWhiteSpace(imageUrl))
                            {
                                imageUrls.Add(imageUrl);
                            }
                        }

                        if (root.TryGetProperty("data", out var dataEl) &&
                            dataEl.TryGetProperty("url", out var dataUrlEl))
                        {
                            var imageUrl = dataUrlEl.GetString();
                            if (!string.IsNullOrWhiteSpace(imageUrl))
                            {
                                imageUrls.Add(imageUrl);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // 忽略无法解析的行
                    }
                }

                return imageUrls;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] 标准API调用异常");
                throw;
            }
        }

        /// <summary>
        /// 读取图片并压缩为Base64
        /// </summary>
        private async Task<string?> ReadAndCompressImageAsBase64(string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/'));
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning($"[图片] 文件不存在: {fullPath}");
                    return null;
                }

                var fileInfo = new FileInfo(fullPath);
                var originalSizeMB = (double)fileInfo.Length / 1024.0 / 1024.0;

                // 如果文件已经很小，直接返回
                if (originalSizeMB <= 5.0) // 5MB以下不压缩
                {
                    var originalBytes = await File.ReadAllBytesAsync(fullPath);
                    return Convert.ToBase64String(originalBytes);
                }

                // 加载图片并压缩
                using var image = await Image.LoadAsync(fullPath);

                byte[] compressedBytes;
                int quality = 85;
                const int minQuality = 50;

                while (true)
                {
                    using var ms = new MemoryStream();
                    var encoder = new JpegEncoder { Quality = quality };
                    await image.SaveAsJpegAsync(ms, encoder);
                    compressedBytes = ms.ToArray();

                    var sizeMB = (double)compressedBytes.Length / 1024.0 / 1024.0;

                    if (sizeMB <= 5.0 || quality <= minQuality)
                        break;

                    quality -= 5;
                }

                return Convert.ToBase64String(compressedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[图片] 读取或压缩图片失败: {relativePath}");
                return null;
            }
        }

        /// <summary>
        /// 根据进度返回友好提示消息
        /// </summary>
        private string GetProgressMessage(int progress)
        {
            return progress switch
            {
                >= 0 and < 10 => "AI正在理解您的创意...",
                >= 10 and < 25 => "深度学习网络正在工作...",
                >= 25 and < 50 => "正在生成创意草图...",
                >= 50 and < 75 => "AI正在精细化处理...",
                >= 75 and < 95 => "正在进行细节渲染...",
                >= 95 and < 100 => "即将完成，请稍候...",
                100 => "处理完成！",
                _ => "处理中..."
            };
        }


    }
}
