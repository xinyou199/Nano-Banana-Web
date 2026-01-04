//using GoogleAI.Models;
//using GoogleAI.Repositories;
//using GoogleAI.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using System.Text.Json;

//namespace GoogleAI.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    [Authorize]
//    public class StoryboardController : ControllerBase
//    {
//        private readonly IStoryboardProjectRepository _storyboardRepository;
//        private readonly IDrawingTaskRepository _taskRepository;
//        private readonly IModelConfigurationRepository _modelRepository;
//        private readonly IPointsRepository _pointsRepository;
//        private readonly ITaskQueueService _taskQueueService;
//        private readonly ILogger<StoryboardController> _logger;

//        public StoryboardController(
//            IStoryboardProjectRepository storyboardRepository,
//            IDrawingTaskRepository taskRepository,
//            IModelConfigurationRepository modelRepository,
//            IPointsRepository pointsRepository,
//            ITaskQueueService taskQueueService,
//            ILogger<StoryboardController> logger)
//        {
//            _storyboardRepository = storyboardRepository;
//            _taskRepository = taskRepository;
//            _modelRepository = modelRepository;
//            _pointsRepository = pointsRepository;
//            _taskQueueService = taskQueueService;
//            _logger = logger;
//        }

//        private int GetCurrentUserId()
//        {
//            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//            return int.Parse(userIdClaim ?? "0");
//        }

//        // ===== 项目管理 =====

//        /// <summary>
//        /// 创建分镜项目
//        /// </summary>
//        [HttpPost("projects")]
//        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                _logger.LogInformation($"用户 {userId} 创建分镜项目: {request.ProjectName}");

//                // 验证模型存在且激活
//                var model = await _modelRepository.GetByIdAsync(request.ModelId);
//                if (model == null || !model.IsActive)
//                {
//                    return BadRequest(new { success = false, message = "模型不存在或未激活" });
//                }

//                var project = new StoryboardProject
//                {
//                    UserId = userId,
//                    ProjectName = request.ProjectName,
//                    Description = request.Description,
//                    ModelId = request.ModelId,
//                    AspectRatio = request.AspectRatio ?? "16:9",
//                    BasePrompt = request.BasePrompt,
//                    ReferenceImages = request.ReferenceImages != null ? JsonSerializer.Serialize(request.ReferenceImages) : null,
//                    Status = "Draft",
//                    TotalFrames = 0,
//                    CompletedFrames = 0,
//                    IsTemplate = false,
//                    TemplateDownloads = 0
//                };

//                var projectId = await _storyboardRepository.CreateProjectAsync(project);
//                _logger.LogInformation($"分镜项目创建成功，ID: {projectId}");

//                return Ok(new { success = true, projectId, message = "项目创建成功" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "创建分镜项目失败");
//                return StatusCode(500, new { success = false, message = "创建项目失败: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// 获取用户的分镜项目列表
//        /// </summary>
//        [HttpGet("projects")]
//        public async Task<IActionResult> GetProjects([FromQuery] int pageSize = 20, [FromQuery] int offset = 0)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var projects = await _storyboardRepository.GetUserProjectsAsync(userId, pageSize, offset);
//                var totalCount = await _storyboardRepository.GetUserProjectCountAsync(userId);

//                var projectList = projects.Select(p => new
//                {
//                    p.Id,
//                    p.ProjectName,
//                    p.Description,
//                    p.ModelId,
//                    p.AspectRatio,
//                    p.Status,
//                    p.TotalFrames,
//                    p.CompletedFrames,
//                    Progress = p.TotalFrames > 0 ? (p.CompletedFrames * 100 / p.TotalFrames) : 0,
//                    p.CreatedAt,
//                    p.UpdatedAt,
//                    p.IsTemplate,
//                    p.TemplateShareCode,
//                    p.TemplateDownloads
//                }).ToList();

//                return Ok(new
//                {
//                    success = true,
//                    data = projectList,
//                    pagination = new { pageSize, offset, total = totalCount }
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "获取项目列表失败");
//                return StatusCode(500, new { success = false, message = "获取项目列表失败: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// 获取项目详情及其所有分镜帧
//        /// </summary>
//        [HttpGet("projects/{projectId}")]
//        public async Task<IActionResult> GetProject(int projectId)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var project = await _storyboardRepository.GetProjectByIdAsync(projectId);

//                if (project == null)
//                {
//                    return NotFound(new { success = false, message = "项目不存在" });
//                }

//                // 验证所有权
//                if (project.UserId != userId)
//                {
//                    return Forbid();
//                }

//                var frames = await _storyboardRepository.GetProjectFramesAsync(projectId);
//                var referenceImages = project.ReferenceImages != null 
//                    ? JsonSerializer.Deserialize<List<string>>(project.ReferenceImages) 
//                    : new List<string>();

