using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IEmailVerificationRepository
    {
        Task<int> CreateAsync(EmailVerification verification);
        Task<EmailVerification?> GetLatestByEmailAsync(string email);
        Task<bool> MarkAsUsedAsync(int id);
        Task<bool> DeleteExpiredAsync();
    }

    public class EmailVerificationRepository : IEmailVerificationRepository
    {
        private readonly string _connectionString;

        public EmailVerificationRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> CreateAsync(EmailVerification verification)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO EmailVerification (Email, VerificationCode, CreatedAt, ExpiresAt, IsUsed) 
                       VALUES (@Email, @VerificationCode, @CreatedAt, @ExpiresAt, @IsUsed);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, verification);
        }

        public async Task<EmailVerification?> GetLatestByEmailAsync(string email)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"SELECT * FROM EmailVerification 
                       WHERE Email = @Email AND IsUsed = 0 AND ExpiresAt > NOW() 
                       ORDER BY CreatedAt DESC LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<EmailVerification>(sql, new { Email = email });
        }

        public async Task<bool> MarkAsUsedAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "UPDATE EmailVerification SET IsUsed = 1 WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id });
            return result > 0;
        }

        public async Task<bool> DeleteExpiredAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "DELETE FROM EmailVerification WHERE ExpiresAt < NOW() OR IsUsed = 1";
            var result = await connection.ExecuteAsync(sql);
            return result > 0;
        }
    }
}