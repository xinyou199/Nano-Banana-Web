using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoogleAI.Services;
using GoogleAI.Repositories;
using GoogleAI.Models;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DebugController : ControllerBase
    {
        private readonly ITaskQueueService _taskQueueService;
        private readonly IDrawingTaskRepository _taskRepository;
        private readonly ILogger<DebugController> _logger;

        public DebugController(
            ITaskQueueService taskQueueService,
            IDrawingTaskRepository taskRepository,
            ILogger<DebugController> logger)
        {
            _taskQueueService = taskQueueService;
            _taskRepository = taskRepository;
            _logger = logger;
        }

        /// <summary>
        /// 获取队列状态
        /// </summary>
        [HttpGet("queue-status")]
        public IActionResult GetQueueStatus()
        {
            try
            {
                var queueLength = _taskQueueService.GetQueueLength();
                
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        queueLength = queueLength,
                        timestamp = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取队列状态失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 获取所有Pending状态的任务
        /// </summary>
        [HttpGet("pending-tasks")]
        public async Task<IActionResult> GetPendingTasks()
        {
            try
            {
                var pendingTasks = await _taskRepository.GetPendingTasksAsync();
                
                return Ok(new
                {
                    success = true,
                    data = pendingTasks.Select(t => new
                    {
                        t.Id,
                        t.TaskStatus,
                        t.CreatedAt,
                        t.Prompt
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Pending任务失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 手动将指定任务加入队列
        /// </summary>
        [HttpPost("enqueue-task/{taskId}")]
        public async Task<IActionResult> EnqueueTask(int taskId)
        {
            try
            {
                var task = await _taskRepository.GetByIdAsync(taskId);
                if (task == null)
                {
                    return NotFound(new { success = false, message = "任务不存在" });
                }

                // 直接将任务加入队列，队列服务会管理状态
                await _taskQueueService.EnqueueTaskAsync(task);
                
                return Ok(new { success = true, message = $"任务 {taskId} 已加入队列" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"手动入队任务 {taskId} 失败");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}