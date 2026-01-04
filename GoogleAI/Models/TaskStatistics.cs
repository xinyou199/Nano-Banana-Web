namespace GoogleAI.Models
{
    /// <summary>
    /// 任务统计信息
    /// </summary>
    public class TaskStatistics
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int CompletedCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingR2UploadCount { get; set; }
        public DateTime StatisticsTime { get; set; }
    }

    public class TaskRequest 
    {
        public List<string> Images { get; set; }

        public string Image { get; set; }

        public string Message { get; set; }

        public bool Status { get; set; }
    }
}
