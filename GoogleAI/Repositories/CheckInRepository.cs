using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface ICheckInRepository
    {
        Task<UserCheckIn?> GetUserCheckInAsync(int userId, DateTime checkInDate);
        Task<UserCheckIn?> GetLastCheckInAsync(int userId);
        Task<int> GetConsecutiveDaysAsync(int userId);
        Task<UserCheckIn> CreateAsync(UserCheckIn checkIn);
        Task<IEnumerable<UserCheckIn>> GetUserCheckInsAsync(int userId, int page, int pageSize);
        Task<int> CountByUserIdAsync(int userId);
    }

    public class CheckInRepository : ICheckInRepository
    {
        private readonly string _connectionString;

        public CheckInRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<UserCheckIn?> GetUserCheckInAsync(int userId, DateTime checkInDate)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM UserCheckIns WHERE UserId = @UserId AND CheckInDate = @CheckInDate";
            return await connection.QueryFirstOrDefaultAsync<UserCheckIn>(sql, new { UserId = userId, CheckInDate = checkInDate.Date });
        }

        public async Task<UserCheckIn?> GetLastCheckInAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM UserCheckIns WHERE UserId = @UserId ORDER BY CheckInDate DESC LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<UserCheckIn>(sql, new { UserId = userId });
        }

        public async Task<int> GetConsecutiveDaysAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"
                WITH RECURSIVE DateSequence AS (
                    SELECT CheckInDate, 1 AS DayNum
                    FROM UserCheckIns
                    WHERE UserId = @UserId
                    ORDER BY CheckInDate DESC
                    LIMIT 1
                    
                    UNION ALL
                    
                    SELECT DATE_SUB(u.CheckInDate, INTERVAL 1 DAY) AS CheckInDate, ds.DayNum + 1
                    FROM UserCheckIns u
                    INNER JOIN DateSequence ds ON DATE_SUB(u.CheckInDate, INTERVAL 1 DAY) = ds.CheckInDate
                    WHERE u.UserId = @UserId
                )
                SELECT MAX(DayNum) FROM DateSequence;
            ";
            
            try
            {
                return await connection.QuerySingleOrDefaultAsync<int>(sql, new { UserId = userId });
            }
            catch
            {
                // 如果数据库不支持 WITH RECURSIVE，使用备用查询
                return await GetConsecutiveDaysFallbackAsync(connection, userId);
            }
        }

        private async Task<int> GetConsecutiveDaysFallbackAsync(MySqlConnection connection, int userId)
        {
            var checkIns = await connection.QueryAsync<DateTime>(
                "SELECT CheckInDate FROM UserCheckIns WHERE UserId = @UserId ORDER BY CheckInDate DESC",
                new { UserId = userId });
            
            var dates = checkIns.ToList();
            if (!dates.Any()) return 0;

            int consecutiveDays = 1;
            DateTime currentDate = DateTime.Today;
            
            foreach (var date in dates)
            {
                if (date == currentDate)
                {
                    currentDate = currentDate.AddDays(-1);
                    consecutiveDays++;
                }
                else if (date == currentDate.AddDays(-1))
                {
                    currentDate = currentDate.AddDays(-1);
                    consecutiveDays++;
                }
                else
                {
                    break;
                }
            }

            return consecutiveDays;
        }

        public async Task<UserCheckIn> CreateAsync(UserCheckIn checkIn)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO UserCheckIns (UserId, CheckInDate, Points, CreatedAt)
                       VALUES (@UserId, @CheckInDate, @Points, @CreatedAt);
                       SELECT LAST_INSERT_ID();";
            checkIn.Id = await connection.ExecuteScalarAsync<int>(sql, checkIn);
            return checkIn;
        }

        public async Task<IEnumerable<UserCheckIn>> GetUserCheckInsAsync(int userId, int page, int pageSize)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"SELECT * FROM UserCheckIns 
                       WHERE UserId = @UserId 
                       ORDER BY CheckInDate DESC 
                       LIMIT @Limit OFFSET @Offset";
            return await connection.QueryAsync<UserCheckIn>(sql, new { 
                UserId = userId, 
                Limit = pageSize, 
                Offset = (page - 1) * pageSize 
            });
        }

        public async Task<int> CountByUserIdAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT COUNT(*) FROM UserCheckIns WHERE UserId = @UserId";
            return await connection.QuerySingleAsync<int>(sql, new { UserId = userId });
        }
    }
}
