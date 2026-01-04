using Dapper;
using GoogleAI.Models;
using MySqlConnector;

namespace GoogleAI.Repositories
{
    public interface IChatImageRepository
    {
        Task<ChatImage> AddImageAsync(ChatImage image);
        Task<List<ChatImage>> GetMessageImagesAsync(int messageId);
        Task<bool> DeleteImageAsync(int imageId);
        Task<bool> MarkAsProcessedAsync(int imageId);
    }

    public class ChatImageRepository : IChatImageRepository
    {
        private readonly string _connectionString;

        public ChatImageRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ChatImage> AddImageAsync(ChatImage image)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"INSERT INTO ChatImage (ChatMessageId, OriginalFileName, StorageUrl, FileSize, ImageWidth, ImageHeight, MimeType, UploadedAt, IsProcessed) 
                           VALUES (@ChatMessageId, @OriginalFileName, @StorageUrl, @FileSize, @ImageWidth, @ImageHeight, @MimeType, NOW(), @IsProcessed);
                           SELECT LAST_INSERT_ID();";

                var id = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    ChatMessageId = image.ChatMessageId,
                    OriginalFileName = image.OriginalFileName,
                    StorageUrl = image.StorageUrl,
                    FileSize = image.FileSize,
                    ImageWidth = image.ImageWidth,
                    ImageHeight = image.ImageHeight,
                    MimeType = image.MimeType,
                    IsProcessed = image.IsProcessed
                });

                image.Id = id;
                image.UploadedAt = DateTime.Now;
                return image;
            }
        }

        public async Task<List<ChatImage>> GetMessageImagesAsync(int messageId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = @"SELECT * FROM ChatImage 
                           WHERE ChatMessageId = @ChatMessageId 
                           ORDER BY UploadedAt ASC";

                var images = await connection.QueryAsync<ChatImage>(sql, new { ChatMessageId = messageId });
                return images.ToList();
            }
        }

        public async Task<bool> DeleteImageAsync(int imageId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "DELETE FROM ChatImage WHERE Id = @Id";
                var result = await connection.ExecuteAsync(sql, new { Id = imageId });
                return result > 0;
            }
        }

        public async Task<bool> MarkAsProcessedAsync(int imageId)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var sql = "UPDATE ChatImage SET IsProcessed = TRUE WHERE Id = @Id";
                var result = await connection.ExecuteAsync(sql, new { Id = imageId });
                return result > 0;
            }
        }
    }
}
