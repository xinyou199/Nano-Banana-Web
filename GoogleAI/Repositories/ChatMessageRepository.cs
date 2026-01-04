using Dapper;
using GoogleAI.Models;
using MySqlConnector;
using System.Text.Json;

namespace GoogleAI.Repositories
{
    public interface IChatMessageRepository
    {
        Task<ChatMessage> AddMessageAsync(ChatMessage message);
        Task<ChatMessage> UpdateMessageAsync(ChatMessage message);
        Task<List<ChatMessage>> GetChatMessagesAsync(int chatId, int limit = 50);
        Task<List<ChatMessage>> GetContextMessagesAsync(int chatId, int contextWindowSize);
        Task<int> GetMessageCountAsync(int chatId);
    }

    public class ChatMessageRepository : IChatMessageRepository
    {
        private readonly string _connectionString;

        public ChatMessageRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ChatMessage> AddMessageAsync(ChatMessage message)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"INSERT INTO ChatMessage (ChatId, UserId, Role, Content, ImageUrls, TokenCount, CreatedAt) 
                           VALUES (@ChatId, @UserId, @Role, @Content, @ImageUrls, @TokenCount, NOW());
                           SELECT LAST_INSERT_ID();";

                var imageUrlsJson = JsonSerializer.Serialize(JsonSerializer.Deserialize<List<string>>(message.ImageUrls) ?? new());

                var id = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    ChatId = message.ChatId,
                    UserId = message.UserId,
                    Role = message.Role,
                    Content = message.Content,
                    ImageUrls = imageUrlsJson,
                    TokenCount = message.TokenCount
                });

                message.Id = id;
                message.CreatedAt = DateTime.Now;
                return message;
            }
        }

        public async Task<ChatMessage> UpdateMessageAsync(ChatMessage message)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"UPDATE ChatMessage 
                           SET Content = @Content, TokenCount = @TokenCount 
                           WHERE Id = @Id";

                await connection.ExecuteAsync(sql, new
                {
                    Id = message.Id,
                    Content = message.Content,
                    TokenCount = message.TokenCount
                });

                return message;
            }
        }

        public async Task<List<ChatMessage>> GetChatMessagesAsync(int chatId, int limit = 50)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"SELECT * FROM ChatMessage 
                           WHERE ChatId = @ChatId 
                           ORDER BY CreatedAt ASC 
                           LIMIT @Limit";

                var messages = await connection.QueryAsync<ChatMessage>(sql, new { ChatId = chatId, Limit = limit });
                return messages.ToList();
            }
        }

        public async Task<List<ChatMessage>> GetContextMessagesAsync(int chatId, int contextWindowSize)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"SELECT * FROM ChatMessage 
                           WHERE ChatId = @ChatId 
                           ORDER BY CreatedAt DESC 
                           LIMIT @Limit";

                var messages = await connection.QueryAsync<ChatMessage>(sql, new { ChatId = chatId, Limit = contextWindowSize * 2 });
                return messages.OrderBy(m => m.CreatedAt).ToList();
            }
        }

        public async Task<int> GetMessageCountAsync(int chatId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "SELECT COUNT(*) FROM ChatMessage WHERE ChatId = @ChatId";
                return await connection.ExecuteScalarAsync<int>(sql, new { ChatId = chatId });
            }
        }
    }
}