//                return Ok(new
//                {
//                    success = true,
//                    data = new
//                    {
//                        project = new
//                        {
//                            project.Id,
//                            project.ProjectName,
//                            project.Description,
//                            project.ModelId,
//                            project.AspectRatio,
//                            project.BasePrompt,
//                            referenceImages,
//                            project.Status,
//                            project.TotalFrames,
//                            project.CompletedFrames,
//                            Progress = project.TotalFrames > 0 ? (project.CompletedFrames * 100 / project.TotalFrames) : 0,
//                            project.CreatedAt,
//                            project.UpdatedAt
//                        },
//                        frames = frames.Select(f => new
//                        {
//                            f.Id,
//                            f.FrameIndex,
//                            f.FramePrompt,
//                            frameReferenceImages = f.FrameReferenceImages != null
//                                ? JsonSerializer.Deserialize<List<string>>(f.FrameReferenceImages)
//                                : null,
//                            f.Status,
//                            f.ResultImageUrl,
//                            f.ThumbnailUrl,
//                            f.Progress,
//                            f.ProgressMessage,
//                            f.CreatedAt,
//                            f.CompletedAt
//                        }).ToList()
//                    }
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "获取项目详情失败");
//                return StatusCode(500, new { success = false, message = "获取项目详情失败: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// 更新项目信息
//        /// </summary>
//        [HttpPut("projects/{projectId}")]
//        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] UpdateProjectRequest request)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var project = await _storyboardRepository.GetProjectByIdAsync(projectId);

//                if (project == null)
//                {
//                    return NotFound(new { success = false, message = "项目不存在" });
//                }

//                if (project.UserId != userId)
//                {
//                    return Forbid();
//                }

//                // 更新字段
//                project.ProjectName = request.ProjectName ?? project.ProjectName;
//                project.Description = request.Description ?? project.Description;
//                project.BasePrompt = request.BasePrompt ?? project.BasePrompt;
//                project.AspectRatio = request.AspectRatio ?? project.AspectRatio;
//                if (request.ReferenceImages != null)
//                {
//                    project.ReferenceImages = JsonSerializer.Serialize(request.ReferenceImages);
//                }

//                await _storyboardRepository.UpdateProjectAsync(project);
//                _logger.LogInformation($"项目 {projectId} 更新成功");

//                return Ok(new { success = true, message = "项目更新成功" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "更新项目失败");
//                return StatusCode(500, new { success = false, message = "更新项目失败: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// 删除项目（连同其所有帧）
//        /// </summary>
//        [HttpDelete("projects/{projectId}")]
//        public async Task<IActionResult> DeleteProject(int projectId)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var project = await _storyboardRepository.GetProjectByIdAsync(projectId);

//                if (project == null)
//                {
//                    return NotFound(new { success = false, message = "项目不存在" });
//                }

//                if (project.UserId != userId)
//                {
//                    return Forbid();
//                }

//                // 删除所有帧
//                await _storyboardRepository.DeleteProjectFramesAsync(projectId);

//                // 删除项目
//                await _storyboardRepository.DeleteProjectAsync(projectId);
//                _logger.LogInformation($"项目 {projectId} 删除成功");

//                return Ok(new { success = true, message = "项目删除成功" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "删除项目失败");
//                return StatusCode(500, new { success = false, message = "删除项目失败: " + ex.Message });
//            }
//        }

//        // ===== 分镜帧管理 =====

//        /// <summary>
//        /// 添加分镜帧
//        /// </summary>
//        [HttpPost("frames")]
//        public async Task<IActionResult> AddFrame([FromBody] AddFrameRequest request)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var project = await _storyboardRepository.GetProjectByIdAsync(request.ProjectId);

//                if (project == null)
//                {
//                    return NotFound(new { success = false, message = "项目不存在" });
//                }

//                if (project.UserId != userId)
//                {
//                    return Forbid();
//                }

//                var frame = new StoryboardFrame
//                {
//                    ProjectId = request.ProjectId,
//                    FrameIndex = request.FrameIndex,
//                    FramePrompt = request.FramePrompt,
//                    FrameReferenceImages = request.ReferenceImages != null 
//                        ? JsonSerializer.Serialize(request.ReferenceImages) 
//                        : null,
//                    Status = "Pending",
//                    Progress = 0,
//                    ProgressMessage = "等待生成..."
//                };

//                var frameId = await _storyboardRepository.CreateFrameAsync(frame);

//                // 更新项目的总帧数
//                project.TotalFrames = (int)await _storyboardRepository.GetProjectFrameCountAsync(request.ProjectId);
//                project.Status = "InProgress";
//                await _storyboardRepository.UpdateProjectAsync(project);

