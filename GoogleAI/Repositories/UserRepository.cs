using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByOpenIdAsync(string openId);
        Task<int> CreateAsync(User user);
        Task UpdateAsync(User user);
        Task<bool> UpdateUserTokenAsync(int userId, string token);
        Task<bool> IsTokenValidAsync(int userId, string token);
        Task<IEnumerable<User>> GetAllAsync();
    }

    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Users WHERE Username = @Username AND IsActive = 1";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Users WHERE Id = @Id AND IsActive = 1";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Users WHERE Email = @Email AND IsActive = 1";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<User?> GetByOpenIdAsync(string openId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Users WHERE OpenId = @OpenId AND IsActive = 1";
            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { OpenId = openId });
        }

        public async Task<int> CreateAsync(User user)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO Users (Username, PasswordHash, Email, IsActive, IsAdmin, Points, OpenId, UnionId, NickName, AvatarUrl, LoginType) 
                       VALUES (@Username, @PasswordHash, @Email, @IsActive, @IsAdmin, @Points, @OpenId, @UnionId, @NickName, @AvatarUrl, @LoginType);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, new { 
                Username = user.Username, 
                PasswordHash = user.PasswordHash, 
                Email = user.Email, 
                IsActive = user.IsActive, 
                IsAdmin = user.IsAdmin,
                Points = user.Points,
                OpenId = user.OpenId,
                UnionId = user.UnionId,
                NickName = user.NickName,
                AvatarUrl = user.AvatarUrl,
                LoginType = user.LoginType
            });
        }
        
        public async Task UpdateAsync(User user)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE Users SET Username=@Username, PasswordHash=@PasswordHash, 
                       Email=@Email, IsActive=@IsActive, IsAdmin=@IsAdmin, NickName=@NickName, AvatarUrl=@AvatarUrl WHERE Id=@Id";
            await connection.ExecuteAsync(sql, user);
        }
        
        public async Task<bool> UpdateUserTokenAsync(int userId, string token)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "UPDATE Users SET CurrentToken = @Token WHERE Id = @UserId";
            var result = await connection.ExecuteAsync(sql, new { Token = token, UserId = userId });
            return result > 0;
        }
        
        public async Task<bool> IsTokenValidAsync(int userId, string token)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT COUNT(1) FROM Users WHERE Id = @UserId AND CurrentToken = @Token";
            var count = await connection.QuerySingleAsync<int>(sql, new { UserId = userId, Token = token });
            return count > 0;
        }
        
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM Users";
            return await connection.QueryAsync<User>(sql);
        }
    }
}