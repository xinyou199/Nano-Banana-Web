using GoogleAI.Configuration;
using GoogleAI.Models;
using GoogleAI.Repositories;
using GoogleAI.Services;
using CloudflareR2.NET;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;

namespace GoogleAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SplitUpscaleController : ControllerBase
    {
        private readonly IDrawingTaskRepository _taskRepository;
        private readonly IModelConfigurationRepository _modelRepository;
        private readonly IImageSplitService _imageSplitService;
        private readonly ITaskQueueService _taskQueueService;
        private readonly IPointsRepository _pointsRepository;
        private readonly TaskProcessorSettings _settings;
        private readonly ILogger<SplitUpscaleController> _logger;
        private readonly ICloudflareR2Client? _r2Client;

        public SplitUpscaleController(
            IDrawingTaskRepository taskRepository,
            IModelConfigurationRepository modelRepository,
            IImageSplitService imageSplitService,
            ITaskQueueService taskQueueService,
            IPointsRepository pointsRepository,
            IOptions<TaskProcessorSettings> options,
            ILogger<SplitUpscaleController> logger,
            ICloudflareR2Client? r2Client = null)
        {
            _taskRepository = taskRepository;
            _modelRepository = modelRepository;
            _imageSplitService = imageSplitService;
            _taskQueueService = taskQueueService;
            _pointsRepository = pointsRepository;
            _settings= options.Value;
            _logger = logger;
            _r2Client = r2Client;
        }

    /// <summary>
    /// 拆分图片并进行高清重绘或仅拆分
    /// </summary>
    [HttpPost("split-and-upscale")]
    public async Task<IActionResult> SplitAndUpscale([FromBody] SplitUpscaleRequest request)
    {
        try
        {
            // 根据处理模式分支处理
            if (request.ProcessMode == "split-only")
            {
                return await HandleSplitOnly(request);
            }
            else
            {
                return await HandleSplitUpscale(request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "拆分处理失败");
            return StatusCode(500, new
            {
                success = false,
                message = "拆分处理失败: " + ex.Message
            });
        }
    }

    /// <summary>
    /// 仅拆分模式处理
    /// </summary>
    private async Task<IActionResult> HandleSplitOnly(SplitUpscaleRequest request)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation($"用户 {userId} 请求仅拆分，任务ID: {request.OriginalTaskId}，容差: {request.Tolerance}");

        // 1. 获取原始任务
        var originalTask = await _taskRepository.GetByIdAsync(request.OriginalTaskId);
        if (originalTask == null)
        {
            return NotFound(new { success = false, message = "原始任务不存在" });
        }

        // 验证任务所有权
        if (originalTask.UserId != userId)
        {
            return Forbid();
        }

        // 检查任务是否已完成
        if (originalTask.TaskStatus != "Completed" || string.IsNullOrEmpty(originalTask.ResultImageUrl))
        {
            return BadRequest(new { success = false, message = "原始任务未完成或没有结果图片" });
        }

        // 2. 解析拆分模式（如 "3x3"）
        var modeParts = request.SplitMode.ToLower().Split('x');
        if (modeParts.Length != 2 || !int.TryParse(modeParts[0], out int rows) || !int.TryParse(modeParts[1], out int cols))
        {
            return BadRequest(new { success = false, message = "拆分模式格式错误，应为 '3x3' 格式" });
        }

        // 3. 拆分图片（使用容差）
        _logger.LogInformation($"开始拆分图片: {originalTask.ResultImageUrl}，容差: {request.Tolerance}");
        var blocks = await _imageSplitService.SplitImageAsync2(
            originalTask.ResultImageUrl,
            rows,
            cols,
            Enumerable.Range(0, rows * cols).ToList(), // 全选所有分块
            request.Tolerance
        );

        _logger.LogInformation($"图片拆分完成，共 {blocks.Count} 个分块");

        // 4. 创建批次组ID
        var batchGroupId = Guid.NewGuid().ToString();
        var taskIds = new List<int>();

        // 5. 为每个分块创建任务（标记为已完成）
        foreach (var block in blocks)
        {
            var task = new DrawingTask
            {
                UserId = userId,
                TaskMode = "split-only",
                TaskStatus = "Completed", // 直接标记为完成
                Progress = 100,
                Prompt="图片分割",
                ModelId=1,
                ProgressMessage = "拆分完成",
                ParentTaskId = request.OriginalTaskId,
                SplitIndex = block.Index,
                BatchGroupId = batchGroupId,
                // 使用本地路径作为初始ResultImageUrl，用户可以立即看到拆分后的图片
                // 异步上传到R2后，会自动更新为R2 URL
                ResultImageUrl = block.RelativeUrl,
                CreatedAt = DateTime.Now,
                CompletedAt = DateTime.Now,
                IsR2Uploaded = false,
                UrlVersion = 0,
                LastUpdatedAt = DateTime.Now,
                ProcessMode = "split-only",
                Tolerance = request.Tolerance
            };

            var taskId = await _taskRepository.CreateAsync(task);
            task.Id = taskId;
            taskIds.Add(taskId);

            _logger.LogInformation($"创建拆分任务 {taskId}，分块索引: {block.Index}，初始图片路径: {block.RelativeUrl}");

            // 6. 异步上传到R2（后台任务，不阻塞主流程）
            // 上传成功后会自动更新ResultImageUrl为R2 URL
            _ = UploadBlockToR2Async(task, block.LocalPath);
        }

        _logger.LogInformation($"批次 {batchGroupId} 创建完成，共 {taskIds.Count} 个拆分任务");

        return Ok(new
        {
            success = true,
            batchGroupId,
            taskIds,
            totalBlocks = blocks.Count,
            message = $"已完成拆分，共生成 {blocks.Count} 个分块"
        });
    }

    /// <summary>
    /// 拆分重绘模式处理
    /// </summary>
    private async Task<IActionResult> HandleSplitUpscale(SplitUpscaleRequest request)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation($"用户 {userId} 请求拆分重绘，任务ID: {request.OriginalTaskId}");

        // 1. 获取原始任务
        var originalTask = await _taskRepository.GetByIdAsync(request.OriginalTaskId);
        if (originalTask == null)
        {
            return NotFound(new { success = false, message = "原始任务不存在" });
        }

        // 验证任务所有权
        if (originalTask.UserId != userId)
        {
            return Forbid();
        }

        // 检查任务是否已完成
        if (originalTask.TaskStatus != "Completed" || string.IsNullOrEmpty(originalTask.ResultImageUrl))
        {
            return BadRequest(new { success = false, message = "原始任务未完成或没有结果图片" });
        }

        // 2. 获取模型信息
        var model = await _modelRepository.GetByIdAsync(request.ModelId ?? 0);
        if (model == null || !model.IsActive)
        {
            return BadRequest(new { success = false, message = "模型不存在或未启用" });
        }

        // 3. 计算总消耗积分
        int selectedCount = request.SelectedBlocks?.Count ?? 0;
        if (selectedCount == 0)
        {
            return BadRequest(new { success = false, message = "请至少选择一个分块进行重绘" });
        }

        int totalCost = selectedCount * model.PointCost;

        // 4. 检查用户积分
        var userPoints = await _pointsRepository.GetUserPointsAsync(userId);
        if (userPoints < totalCost)
        {
            return BadRequest(new
            {
                success = false,
                message = $"积分不足，当前积分: {userPoints}，所需积分: {totalCost}"
            });
        }

        // 5. 解析拆分模式（如 "3x3"）
        var modeParts = request.SplitMode.ToLower().Split('x');
        if (modeParts.Length != 2 || !int.TryParse(modeParts[0], out int rows) || !int.TryParse(modeParts[1], out int cols))
        {
            return BadRequest(new { success = false, message = "拆分模式格式错误，应为 '3x3' 格式" });
        }

        // 6. 扣除积分（预扣）
        await _pointsRepository.DeductPointsAsync(
            userId,
            totalCost,
            $"拆分重绘任务 - 原任务{request.OriginalTaskId} - {selectedCount}个分块"
        );

        _logger.LogInformation($"已为用户 {userId} 扣除 {totalCost} 积分（{selectedCount}块 × {model.PointCost}积分）");

        // 7. 拆分图片
        _logger.LogInformation($"开始拆分图片: {originalTask.ResultImageUrl}");
        var blocks = await _imageSplitService.SplitImageAsync(
            originalTask.ResultImageUrl,
            rows,
            cols,
            request.SelectedBlocks ?? new List<int>()
        );

        _logger.LogInformation($"图片拆分完成，共 {blocks.Count} 个分块");

        // 8. 创建批次组ID
        var batchGroupId = Guid.NewGuid().ToString();
        var taskIds = new List<int>();

        var prompt = string.IsNullOrEmpty(request.ReferenceImageUrl) ? _settings.Prompt4k : _settings.Prompt4kFace;
        // 9. 为每个分块创建重绘任务
        foreach (var block in blocks)
        {
            // 构建参考图列表：分块图片 + 可选的脸部参考图
            var referenceImages = new List<string> { block.RelativeUrl };
            if (!string.IsNullOrEmpty(request.ReferenceImageUrl))
            {
                referenceImages.Add(request.ReferenceImageUrl);
                _logger.LogInformation($"分块 {block.Index} 添加脸部参考图: {request.ReferenceImageUrl}");
            }

            var task = new DrawingTask
            {
                UserId = userId,
                ModelId = request.ModelId ?? 0,
                TaskMode = "upscale", // 高清重绘模式
                Prompt = prompt,
                TaskStatus = "Pending",
                Progress = 0,
                ProgressMessage = "等待处理中...",
                ParentTaskId = request.OriginalTaskId,
                SplitIndex = block.Index,
                BatchGroupId = batchGroupId,
                ReferenceImages = JsonSerializer.Serialize(referenceImages),
                CreatedAt = DateTime.Now,
                AspectRatio = originalTask.AspectRatio, // 分块通常是正方形或保持原比例
                IsR2Uploaded = false,
                UrlVersion = 0,
                LastUpdatedAt = DateTime.Now,
                ProcessMode = "split-upscale"
            };

            var taskId = await _taskRepository.CreateAsync(task);
            task.Id = taskId;
            taskIds.Add(taskId);

            // 10. 加入队列
            await _taskQueueService.EnqueueTaskAsync(task);

            _logger.LogInformation($"创建子任务 {taskId}，分块索引: {block.Index}");
        }

        _logger.LogInformation($"批次 {batchGroupId} 创建完成，共 {taskIds.Count} 个子任务");

        return Ok(new
        {
            success = true,
            batchGroupId,
            taskIds,
            totalCost,
            selectedCount,
            message = $"已创建 {blocks.Count} 个重绘任务，总消耗 {totalCost} 积分"
        });
    }

    /// <summary>
    /// 异步上传分块到R2
    /// </summary>
    private async Task UploadBlockToR2Async(DrawingTask task, string localPath)
    {
        try
        {
            if (_r2Client == null)
            {
                _logger.LogWarning("R2客户端未配置，跳过上传");
                return;
            }

            if (!System.IO.File.Exists(localPath))
            {
                _logger.LogWarning($"本地文件不存在: {localPath}");
                return;
            }

            var fileName = $"split-blocks/{task.BatchGroupId}/block_{task.SplitIndex}_{Guid.NewGuid()}.png";
            
            _logger.LogInformation($"开始上传分块 {task.SplitIndex} 到R2: {fileName}");

            // 使用 UploadBlobAsync 方法上传文件
            using (var fileStream = System.IO.File.OpenRead(localPath))
            {
                var r2Url = await _r2Client.UploadBlobAsync(fileStream, fileName, CancellationToken.None, "image/png");

                if (string.IsNullOrEmpty(r2Url))
                {
                    _logger.LogError($"R2上传返回空URL，分块 {task.SplitIndex}");
                    return;
                }

                // 更新任务的R2 URL
                task.ResultImageUrl = r2Url;
                task.IsR2Uploaded = true;
                task.LastUpdatedAt = DateTime.Now;

                var updateResult = await _taskRepository.UpdateAsync(task);

                if (updateResult)
                {
                    _logger.LogInformation($"分块 {task.SplitIndex} 已上传到R2并更新数据库: {r2Url}");
                }
                else
                {
                    _logger.LogError($"分块 {task.SplitIndex} 上传到R2成功但数据库更新失败: {r2Url}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"上传分块 {task.SplitIndex} 到R2失败");
        }
    }

        /// <summary>
        /// 获取批量任务进度
        /// </summary>
        [HttpGet("batch-progress/{batchGroupId}")]
        public async Task<IActionResult> GetBatchProgress(string batchGroupId)
        {
            try
            {
                var userId = GetCurrentUserId();

                // 获取该批次的所有任务
                var tasks = await _taskRepository.GetTasksByBatchGroupAsync(batchGroupId);

                if (tasks == null || !tasks.Any())
                {
                    return NotFound(new { success = false, message = "批次任务不存在" });
                }

                // 验证所有权（检查第一个任务）
                if (tasks.First().UserId != userId)
                {
                    return Forbid();
                }

                // 统计进度
                var tasksList = tasks.ToList();
                int totalBlocks = tasksList.Count;
                int completedBlocks = tasksList.Count(t => t.TaskStatus == "Completed");
                int failedBlocks = tasksList.Count(t => t.TaskStatus == "Failed");
                int processingBlocks = tasksList.Count(t => t.TaskStatus == "Processing");
                int pendingBlocks = tasksList.Count(t => t.TaskStatus == "Pending");

                double progress = totalBlocks > 0 ? (completedBlocks * 100.0 / totalBlocks) : 0;

                // 获取每个分块的详细信息
                var blocks = tasks.Select(t => new
                {
                    taskId = t.Id,
                    blockIndex = t.SplitIndex ?? 0,
                    status = t.TaskStatus,
                    progress = t.Progress,
                    progressMessage = t.ProgressMessage,
                    resultImageUrl = t.ResultImageUrl,
                    thumbnailUrl = t.ThumbnailUrl,
                    errorMessage = t.ErrorMessage,
                    createdAt = t.CreatedAt,
                    completedAt = t.CompletedAt
                }).OrderBy(b => b.blockIndex).ToList();

                return Ok(new
                {
                    success = true,
                    batchGroupId,
                    totalBlocks,
                    completedBlocks,
                    failedBlocks,
                    processingBlocks,
                    pendingBlocks,
                    progress = Math.Round(progress, 2),
                    isCompleted = completedBlocks == totalBlocks,
                    blocks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取批次进度失败: {batchGroupId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取批次进度失败: " + ex.Message
                });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }
    }

    /// <summary>
    /// 拆分重绘请求模型
    /// </summary>
    public class SplitUpscaleRequest
    {
        public int OriginalTaskId { get; set; }
        public string SplitMode { get; set; } = "3x3"; // 如 "2x2", "3x3", "4x4"
        public List<int>? SelectedBlocks { get; set; } // 选中的分块索引
        public int? ModelId { get; set; }
        public string? ReferenceImageUrl { get; set; } // 脸部参考图URL（可选）
        public string ProcessMode { get; set; } = "split-upscale"; // "split-only" 或 "split-upscale"
        public double Tolerance { get; set; } = 0.2; // 仅在 split-only 模式下使用
    }
}
