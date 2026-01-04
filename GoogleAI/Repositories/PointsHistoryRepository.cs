using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IPointsHistoryRepository
    {
        Task<IEnumerable<PointsHistory>> GetByUserIdAsync(int userId, int page = 1, int pageSize = 20);
        Task<int> CountByUserIdAsync(int userId);
        Task<int> CreateAsync(PointsHistory pointsHistory);
    }

    public class PointsHistoryRepository : IPointsHistoryRepository
    {
        private readonly string _connectionString;

        public PointsHistoryRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<PointsHistory>> GetByUserIdAsync(int userId, int page = 1, int pageSize = 20)
        {
            using var connection = new MySqlConnection(_connectionString);
            var offset = (page - 1) * pageSize;
            var sql = @"SELECT * FROM PointsHistory 
                       WHERE UserId = @UserId 
                       ORDER BY CreatedAt DESC 
                       LIMIT @Limit OFFSET @Offset";
            return await connection.QueryAsync<PointsHistory>(sql, new { UserId = userId, Limit = pageSize, Offset = offset });
        }

        public async Task<int> CountByUserIdAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT COUNT(*) FROM PointsHistory WHERE UserId = @UserId";
            return await connection.QuerySingleAsync<int>(sql, new { UserId = userId });
        }

        public async Task<int> CreateAsync(PointsHistory pointsHistory)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO PointsHistory (UserId, TaskId, Points, Description) 
                       VALUES (@UserId, @TaskId, @Points, @Description);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, pointsHistory);
        }
    }
}