using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoogleAI.Services;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QueueStatusController : ControllerBase
    {
        private readonly ITaskQueueService _taskQueueService;
        private readonly ILogger<QueueStatusController> _logger;

        public QueueStatusController(
            ITaskQueueService taskQueueService,
            ILogger<QueueStatusController> logger)
        {
            _taskQueueService = taskQueueService;
            _logger = logger;
        }

        /// <summary>
        /// 获取任务队列状态
        /// </summary>
        [HttpGet("status")]
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
                        timestamp = DateTime.Now,
                        status = queueLength == 0 ? "空闲" : queueLength < 5 ? "正常" : "繁忙"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取队列状态时发生错误");
                return StatusCode(500, new { success = false, message = $"获取队列状态失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// ✅ 新增：获取详细诊断信息
        /// </summary>
        [HttpGet("diagnostics")]
        public IActionResult GetDiagnostics()
        {
            return Ok(new { success = false, message = "诊断信息功能暂未启用" });
        }

        private string GetStatusDescription(QueueDiagnostics diagnostics)
        {
            //if (diagnostics.IsChannelClosed)
            //    return "队列已关闭";

            //if (diagnostics.QueueLength == 0 && diagnostics.ProcessingCount == 0)
            //    return "空闲";

            //if (diagnostics.QueueLength < 5)
            //    return "正常";

            //if (diagnostics.QueueLength < 20)
            //    return "繁忙";

            return "拥堵";
        }
    }
}
