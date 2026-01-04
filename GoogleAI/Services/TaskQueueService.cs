using GoogleAI.Configuration;
using GoogleAI.Models;
using GoogleAI.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace GoogleAI.Services
{
    public class TaskQueueService : ITaskQueueService
    {
        private readonly Channel<DrawingTask> _taskChannel;

        private readonly IDrawingTaskRepository _taskRepository;
        private readonly ILogger<TaskQueueService> _logger;
        private readonly TaskProcessorSettings _settings;
        private readonly IMemoryCache _cache; // ✅ 添加缓存用于统计信息

        public TaskQueueService(
            IDrawingTaskRepository taskRepository,
            ILogger<TaskQueueService> logger,
            IOptions<TaskProcessorSettings> options,
            IMemoryCache cache)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _settings = options.Value;
            _cache = cache;

            _taskChannel = Channel.CreateBounded<DrawingTask>(new BoundedChannelOptions(_settings.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });

            _logger.LogInformation(
                $"TaskQueueService 已初始化，队列容量: {_settings.QueueCapacity}, " +
                $"最大并发: {_settings.MaxConcurrentTasks}");
        }

        public async Task EnqueueTaskAsync(DrawingTask task)
        {
            try
            {
                task.TaskStatus = "Pending";

                await _taskChannel.Writer.WriteAsync(task);
                await _taskRepository.UpdateStatusAsync(task.Id, "Pending");

                _logger.LogInformation(
                    $"[队列] 任务 {task.Id} 已加入队列，当前队列长度: {GetQueueLength()}/{_settings.QueueCapacity}");
            }
            catch (ChannelClosedException)
            {
                _logger.LogError($"[队列] 无法加入任务 {task.Id}，队列已关闭");
                throw new InvalidOperationException("任务队列已关闭，无法接受新任务");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[队列] 将任务 {task.Id} 加入队列时发生错误");
                throw;
            }
        }

        public async Task<DrawingTask?> DequeueAndMarkProcessingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var task = await _taskChannel.Reader.ReadAsync(cancellationToken);

                // ✅ 只更新数据库状态
                await _taskRepository.UpdateStatusAsync(task.Id, "Processing");
                task.TaskStatus = "Processing";

                _logger.LogInformation(
                    $"[队列] 从队列中取出任务 {task.Id}，" +
                    $"剩余: {GetQueueLength()}/{_settings.QueueCapacity}");

                return task;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[队列] 任务获取操作被取消");
                return null;
            }
            catch (ChannelClosedException)
            {
                _logger.LogInformation("[队列] 队列已关闭，无更多任务");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[队列] 从队列中获取任务时发生错误");
                return null;
            }
        }

        public async Task MarkTaskCompletedWithOriginalUrlAsync(
            int taskId,
            string originalImageUrl,
            string originalThumbnailUrl)
        {
            try
            {
                // ✅ 只更新数据库，标记为使用原始URL
                await _taskRepository.UpdateToCompletedWithOriginalUrlAsync(
                    taskId,
                    originalImageUrl,
                    originalThumbnailUrl);

                _logger.LogInformation($"[队列] 任务 {taskId} 已完成(原始URL)");

                // 清除缓存的统计信息
                _cache.Remove("task_statistics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[队列] 标记任务 {taskId} 完成时发生错误");
                throw;
            }
        }

        public async Task MarkTaskFailedAsync(int taskId, string errorMessage)
        {
            try
            {
                // ✅ 只更新数据库
                await _taskRepository.UpdateStatusAsync(taskId, "Failed", errorMessage: errorMessage);
                await _taskRepository.UpdateProgressAsync(taskId, 0, $"处理失败: {errorMessage}");

                _logger.LogWarning($"[队列] 任务 {taskId} 失败: {errorMessage}");

                // 清除缓存的统计信息
                _cache.Remove("task_statistics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[队列] 标记任务 {taskId} 失败时发生错误");
                throw;
            }
        }

        public async Task UpdateTaskProgressAsync(int taskId, int progress, string progressMessage)
        {
            try
            {
                await _taskRepository.UpdateProgressAsync(taskId, progress, progressMessage);
                _logger.LogDebug($"[队列] 任务 {taskId} 进度: {progress}% - {progressMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[队列] 更新任务 {taskId} 进度时发生错误");
            }
        }

        public int GetQueueLength()
        {
            return _taskChannel.Reader.Count;
        }

        public async Task<QueueDiagnostics> GetDiagnosticsAsync()
        {
            // ✅ 使用缓存避免频繁查询数据库
            var stats = await _cache.GetOrCreateAsync("task_statistics", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5); // 5秒缓存
                return await _taskRepository.GetTaskStatisticsAsync();
            });

            return new QueueDiagnostics
            {
                ChannelQueueLength = GetQueueLength(),
                PendingCount = stats?.PendingCount ?? 0,
                ProcessingCount = stats?.ProcessingCount ?? 0,
                CompletedCount = stats?.CompletedCount ?? 0,
                FailedCount = stats?.FailedCount ?? 0,
                PendingR2UploadCount = stats?.PendingR2UploadCount ?? 0,
                IsChannelClosed = _taskChannel.Reader.Completion.IsCompleted,
                Timestamp = DateTime.Now,
                MaxCapacity = _settings.QueueCapacity,
                MaxConcurrent = _settings.MaxConcurrentTasks
            };
        }

        public void CompleteQueue()
        {
            _taskChannel.Writer.Complete();
            _logger.LogInformation("[队列] 队列已标记为完成");
        }
    }
}