//                _logger.LogInformation($"分镜帧创建成功，ID: {frameId}，项目ID: {request.ProjectId}");

//                return Ok(new { success = true, frameId, message = "分镜帧添加成功" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "添加分镜帧失败");
//                return StatusCode(500, new { success = false, message = "添加分镜帧失败: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// 删除分镜帧
//        /// </summary>
//        [HttpDelete("frames/{frameId}")]
//        public async Task<IActionResult> DeleteFrame(int frameId)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var frame = await _storyboardRepository.GetFrameByIdAsync(frameId);

//                if (frame == null)
//                {
//                    return NotFound(new { success = false, message = "分镜帧不存在" });
//                }

//                var project = await _storyboardRepository.GetProjectByIdAsync(frame.ProjectId);
//                if (project?.UserId != userId)
//                {
//                    return Forbid();
//                }

//                await _storyboardRepository.DeleteFrameAsync(frameId);

//                // 更新项目的总帧数
//                project.TotalFrames = (int)await _storyboardRepository.GetProjectFrameCountAsync(project.Id);
//                await _storyboardRepository.UpdateProjectAsync(project);

//                _logger.LogInformation($"分镜帧 {frameId} 删除成功");

//                return Ok(new { success = true, message = "分镜帧删除成功" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "删除分镜帧失败");
//                return StatusCode(500, new { success = false, message = "删除分镜帧失败: " + ex.Message });
//            }
//        }

//        // ===== 生成功能 =====

//        /// <summary>
//        /// 生成单个分镜帧
//        /// </summary>
//        [HttpPost("generate-frame")]
//        public async Task<IActionResult> GenerateFrame([FromBody] GenerateFrameRequest request)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var frame = await _storyboardRepository.GetFrameByIdAsync(request.FrameId);

//                if (frame == null)
//                {
//                    return NotFound(new { success = false, message = "分镜帧不存在" });
//                }

//                var project = await _storyboardRepository.GetProjectByIdAsync(frame.ProjectId);
//                if (project?.UserId != userId)
//                {
//                    return Forbid();
//                }

//                // 验证模型
//                var model = await _modelRepository.GetByIdAsync(project.ModelId);
//                if (model == null || !model.IsActive)
//                {
//                    return BadRequest(new { success = false, message = "模型不存在或未激活" });
//                }

//                // 检查积分
//                var userPoints = await _pointsRepository.GetUserPointsAsync(userId);
//                if (userPoints < model.PointCost)
//                {
//                    return BadRequest(new
//                    {
//                        success = false,
//                        message = $"积分不足，当前积分: {userPoints}，所需积分: {model.PointCost}"
//                    });
//                }

//                // 扣除积分
//                await _pointsRepository.DeductPointsAsync(
//                    userId,
//                    model.PointCost,
//                    $"分镜制作 - 项目{project.Id} - 第{frame.FrameIndex}帧"
//                );

//                // 获取参考图列表
//                var referenceImages = new List<string>();
                
//                // 优先使用帧级别的参考图
//                if (!string.IsNullOrEmpty(frame.FrameReferenceImages))
//                {
//                    referenceImages = JsonSerializer.Deserialize<List<string>>(frame.FrameReferenceImages) ?? new List<string>();
//                }
//                else if (!string.IsNullOrEmpty(project.ReferenceImages))
//                {
//                    // 否则使用项目级别的参考图
//                    referenceImages = JsonSerializer.Deserialize<List<string>>(project.ReferenceImages) ?? new List<string>();
//                }

//                // 创建生成任务
//                var task = new DrawingTask
//                {
//                    UserId = userId,
//                    ModelId = project.ModelId,
//                    TaskMode = "storyboard",
//                    Prompt = frame.FramePrompt,
//                    TaskStatus = "Pending",
//                    Progress = 0,
//                    ProgressMessage = "等待处理中...",
//                    ReferenceImages = JsonSerializer.Serialize(referenceImages),
//                    AspectRatio = project.AspectRatio,
//                    CreatedAt = DateTime.Now,
//                    IsR2Uploaded = false,
//                    UrlVersion = 0,
//                    LastUpdatedAt = DateTime.Now
//                };

//                var taskId = await _taskRepository.CreateAsync(task);
//                task.Id = taskId;

//                // 关联分镜帧和任务
//                frame.TaskId = taskId;
//                frame.Status = "Processing";
//                frame.ProgressMessage = "等待队列...";
//                await _storyboardRepository.UpdateFrameAsync(frame);

//                // 加入队列
//                await _taskQueueService.EnqueueTaskAsync(task);

//                _logger.LogInformation($"分镜帧 {request.FrameId} 生成任务创建成功，任务ID: {taskId}");

