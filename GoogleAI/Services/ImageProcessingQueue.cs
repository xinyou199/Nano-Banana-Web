using GoogleAI.Configuration;
using GoogleAI.Models;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace GoogleAI.Services
{
    /// <summary>
    /// 图片处理队列服务 - 专门处理图片压缩和R2上传
    /// </summary>
    public class ImageProcessingQueue : IImageProcessingQueue
    {
        private readonly Channel<ImageProcessingJob> _channel;
        private readonly ILogger<ImageProcessingQueue> _logger;

        public ImageProcessingQueue(
            ILogger<ImageProcessingQueue> logger,
            IOptions<TaskProcessorSettings> options)
        {
            _logger = logger;

            // 使用较大的队列容量，因为图片处理比绘图快
            _channel = Channel.CreateBounded<ImageProcessingJob>(
                new BoundedChannelOptions(options.Value.QueueCapacity * 2)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                });

            _logger.LogInformation($"ImageProcessingQueue 已初始化，队列容量: {options.Value.QueueCapacity * 2}");
        }

        public async Task EnqueueAsync(ImageProcessingJob job)
        {
            try
            {
                await _channel.Writer.WriteAsync(job);
                _logger.LogInformation(
                    $"[图片队列] 任务 {job.TaskId} 已加入图片处理队列，队列长度: {GetQueueLength()}");
            }
            catch (ChannelClosedException)
            {
                _logger.LogError($"[图片队列] 无法加入任务 {job.TaskId}，队列已关闭");
                throw new InvalidOperationException("图片处理队列已关闭");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[图片队列] 加入任务 {job.TaskId} 时发生错误");
                throw;
            }
        }

        public async Task<ImageProcessingJob?> DequeueAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var job = await _channel.Reader.ReadAsync(cancellationToken);
                _logger.LogInformation($"[图片队列] 取出任务 {job.TaskId}，剩余: {GetQueueLength()}");
                return job;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[图片队列] 操作被取消");
                return null;
            }
            catch (ChannelClosedException)
            {
                _logger.LogInformation("[图片队列] 队列已关闭");
                return null;
            }
        }

        public int GetQueueLength()
        {
            return _channel.Reader.Count;
        }

        public void Complete()
        {
            _channel.Writer.Complete();
            _logger.LogInformation("[图片队列] 队列已标记为完成");
        }
    }
}
