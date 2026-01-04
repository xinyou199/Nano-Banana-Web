namespace GoogleAI.Configuration
{
    public class TaskProcessorSettings
    {
        /// <summary>
        /// 队列最大容量
        /// </summary>
        public int QueueCapacity { get; set; } = 100;

        /// <summary>
        /// 最大并发任务数
        /// </summary>
        public int MaxConcurrentTasks { get; set; } = 3;

        /// <summary>
        /// 任务超时时间（分钟）
        /// </summary>
        public int TaskTimeoutMinutes { get; set; } = 10;

        /// <summary>
        /// 卡住任务清理阈值（分钟）
        /// </summary>
        public int StuckTaskThresholdMinutes { get; set; } = 15;

        // ✅ 新增图片处理相关配置

        /// <summary>
        /// 图片处理并发数
        /// </summary>
        public int ImageProcessingConcurrency { get; set; } = 2;

        /// <summary>
        /// JPEG压缩质量（1-100）
        /// </summary>
        public int JpegQuality { get; set; } = 85;

        /// <summary>
        /// 缩略图宽度
        /// </summary>
        public int ThumbnailWidth { get; set; } = 400;

        /// <summary>
        /// 缩略图高度
        /// </summary>
        public int ThumbnailHeight { get; set; } = 400;

        /// <summary>
        /// 缩略图质量（1-100）
        /// </summary>
        public int ThumbnailQuality { get; set; } = 75;

        /// <summary>
        /// 统计信息缓存时间（秒）
        /// </summary>
        public int StatisticsCacheSeconds { get; set; } = 5;
        public object ProgressUpdateIntervalSeconds { get; internal set; }
        public object StuckTaskDetectionMinutes { get; internal set; }


        public string Prompt4k { get; set; }

        public string Prompt4kFace { get; set; }
    }
}
