using GoogleAI.Models;
using GoogleAI.Repositories;
using GoogleAI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GoogleAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly IDrawingTaskRepository _taskRepository;
        private readonly IDrawingHistoryRepository _historyRepository;
        private readonly IModelConfigurationRepository _modelRepository;
        private readonly ITaskQueueService _taskQueueService;
        private readonly ILogger<TasksController> _logger;
        private readonly IPointsRepository _pointsRepository; // 新增积分仓储
        private readonly IWebHostEnvironment _environment;

        public TasksController(
            IDrawingTaskRepository taskRepository,
            IDrawingHistoryRepository historyRepository,
            IModelConfigurationRepository modelRepository,
            ITaskQueueService taskQueueService,
            IPointsRepository pointsRepository, // 注入积分仓储
            IWebHostEnvironment environment,
            ILogger<TasksController> logger)
        {
            _taskRepository = taskRepository;
            _historyRepository = historyRepository;
            _modelRepository = modelRepository;
            _taskQueueService = taskQueueService;
            _pointsRepository = pointsRepository;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// 创建绘图任务
        /// </summary>
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateTask([FromForm] int modelId, [FromForm] string taskMode, [FromForm] string prompt, [FromForm] string aspectRatio, [FromForm] List<IFormFile>? images)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (userId == 0)
                {
                    return Unauthorized(new { success = false, message = "未授权的用户" });
                }

                var model = await _modelRepository.GetByIdAsync(modelId);
                if (model == null || !model.IsActive)
                {
                    return BadRequest(new { success = false, message = "模型不存在或未启用" });
                }

                // 检查用户积分是否足够
                var userPoints = await _pointsRepository.GetUserPointsAsync(userId);
                if (userPoints < model.PointCost)
                {
                    return BadRequest(new { success = false, message = $"积分不足，当前积分: {userPoints}，所需积分: {model.PointCost}" });
                }

                // 保存图片到服务器临时目录
                var imagePaths = new List<string>();
                if (images != null && images.Count > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "temp");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    foreach (var image in images)
                    {
                        if (image.Length > 0)
                        {
                            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
                            var filePath = Path.Combine(uploadsFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(stream);
                            }

                            // 保存相对路径
                            imagePaths.Add($"/uploads/temp/{fileName}");
                        }
                    }
                }

                var task = new DrawingTask
                {
                    UserId = userId,
                    ModelId = modelId,
                    TaskMode = taskMode,
                    Prompt = prompt,
                    TaskStatus = "Pending",
                    Progress = 0,
                    ProgressMessage = "等待处理中...",
                    ReferenceImages = imagePaths.Count > 0 ? JsonSerializer.Serialize(imagePaths) : null,
                    CreatedAt = DateTime.Now,
                    AspectRatio = aspectRatio,
                    IsR2Uploaded = false,
                    UrlVersion = 0,
                    LastUpdatedAt = DateTime.Now
                };

                var taskId = await _taskRepository.CreateAsync(task);
                task.Id = taskId;

                // 3. ✅ 加入队列（异步处理）
                await _taskQueueService.EnqueueTaskAsync(task);

                _logger.LogInformation($"用户 {userId} 创建任务 {taskId}，已加入队列");

                // 4. 立即返回完整的任务对象，供前端直接显示
                return Ok(new
                {
                    success = true,
                    taskId = taskId,
                    message = "任务已创建，正在处理中...",
                    status = "Pending",
                    // 返回完整任务数据，前端可以直接插入列表
                    task = new
                    {
                        task.Id,
                        task.UserId,
                        task.ModelId,
                        task.TaskMode,
                        task.Prompt,
                        task.TaskStatus,
                        task.Progress,
                        task.ProgressMessage,
                        task.ResultImageUrl,
                        task.ResultImageUrls, // ✅ 新增多张图片支持
                        task.ThumbnailUrl,
                        task.ErrorMessage,
                        task.ReferenceImages,
                        task.CreatedAt,
                        task.CompletedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建绘图任务失败");
                return StatusCode(500, new { message = "创建任务失败", error = ex.Message, success = false });
            }
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }
        [HttpGet("my")]
        public async Task<IActionResult> GetMyTasks([FromQuery] int pageSize = 20, [FromQuery] int page = 1)
        {
            try
            {
                var userId = GetUserId();
                var offset = (page - 1) * pageSize;
                var tasks = await _taskRepository.GetUserTasksAsync(userId, pageSize, offset);

                // 检查并处理超时任务
                var processedTasks = new List<object>();

                foreach (var task in tasks)
                {
                    string currentStatus = task.TaskStatus;
                    string? errorMessage = task.ErrorMessage;
                    bool needsUpdate = false;

                    // 检查任务是否超时（针对Processing状态的任务）
                    if (task.TaskStatus == "Processing" &&
                        DateTime.Now.Subtract(task.CreatedAt).TotalMinutes > 15)
                    {
                        // 任务处理超时，标记为失败并返还积分
                        var model = await _modelRepository.GetByIdAsync(task.ModelId);
                        if (model != null)
                        {
                            await _pointsRepository.AddPointsAsync(task.UserId, model.PointCost, $"任务超时返还积分 - 任务处理超过15分钟");
                            Console.WriteLine($"任务 {task.Id} 处理超时，已为用户 {task.UserId} 返还 {model.PointCost} 积分");
                        }

                        await _taskRepository.UpdateStatusAsync(task.Id, "Failed", errorMessage: "任务处理超时，处理时间超过15分钟");
                        currentStatus = "Failed";
                        errorMessage = "任务处理超时，处理时间超过15分钟";
                        needsUpdate = true;

                        Console.WriteLine($"任务 {task.Id} 已标记为超时失败");
                    }

                    // 创建返回的任务对象
                    processedTasks.Add(new
                    {
                        task.Id,
                        task.UserId,
                        task.ModelId,
                        task.TaskMode,
                        task.Prompt,
                        TaskStatus = currentStatus,
                        task.ResultImageUrl,
                        task.ResultImageUrls, // ✅ 新增多张图片支持
                        ThumbnailUrl = task.ThumbnailUrl,
                        ErrorMessage = errorMessage,
                        task.CreatedAt,
                        task.CompletedAt,
                        task.ReferenceImages,
                        Progress = task.Progress, // ✅ 添加进度信息
                        ProgressMessage = task.ProgressMessage // ✅ 添加进度消息
                    });
                }

                return Ok(new { success = true, data = processedTasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"获取任务列表失败: {ex.Message}" });
            }
        }
        /// <summary>
        /// 获取任务状态
        /// </summary>
        [HttpGet("{taskId}")]
        [Authorize]
        public async Task<IActionResult> GetTaskStatus(int taskId)
        {
            try
            {
                var userId = GetCurrentUserId();

                // ✅ 直接从数据库获取最新状态
                var task = await _taskRepository.GetByIdAsync(taskId);

                if (task == null)
                {
                    return NotFound(new { message = "任务不存在" });
                }

                if (task.UserId != userId)
                {
                    return Forbid();
                }

                return Ok(new
                {
                    taskId = task.Id,
                    status = task.TaskStatus,
                    progress = task.Progress,
                    progressMessage = task.ProgressMessage,
                    resultImageUrl = task.ResultImageUrl,
                    resultImageUrls = task.ResultImageUrls, // ✅ 新增多张图片支持
                    thumbnailUrl = task.ThumbnailUrl,
                    errorMessage = task.ErrorMessage,
                    isR2Uploaded = task.IsR2Uploaded,  // ✅ 告诉前端是否已上传R2
                    createdAt = task.CreatedAt,
                    completedAt = task.CompletedAt,
                    referenceImages = task.ReferenceImages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取任务 {taskId} 状态失败");
                return StatusCode(500, new { message = "获取任务状态失败" });
            }
        }

        /// <summary>
        /// 获取用户的任务列表
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserTasks(
            [FromQuery] int pageSize = 20,
            [FromQuery] int offset = 0)
        {
            try
            {
                var userId = GetCurrentUserId();
                var tasks = await _taskRepository.GetUserTasksAsync(userId, pageSize, offset);

                return Ok(new
                {
                    tasks = tasks,
                    pageSize = pageSize,
                    offset = offset
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户任务列表失败");
                return StatusCode(500, new { message = "获取任务列表失败" });
            }
        }

        /// <summary>
        /// 获取队列诊断信息（仅管理员）
        /// </summary>
        [HttpGet("diagnostics")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDiagnostics()
        {
            try
            {
                // ✅ 获取包含数据库统计的诊断信息
                var diagnostics = await _taskQueueService.GetDiagnosticsAsync();

                return Ok(diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取诊断信息失败");
                return StatusCode(500, new { message = "获取诊断信息失败" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        [HttpDelete("{taskId}")]
        [Authorize]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            try
            {
                var userId = GetCurrentUserId();

                // 获取任务信息
                var task = await _taskRepository.GetByIdAsync(taskId);

                if (task == null)
                {
                    return NotFound(new { success = false, message = "任务不存在" });
                }

                // 验证任务所有权
                if (task.UserId != userId)
                {
                    return Forbid();
                }

                // 只允许删除已完成或失败的任务
                if (task.TaskStatus != "Completed" && task.TaskStatus != "Failed")
                {
                    return BadRequest(new { success = false, message = "只能删除已完成或失败的任务" });
                }

                // 删除任务
                await _taskRepository.DeleteAsync(taskId);

                _logger.LogInformation($"用户 {userId} 删除了任务 {taskId}");

                return Ok(new { success = true, message = "任务已删除" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除任务 {taskId} 失败");
                return StatusCode(500, new { success = false, message = "删除任务失败" });
            }
        }

    }

    // ✅ 请求模型
    public class CreateDrawingTaskRequest
    {
        public int ModelId { get; set; }
        public string TaskMode { get; set; } = string.Empty;
        public string? Prompt { get; set; }
        public string? AspectRatio { get; set; }
        public List<string>? ReferenceImages { get; set; }
    }
}
