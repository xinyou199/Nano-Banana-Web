using GoogleAI.Configuration;
using GoogleAI.Repositories;
using Microsoft.Extensions.Options;

namespace GoogleAI.Services
{
    /// <summary>
    /// 卡住任务清理后台服务 - 定期清理超时或卡住的任务
    /// </summary>
    public class StuckTaskCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StuckTaskCleanupService> _logger;
        private readonly TaskProcessorSettings _settings;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // 每5分钟检查一次

        public StuckTaskCleanupService(
            IServiceProvider serviceProvider,
            ILogger<StuckTaskCleanupService> logger,
            IOptions<TaskProcessorSettings> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [启动] StuckTaskCleanupService 正在启动\n" +
                $"  - 检查间隔: {_cleanupInterval.TotalMinutes} 分钟\n" +
                $"  - 卡住任务阈值: {_settings.StuckTaskThresholdMinutes} 分钟");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupStuckTasksAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[清理服务] 清理卡住任务时发生错误");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }

            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [停止] StuckTaskCleanupService 已停止");
        }

        private async Task CleanupStuckTasksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var taskRepository = scope.ServiceProvider.GetRequiredService<IDrawingTaskRepository>();

            var stuckTasks = await taskRepository.GetStuckProcessingTasksAsync(
                _settings.StuckTaskThresholdMinutes);

            if (stuckTasks.Any())
            {
                _logger.LogWarning($"[清理服务] 发现 {stuckTasks.Count()} 个卡住的任务");

                foreach (var task in stuckTasks)
                {
                    await taskRepository.UpdateStatusAsync(
                        task.Id,
                        "Failed",
                        errorMessage: $"任务超时（超过 {_settings.StuckTaskThresholdMinutes} 分钟未完成）");

                    _logger.LogWarning($"[清理服务] 任务 {task.Id} 已标记为失败（超时）");
                }
            }
        }
    }
}
