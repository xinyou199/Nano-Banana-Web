using Dapper;
using GoogleAI.Models;
using MySqlConnector;

namespace GoogleAI.Repositories
{
    public interface IChatContextRepository
    {
        Task<ChatContext> AddContextAsync(ChatContext context);
        Task<List<ChatContext>> GetContextsAsync(int chatId);
        Task<bool> UpdateContextAsync(ChatContext context);
        Task<bool> DeleteContextAsync(int contextId);
    }

    public class ChatContextRepository : IChatContextRepository
    {
        private readonly string _connectionString;

        public ChatContextRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ChatContext> AddContextAsync(ChatContext context)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"INSERT INTO ChatContext (ChatId, ContextType, Content, Priority, CreatedAt, UpdatedAt) 
                           VALUES (@ChatId, @ContextType, @Content, @Priority, NOW(), NOW());
                           SELECT LAST_INSERT_ID();";

                var id = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    ChatId = context.ChatId,
                    ContextType = context.ContextType,
                    Content = context.Content,
                    Priority = context.Priority
                });

                context.Id = id;
                context.CreatedAt = DateTime.Now;
                context.UpdatedAt = DateTime.Now;
                return context;
            }
        }

        public async Task<List<ChatContext>> GetContextsAsync(int chatId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"SELECT * FROM ChatContext 
                           WHERE ChatId = @ChatId 
                           ORDER BY Priority DESC, CreatedAt DESC";

                var contexts = await connection.QueryAsync<ChatContext>(sql, new { ChatId = chatId });
                return contexts.ToList();
            }
        }

        public async Task<bool> UpdateContextAsync(ChatContext context)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"UPDATE ChatContext 
                           SET Content = @Content, Priority = @Priority, UpdatedAt = NOW() 
                           WHERE Id = @Id";

                var result = await connection.ExecuteAsync(sql, new
                {
                    Id = context.Id,
                    Content = context.Content,
                    Priority = context.Priority
                });

                return result > 0;
            }
        }

        public async Task<bool> DeleteContextAsync(int contextId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "DELETE FROM ChatContext WHERE Id = @Id";
                var result = await connection.ExecuteAsync(sql, new { Id = contextId });
                return result > 0;
            }
        }
    }
}
