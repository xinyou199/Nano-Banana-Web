using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IDrawingHistoryRepository
    {
        Task<int> CreateAsync(DrawingHistory history);
        Task<IEnumerable<DrawingHistory>> GetUserHistoryAsync(int userId, int pageSize = 20, int offset = 0);
        Task<int> GetUserHistoryCountAsync(int userId);
        Task<bool> DeleteAsync(int id);
        Task<bool> ClearUserHistoryAsync(int userId);
        Task<bool> UpdateImageUrlAsync(int id, string imageUrl, string thumbnailUrl);
        Task<bool> UpdateImageUrlByTaskIdAsync(int taskId, string imageUrl, string thumbnailUrl);

        // ✅ 新增方法

        /// <summary>
        /// 条件更新为R2 URL
        /// </summary>
        Task<bool> UpdateToR2UrlByTaskIdAsync(
            int taskId,
            string r2ImageUrl,
            string r2ThumbnailUrl,
            string expectedOriginalUrl);
    }

    public class DrawingHistoryRepository : IDrawingHistoryRepository
    {
        private readonly string _connectionString;

        public DrawingHistoryRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> CreateAsync(DrawingHistory history)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"
                INSERT INTO DrawingHistory (
                    UserId, TaskId, ModelName, Prompt, ImageUrl, ThumbnailUrl, TaskMode,
                    IsR2Uploaded, OriginalImageUrl
                ) 
                VALUES (
                    @UserId, @TaskId, @ModelName, @Prompt, @ImageUrl, @ThumbnailUrl, @TaskMode,
                    @IsR2Uploaded, @OriginalImageUrl
                );
                SELECT LAST_INSERT_ID();";

            return await connection.ExecuteScalarAsync<int>(sql, history);
        }

        public async Task<bool> UpdateToR2UrlByTaskIdAsync(
     int taskId,
     string r2ImageUrl,
     string r2ThumbnailUrl,
     string expectedOriginalUrl)
        {
            using var connection = new MySqlConnection(_connectionString);

            // ✅ 条件更新：只有当前URL匹配且未上传R2时才更新
            var sql = @"
                UPDATE DrawingHistory 
                SET ImageUrl = @R2ImageUrl,
                    ThumbnailUrl = @R2ThumbnailUrl,
                    IsR2Uploaded = TRUE,
                    UrlVersion = UrlVersion + 1
                WHERE TaskId = @TaskId 
                  AND IsR2Uploaded = FALSE
                  AND OriginalImageUrl = @ExpectedOriginalUrl";

            var result = await connection.ExecuteAsync(sql, new
            {
                TaskId = taskId,
                R2ImageUrl = r2ImageUrl,
                R2ThumbnailUrl = r2ThumbnailUrl,
                ExpectedOriginalUrl = expectedOriginalUrl
            });

            return result > 0;
        }

        public async Task<IEnumerable<DrawingHistory>> GetUserHistoryAsync(int userId, int pageSize = 20, int offset = 0)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"SELECT * FROM DrawingHistory 
                       WHERE UserId = @UserId 
                       ORDER BY CreatedAt DESC 
                       LIMIT @PageSize OFFSET @Offset";
            return await connection.QueryAsync<DrawingHistory>(sql, new { UserId = userId, PageSize = pageSize, Offset = offset });
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "DELETE FROM DrawingHistory WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id });
            return result > 0;
        }

        public async Task<int> GetUserHistoryCountAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT COUNT(*) FROM DrawingHistory WHERE UserId = @UserId";
            return await connection.QuerySingleAsync<int>(sql, new { UserId = userId });
        }

        public async Task<bool> ClearUserHistoryAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "DELETE FROM DrawingHistory WHERE UserId = @UserId";
            var result = await connection.ExecuteAsync(sql, new { UserId = userId });
            return result > 0;
        }

        public async Task<bool> UpdateImageUrlAsync(int id, string imageUrl, string thumbnailUrl)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "UPDATE DrawingHistory SET ImageUrl = @ImageUrl, ThumbnailUrl=@ThumbnailUrl WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { ImageUrl = imageUrl, ThumbnailUrl = thumbnailUrl, Id = id });
            return result > 0;
        }

        public async Task<bool> UpdateImageUrlByTaskIdAsync(int taskId, string imageUrl, string thumbnailUrl)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "UPDATE DrawingHistory SET ImageUrl = @ImageUrl, ThumbnailUrl=@ThumbnailUrl WHERE TaskId = @TaskId";
            var result = await connection.ExecuteAsync(sql, new { ImageUrl = imageUrl, ThumbnailUrl = thumbnailUrl, TaskId = taskId });
            return result > 0;
        }
    }
}