//                return Ok(new { success = true, taskId, message = "生成任务已提交" });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "生成分镜帧失败");
//                return StatusCode(500, new { success = false, message = "生成分镜帧失败: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// 获取帧的生成进度
//        /// </summary>
//        [HttpGet("frame-progress/{frameId}")]
//        public async Task<IActionResult> GetFrameProgress(int frameId)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var frame = await _storyboardRepository.GetFrameByIdAsync(frameId);

//                if (frame == null)
//                {
//                    return NotFound(new { success = false, message = "分镜帧不存在" });
//                }

//                var project = await _storyboardRepository.GetProjectByIdAsync(frame.ProjectId);
//                if (project?.UserId != userId)
//                {
//                    return Forbid();
//                }

//                // 如果帧有关联的任务，获取任务进度
//                if (frame.TaskId.HasValue)
//                {
//                    var task = await _taskRepository.GetByIdAsync(frame.TaskId.Value);
//                    if (task != null)
//                    {
//                        return Ok(new
//                        {
//                            success = true,
//                            frameId,
//                            taskId = task.Id,
//                            status = task.TaskStatus,
//                            progress = task.Progress,
//                            progressMessage = task.ProgressMessage,
//                            resultImageUrl = task.ResultImageUrl,
//                            thumbnailUrl = task.ThumbnailUrl,
//                            errorMessage = task.ErrorMessage
//                        });
//                    }
//                }

//                return Ok(new
//                {
//                    success = true,
//                    frameId,
//                    status = frame.Status,
//                    progress = frame.Progress,
//                    progressMessage = frame.ProgressMessage,
//                    resultImageUrl = frame.ResultImageUrl,
//                    thumbnailUrl = frame.ThumbnailUrl
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "获取帧进度失败");
//                return StatusCode(500, new { success = false, message = "获取帧进度失败: " + ex.Message });
//            }
//        }

//        // ===== 导出功能 =====

//        /// <summary>
//        /// 导出项目为JSON
//        /// </summary>
//        [HttpPost("projects/{projectId}/export")]
//        public async Task<IActionResult> ExportProject(int projectId)
//        {
//            try
//            {
//                var userId = GetCurrentUserId();
//                var project = await _storyboardRepository.GetProjectByIdAsync(projectId);

//                if (project == null)
//                {
//                    return NotFound(new { success = false, message = "项目不存在" });
//                }

//                if (project.UserId != userId)
//                {
//                    return Forbid();
//                }

//                var frames = await _storyboardRepository.GetProjectFramesAsync(projectId);
//                var model = await _modelRepository.GetByIdAsync(project.ModelId);

//                var exportData = new
//                {
//                    project = new
//                    {
//                        project.Id,
//                        project.ProjectName,
//                        project.Description,
//                        project.AspectRatio,
//                        project.BasePrompt,
//                        project.TotalFrames,
//                        project.CompletedFrames,
//                        modelName = model?.ModelName,
//                        project.CreatedAt,
//                        project.UpdatedAt,
//                        project.CompletedAt
//                    },
//                    frames = frames.Select(f => new
//                    {
//                        f.FrameIndex,
//                        f.FramePrompt,
//                        f.Status,
//                        f.ResultImageUrl,
//                        f.CreatedAt,
//                        f.CompletedAt
//                    }).ToList(),
//                    exportedAt = DateTime.Now
//                };

//                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });

//                // 更新项目导出数据
//                project.ExportData = json;
//                project.Status = "Exported";
//                await _storyboardRepository.UpdateProjectAsync(project);

//                // 返回JSON文件
//                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
//                return File(bytes, "application/json", $"storyboard_{project.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "导出项目失败");
//                return StatusCode(500, new { success = false, message = "导出项目失败: " + ex.Message });
//            }
//        }
//    }

//    // ===== 请求模型 =====

//    public class CreateProjectRequest
//    {
//        public string ProjectName { get; set; } = string.Empty;
//        public string? Description { get; set; }
//        public int ModelId { get; set; }
//        public string? AspectRatio { get; set; }
//        public string? BasePrompt { get; set; }
//        public List<string>? ReferenceImages { get; set; }
//    }

//    public class UpdateProjectRequest
//    {
//        public string? ProjectName { get; set; }
//        public string? Description { get; set; }
//        public string? AspectRatio { get; set; }
//        public string? BasePrompt { get; set; }
//        public List<string>? ReferenceImages { get; set; }
//    }

//    public class AddFrameRequest
//    {
//        public int ProjectId { get; set; }
//        public int FrameIndex { get; set; }
//        public string FramePrompt { get; set; } = string.Empty;
//        public List<string>? ReferenceImages { get; set; }
//    }

//    public class GenerateFrameRequest
//    {
//        public int FrameId { get; set; }
//    }
//}
