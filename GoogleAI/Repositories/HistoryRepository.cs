using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IHistoryRepository
    {
        Task<int> CreateAsync(DrawingHistory history);
        Task<IEnumerable<DrawingHistory>> GetByUserIdAsync(int userId);
        Task<DrawingHistory?> GetByTaskIdAsync(int taskId);
    }

    public interface IPointsRepository
    {
        Task<int> AddPointsAsync(int userId, int points, string description);
        Task<int> GetUserPointsAsync(int userId);
        Task<IEnumerable<PointsHistory>> GetPointsHistoryAsync(int userId);
        Task<bool> DeductPointsAsync(int userId, int points, string description);
    }

    public class HistoryRepository : IHistoryRepository, IPointsRepository
    {
        private readonly string _connectionString;

        public HistoryRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> CreateAsync(DrawingHistory history)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO DrawingHistory (UserId, TaskId, ModelName, Prompt, ImageUrl, TaskMode, CreatedAt) 
                       VALUES (@UserId, @TaskId, @ModelName, @Prompt, @ImageUrl, @TaskMode, @CreatedAt);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, history);
        }

        public async Task<IEnumerable<DrawingHistory>> GetByUserIdAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM DrawingHistory WHERE UserId = @UserId ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<DrawingHistory>(sql, new { UserId = userId });
        }

        public async Task<DrawingHistory?> GetByTaskIdAsync(int taskId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM DrawingHistory WHERE TaskId = @TaskId";
            return await connection.QueryFirstOrDefaultAsync<DrawingHistory>(sql, new { TaskId = taskId });
        }

        public async Task<int> AddPointsAsync(int userId, int points, string description)
        {
            using var connection = new MySqlConnection(_connectionString);
            
            // 添加积分历史记录
            var historySql = @"INSERT INTO PointsHistory (UserId, Points, Description, CreatedAt) 
                              VALUES (@UserId, @Points, @Description, @CreatedAt)";
            await connection.ExecuteAsync(historySql, new { 
                UserId = userId, 
                Points = points, 
                Description = description, 
                CreatedAt = DateTime.Now 
            });

            // 更新用户积分总数
            var updateUserSql = "UPDATE Users SET Points = Points + @Points WHERE Id = @UserId";
            await connection.ExecuteAsync(updateUserSql, new { Points = points, UserId = userId });

            // 返回用户当前积分
            var getUserPointsSql = "SELECT Points FROM Users WHERE Id = @UserId";
            return await connection.QuerySingleAsync<int>(getUserPointsSql, new { UserId = userId });
        }

        public async Task<int> GetUserPointsAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT Points FROM Users WHERE Id = @UserId";
            return await connection.QuerySingleOrDefaultAsync<int>(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<PointsHistory>> GetPointsHistoryAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PointsHistory WHERE UserId = @UserId ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<PointsHistory>(sql, new { UserId = userId });
        }

        public async Task<bool> DeductPointsAsync(int userId, int points, string description)
        {
            using var connection = new MySqlConnection(_connectionString);
            
            // 先检查用户是否有足够的积分
            var userPoints = await GetUserPointsAsync(userId);
            if (userPoints < points)
            {
                return false; // 积分不足
            }

            // 添加积分历史记录（负数表示扣除）
            var historySql = @"INSERT INTO PointsHistory (UserId, Points, Description, CreatedAt) 
                              VALUES (@UserId, @Points, @Description, @CreatedAt)";
            await connection.ExecuteAsync(historySql, new { 
                UserId = userId, 
                Points = -points, 
                Description = description, 
                CreatedAt = DateTime.Now 
            });

            // 更新用户积分总数
            var updateUserSql = "UPDATE Users SET Points = Points - @Points WHERE Id = @UserId";
            await connection.ExecuteAsync(updateUserSql, new { Points = points, UserId = userId });

            return true; // 扣除成功
        }
    }
}