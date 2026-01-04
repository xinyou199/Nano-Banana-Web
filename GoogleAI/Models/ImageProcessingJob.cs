namespace GoogleAI.Models
{
    /// <summary>
    /// 图片处理任务
    /// </summary>
    public class ImageProcessingJob
    {
        public int TaskId { get; set; }
        public int HistoryId { get; set; }
        public string OriginalUrl { get; set; } = string.Empty;
        public List<string> AllImageUrls { get; set; } = new();
        public DateTime EnqueuedAt { get; set; } = DateTime.Now;
    }
}
