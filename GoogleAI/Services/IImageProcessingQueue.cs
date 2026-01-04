using GoogleAI.Models;

namespace GoogleAI.Services
{
    /// <summary>
    /// 图片处理队列接口
    /// </summary>
    public interface IImageProcessingQueue
    {
        /// <summary>
        /// 将图片处理任务加入队列
        /// </summary>
        Task EnqueueAsync(ImageProcessingJob job);

        /// <summary>
        /// 从队列中获取图片处理任务
        /// </summary>
        Task<ImageProcessingJob?> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取队列长度
        /// </summary>
        int GetQueueLength();
    }
}
