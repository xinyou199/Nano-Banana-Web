using Dapper;
using GoogleAI.Models;
using MySqlConnector;

namespace GoogleAI.Repositories
{
    public interface IChatRepository
    {
        Task<Chat> CreateChatAsync(int userId, int modelId, string title);
        Task<Chat> GetChatByIdAsync(int chatId);
        Task<List<Chat>> GetUserChatsAsync(int userId, int pageSize = 20, int pageNumber = 1);
        Task<bool> DeleteChatAsync(int chatId);
        Task<bool> UpdateChatTitleAsync(int chatId, string title);
    }

    public class ChatRepository : IChatRepository
    {
        private readonly string _connectionString;

        public ChatRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<Chat> CreateChatAsync(int userId, int modelId, string title)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"INSERT INTO Chat (UserId, ModelId, Title, CreatedAt, UpdatedAt) 
                           VALUES (@UserId, @ModelId, @Title, NOW(), NOW());
                           SELECT LAST_INSERT_ID();";

                var id = await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, ModelId = modelId, Title = title });

                return new Chat
                {
                    Id = id,
                    UserId = userId,
                    ModelId = modelId,
                    Title = title,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };
            }
        }

        public async Task<Chat> GetChatByIdAsync(int chatId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "SELECT * FROM Chat WHERE Id = @Id AND IsDeleted = FALSE";
                return await connection.QueryFirstOrDefaultAsync<Chat>(sql, new { Id = chatId });
            }
        }

        public async Task<List<Chat>> GetUserChatsAsync(int userId, int pageSize = 20, int pageNumber = 1)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"SELECT * FROM Chat 
                           WHERE UserId = @UserId AND IsDeleted = FALSE 
                           ORDER BY UpdatedAt DESC 
                           LIMIT @Offset, @PageSize";

                var chats = await connection.QueryAsync<Chat>(sql, new
                {
                    UserId = userId,
                    Offset = (pageNumber - 1) * pageSize,
                    PageSize = pageSize
                });

                return chats.ToList();
            }
        }

        public async Task<bool> DeleteChatAsync(int chatId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "UPDATE Chat SET IsDeleted = TRUE WHERE Id = @Id";
                var result = await connection.ExecuteAsync(sql, new { Id = chatId });
                return result > 0;
            }
        }

        public async Task<bool> UpdateChatTitleAsync(int chatId, string title)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "UPDATE Chat SET Title = @Title, UpdatedAt = NOW() WHERE Id = @Id";
                var result = await connection.ExecuteAsync(sql, new { Id = chatId, Title = title });
                return result > 0;
            }
        }
    }
}
