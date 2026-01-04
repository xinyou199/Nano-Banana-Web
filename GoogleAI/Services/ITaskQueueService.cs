using GoogleAI.Models;

namespace GoogleAI.Services
{
    /// <summary>
    /// 任务队列服务接口 - 移除内存状态,依赖数据库
    /// </summary>
    public interface ITaskQueueService
    {
        /// <summary>
        /// 将任务加入队列
        /// </summary>
        Task EnqueueTaskAsync(DrawingTask task);

        /// <summary>
        /// 从队列中获取待处理的任务(阻塞等待,自动标记为 Processing)
        /// </summary>
        Task<DrawingTask?> DequeueAndMarkProcessingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记任务为完成状态(使用原始URL)
        /// </summary>
        Task MarkTaskCompletedWithOriginalUrlAsync(int taskId, string originalImageUrl, string originalThumbnailUrl);

        /// <summary>
        /// 标记任务为失败状态
        /// </summary>
        Task MarkTaskFailedAsync(int taskId, string errorMessage);

        /// <summary>
        /// 更新任务进度
        /// </summary>
        Task UpdateTaskProgressAsync(int taskId, int progress, string progressMessage);

        /// <summary>
        /// 获取队列中等待处理任务数量(Channel队列)
        /// </summary>
        int GetQueueLength();

        /// <summary>
        /// 获取队列诊断信息(包含数据库统计)
        /// </summary>
        Task<QueueDiagnostics> GetDiagnosticsAsync();
    }

    /// <summary>
    /// 队列诊断信息
    /// </summary>
    public class QueueDiagnostics
    {
        /// <summary>
        /// Channel队列中等待任务数
        /// </summary>
        public int ChannelQueueLength { get; set; }

        /// <summary>
        /// 数据库中Pending状态任务数
        /// </summary>
        public int PendingCount { get; set; }

        /// <summary>
        /// 正在处理的任务数
        /// </summary>
        public int ProcessingCount { get; set; }

        /// <summary>
        /// 已完成任务数
        /// </summary>
        public int CompletedCount { get; set; }

        /// <summary>
        /// 失败任务数
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// 等待上传R2的任务数
        /// </summary>
        public int PendingR2UploadCount { get; set; }

        /// <summary>
        /// 队列是否已关闭
        /// </summary>
        public bool IsChannelClosed { get; set; }

        /// <summary>
        /// 诊断时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        public int MaxCapacity { get; set; }
        public int MaxConcurrent { get; set; }
    }
}
