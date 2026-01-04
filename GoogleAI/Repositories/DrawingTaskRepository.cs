using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IDrawingTaskRepository
    {
        Task<int> CreateAsync(DrawingTask task);
        Task<DrawingTask?> GetByIdAsync(int id);
        Task<IEnumerable<DrawingTask>> GetPendingTasksAsync();
        Task<IEnumerable<DrawingTask>> GetUserTasksAsync(int userId, int pageSize = 20, int offset = 0);
        Task<bool> UpdateStatusAsync(int id, string status, string? resultImageUrl = null, string? thumbnailUrl = null, string? errorMessage = null);
        Task<bool> UpdateProgressAsync(int id, int progress, string? progressMessage = null);
        Task<bool> TryAcquireTaskAsync(int taskId);
        Task<IEnumerable<DrawingTask>> GetStuckProcessingTasksAsync(int minutesThreshold);

        Task<bool> DeleteAsync(int taskId);


        // ✅ 新增方法

        /// <summary>
        /// 更新任务为完成状态（使用原始URL）
        /// </summary>
        Task<bool> UpdateToCompletedWithOriginalUrlAsync(
            int taskId,
            string originalImageUrl,
            string originalThumbnailUrl);

        /// <summary>
        /// 条件更新为R2 URL（仅当未上传R2且当前URL匹配时）
        /// </summary>
        Task<bool> UpdateToR2UrlAsync(
            int taskId,
            string r2ImageUrl,
            string r2ThumbnailUrl,
            string expectedOriginalUrl);

        /// <summary>
        /// 获取需要上传到R2的已完成任务
        /// </summary>
        Task<IEnumerable<DrawingTask>> GetPendingR2UploadTasksAsync(int limit = 50);

        /// <summary>
        /// 获取任务统计信息
        /// </summary>
        Task<TaskStatistics> GetTaskStatisticsAsync();

        /// <summary>
        /// 原子性地更新任务状态和历史记录（在事务中）
        /// </summary>
        Task<bool> UpdateTaskAndHistoryAsync(
            int taskId,
            int historyId,
            string imageUrl,
            string thumbnailUrl,
            bool isR2Uploaded);

        /// <summary>
        /// 根据批次组ID获取所有任务
        /// </summary>
        Task<IEnumerable<DrawingTask>> GetTasksByBatchGroupAsync(string batchGroupId);

        /// <summary>
        /// 更新任务（通用更新方法）
        /// </summary>
        Task<bool> UpdateAsync(DrawingTask task);
    }

    public class DrawingTaskRepository : IDrawingTaskRepository
    {
        private readonly string _connectionString;

        public DrawingTaskRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> CreateAsync(DrawingTask task)
        {
            using var connection = new MySqlConnection(_connectionString);
            // 确保 CreatedAt 被设置
            if (task.CreatedAt == default || task.CreatedAt == DateTime.MinValue)
            {
                task.CreatedAt = DateTime.Now;
            }

            var sql = @"INSERT INTO DrawingTasks (UserId, ModelId, TaskMode, Prompt, TaskStatus, ReferenceImages,ResultImageUrl, CreatedAt,AspectRatio) 
                       VALUES (@UserId, @ModelId, @TaskMode, @Prompt, @TaskStatus, @ReferenceImages,@ResultImageUrl,@CreatedAt,@AspectRatio);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, task);
        }

        public async Task<DrawingTask?> GetByIdAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM DrawingTasks WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<DrawingTask>(sql, new { Id = id });
        }

        public async Task<IEnumerable<DrawingTask>> GetPendingTasksAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            // 简单查询，不加锁，不修改状态
            // 状态检查和标记延迟到执行时进行
            var sql = @"SELECT * FROM DrawingTasks 
                       WHERE TaskStatus = 'Pending' 
                       ORDER BY CreatedAt ASC 
                       LIMIT 100";

            return await connection.QueryAsync<DrawingTask>(sql);
        }

        public async Task<IEnumerable<DrawingTask>> GetUserTasksAsync(int userId, int pageSize = 20, int offset = 0)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"SELECT * FROM DrawingTasks 
                       WHERE UserId = @UserId 
                       ORDER BY CreatedAt DESC 
                       LIMIT @PageSize OFFSET @Offset";
            return await connection.QueryAsync<DrawingTask>(sql, new { UserId = userId, PageSize = pageSize, Offset = offset });
        }

        public async Task<bool> UpdateStatusAsync(int id, string status, string? resultImageUrl = null, string? thumbnailUrl = null, string? errorMessage = null)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE DrawingTasks 
                       SET TaskStatus = @Status, 
                           ResultImageUrl = @ResultImageUrl, 
                           ThumbnailUrl = @ThumbnailUrl,
                           ErrorMessage = @ErrorMessage,
                           CompletedAt = CASE WHEN @Status IN ('Completed', 'Failed') THEN CURRENT_TIMESTAMP ELSE CompletedAt END
                       WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id, Status = status, ResultImageUrl = resultImageUrl, ThumbnailUrl = thumbnailUrl, ErrorMessage = errorMessage });
            return result > 0;
        }

        public async Task<bool> UpdateProgressAsync(int id, int progress, string? progressMessage = null)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE DrawingTasks 
                       SET Progress = @Progress, 
                           ProgressMessage = @ProgressMessage
                       WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id, Progress = progress, ProgressMessage = progressMessage });
            return result > 0;
        }

        public async Task<bool> TryAcquireTaskAsync(int taskId)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                // 第一步：检查任务当前状态
                var checkSql = "SELECT TaskStatus FROM DrawingTasks WHERE Id = @TaskId";
                var currentStatus = await connection.QueryFirstOrDefaultAsync<string>(checkSql, new { TaskId = taskId }, transaction: transaction);

                if (string.IsNullOrEmpty(currentStatus))
                {
                    transaction.Rollback();
                    return false; // 任务不存在
                }

                if (currentStatus != "Pending")
                {
                    // 任务不是Pending状态，无法占用
                    Console.WriteLine($"[TryAcquireTaskAsync] 任务 {taskId} 状态不是Pending，当前状态: {currentStatus}");
                    transaction.Rollback();
                    return false;
                }

                // 第二步：原子性地尝试将任务状态从Pending改为Processing
                var updateSql = @"UPDATE DrawingTasks 
                                   SET TaskStatus = 'Processing'
                                   WHERE Id = @TaskId AND TaskStatus = 'Pending'";
                var result = await connection.ExecuteAsync(updateSql, new { TaskId = taskId }, transaction: transaction);

                Console.WriteLine($"[TryAcquireTaskAsync] 任务 {taskId} 尝试占用结果: {result > 0} (影响了 {result} 行)");

                transaction.Commit();
                return result > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TryAcquireTaskAsync] 任务 {taskId} 占用时发生异常: {ex.Message}");
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<DrawingTask>> GetStuckProcessingTasksAsync(int minutesThreshold)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"SELECT * FROM DrawingTasks 
                       WHERE TaskStatus = 'Processing' 
                       AND TIMESTAMPDIFF(MINUTE, CreatedAt, NOW()) > @MinutesThreshold
                       ORDER BY CreatedAt ASC";
            return await connection.QueryAsync<DrawingTask>(sql, new { MinutesThreshold = minutesThreshold });
        }

        // ✅ 新增方法实现
        public async Task<bool> UpdateToCompletedWithOriginalUrlAsync(
            int taskId,
            string originalImageUrl,
            string originalThumbnailUrl)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"
                UPDATE DrawingTasks 
                SET TaskStatus = 'Completed',
                    ResultImageUrl = @OriginalImageUrl,
                    ThumbnailUrl = @OriginalThumbnailUrl,
                    OriginalImageUrl = @OriginalImageUrl,
                    IsR2Uploaded = FALSE,
                    UrlVersion = UrlVersion + 1,
                    CompletedAt = CURRENT_TIMESTAMP,
                    Progress = 100,
                    ProgressMessage = '绘图完成，图片处理中...'
                WHERE Id = @TaskId";

            var result = await connection.ExecuteAsync(sql, new
            {
                TaskId = taskId,
                OriginalImageUrl = originalImageUrl,
                OriginalThumbnailUrl = originalThumbnailUrl
            });

            return result > 0;
        }
        public async Task<bool> UpdateToR2UrlAsync(
            int taskId,
            string r2ImageUrl,
            string r2ThumbnailUrl,
            string expectedOriginalUrl)
        {
            using var connection = new MySqlConnection(_connectionString);

            // ✅ 条件更新：只有当前URL匹配且未上传R2时才更新
            var sql = @"
                UPDATE DrawingTasks 
                SET ResultImageUrl = @R2ImageUrl,
                    ThumbnailUrl = @R2ThumbnailUrl,
                    IsR2Uploaded = TRUE,
                    UrlVersion = UrlVersion + 1,
                    ProgressMessage = '图片已上传到云存储'
                WHERE Id = @TaskId 
                  AND IsR2Uploaded = FALSE
                  AND OriginalImageUrl = @ExpectedOriginalUrl
                  AND TaskStatus = 'Completed'";

            var result = await connection.ExecuteAsync(sql, new
            {
                TaskId = taskId,
                R2ImageUrl = r2ImageUrl,
                R2ThumbnailUrl = r2ThumbnailUrl,
                ExpectedOriginalUrl = expectedOriginalUrl
            });

            return result > 0;
        }
        public async Task<IEnumerable<DrawingTask>> GetPendingR2UploadTasksAsync(int limit = 50)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"
                SELECT * FROM DrawingTasks 
                WHERE TaskStatus = 'Completed' 
                  AND IsR2Uploaded = FALSE
                  AND OriginalImageUrl IS NOT NULL
                ORDER BY CompletedAt ASC 
                LIMIT @Limit";

            return await connection.QueryAsync<DrawingTask>(sql, new { Limit = limit });
        }
        public async Task<TaskStatistics> GetTaskStatisticsAsync()
        {
            using var connection = new MySqlConnection(_connectionString);

            // 使用视图或直接查询
            var sql = @"
                SELECT 
                    COUNT(CASE WHEN TaskStatus = 'Pending' THEN 1 END) AS PendingCount,
                    COUNT(CASE WHEN TaskStatus = 'Processing' THEN 1 END) AS ProcessingCount,
                    COUNT(CASE WHEN TaskStatus = 'Completed' THEN 1 END) AS CompletedCount,
                    COUNT(CASE WHEN TaskStatus = 'Failed' THEN 1 END) AS FailedCount,
                    COUNT(CASE WHEN TaskStatus = 'Completed' AND IsR2Uploaded = FALSE THEN 1 END) AS PendingR2UploadCount,
                    NOW() AS StatisticsTime
                FROM DrawingTasks
                WHERE CreatedAt >= DATE_SUB(NOW(), INTERVAL 24 HOUR)";

            var stats = await connection.QueryFirstOrDefaultAsync<TaskStatistics>(sql);
            return stats ?? new TaskStatistics { StatisticsTime = DateTime.Now };
        }
        public async Task<bool> UpdateTaskAndHistoryAsync(
            int taskId,
            int historyId,
            string imageUrl,
            string thumbnailUrl,
            bool isR2Uploaded)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // 更新任务记录
                var updateTaskSql = @"
                    UPDATE DrawingTasks 
                    SET ResultImageUrl = @ImageUrl,
                        ThumbnailUrl = @ThumbnailUrl,
                        IsR2Uploaded = @IsR2Uploaded,
                        UrlVersion = UrlVersion + 1
                    WHERE Id = @TaskId";

                await connection.ExecuteAsync(updateTaskSql, new
                {
                    TaskId = taskId,
                    ImageUrl = imageUrl,
                    ThumbnailUrl = thumbnailUrl,
                    IsR2Uploaded = isR2Uploaded
                }, transaction: transaction);

                // 更新历史记录
                var updateHistorySql = @"
                    UPDATE DrawingHistory 
                    SET ImageUrl = @ImageUrl,
                        ThumbnailUrl = @ThumbnailUrl,
                        IsR2Uploaded = @IsR2Uploaded,
                        UrlVersion = UrlVersion + 1
                    WHERE Id = @HistoryId";

                await connection.ExecuteAsync(updateHistorySql, new
                {
                    HistoryId = historyId,
                    ImageUrl = imageUrl,
                    ThumbnailUrl = thumbnailUrl,
                    IsR2Uploaded = isR2Uploaded
                }, transaction: transaction);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int taskId)
        {
            using var connection = new MySqlConnection(_connectionString);
            int count = await connection.ExecuteAsync(
                  "DELETE FROM DrawingTasks WHERE Id = @TaskId",
                  new { TaskId = taskId }
              );
            return count > 0;
        }

        public async Task<IEnumerable<DrawingTask>> GetTasksByBatchGroupAsync(string batchGroupId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"SELECT * FROM DrawingTasks 
                       WHERE BatchGroupId = @BatchGroupId 
                       ORDER BY SplitIndex ASC";
            return await connection.QueryAsync<DrawingTask>(sql, new { BatchGroupId = batchGroupId });
        }

        public async Task<bool> UpdateAsync(DrawingTask task)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"
                UPDATE DrawingTasks 
                SET UserId = @UserId,
                    ModelId = @ModelId,
                    TaskMode = @TaskMode,
                    Prompt = @Prompt,
                    TaskStatus = @TaskStatus,
                    ResultImageUrl = @ResultImageUrl,
                    ThumbnailUrl = @ThumbnailUrl,
                    ErrorMessage = @ErrorMessage,
                    ReferenceImages = @ReferenceImages,
                    CompletedAt = @CompletedAt,
                    AspectRatio = @AspectRatio,
                    Progress = @Progress,
                    ProgressMessage = @ProgressMessage,
                    IsR2Uploaded = @IsR2Uploaded,
                    UrlVersion = @UrlVersion,
                    OriginalImageUrl = @OriginalImageUrl,
                    LastUpdatedAt = @LastUpdatedAt,
                    ParentTaskId = @ParentTaskId,
                    SplitIndex = @SplitIndex,
                    BatchGroupId = @BatchGroupId,
                    ProcessMode = @ProcessMode,
                    Tolerance = @Tolerance
                WHERE Id = @Id";

            var result = await connection.ExecuteAsync(sql, task);
            return result > 0;
        }
    }
}
