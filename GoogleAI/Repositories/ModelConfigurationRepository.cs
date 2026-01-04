using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IModelConfigurationRepository
    {
        Task<IEnumerable<ModelConfiguration>> GetAllActiveAsync();
        Task<IEnumerable<ModelConfiguration>> GetAllAsync(); // 添加获取所有模型的方法
        Task<ModelConfiguration?> GetByIdAsync(int id);
        Task<int> CreateAsync(ModelConfiguration model);
        Task<bool> UpdateAsync(ModelConfiguration model);
        Task<bool> DeleteAsync(int id);
    }

    public class ModelConfigurationRepository : IModelConfigurationRepository
    {
        private readonly string _connectionString;

        public ModelConfigurationRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<ModelConfiguration>> GetAllActiveAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM ModelConfigurations WHERE IsActive = 1 ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<ModelConfiguration>(sql);
        }

        public async Task<IEnumerable<ModelConfiguration>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM ModelConfigurations ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<ModelConfiguration>(sql);
        }

        public async Task<ModelConfiguration?> GetByIdAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM ModelConfigurations WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<ModelConfiguration>(sql, new { Id = id });
        }

        public async Task<int> CreateAsync(ModelConfiguration model)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO ModelConfigurations (ModelName, ApiUrl, ApiKey, IsActive, MaxTokens, Temperature, Description, PointCost) 
                       VALUES (@ModelName, @ApiUrl, @ApiKey, @IsActive, @MaxTokens, @Temperature, @Description, @PointCost);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, new { 
                ModelName = model.ModelName,
                ApiUrl = model.ApiUrl,
                ApiKey = model.ApiKey,
                IsActive = model.IsActive,
                MaxTokens = model.MaxTokens,
                Temperature = model.Temperature,
                Description = model.Description,
                ImageSize=model.ImageSize,
                PointCost = model.PointCost
            });
        }

        public async Task<bool> UpdateAsync(ModelConfiguration model)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE ModelConfigurations 
                       SET ModelName = @ModelName, ApiUrl = @ApiUrl, ApiKey = @ApiKey, 
                           IsActive = @IsActive, MaxTokens = @MaxTokens, Temperature = @Temperature, 
                           Description = @Description, PointCost = @PointCost, UpdatedAt = CURRENT_TIMESTAMP
                       WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, model);
            return result > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "UPDATE ModelConfigurations SET IsActive = 0 WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id });
            return result > 0;
        }
    }
}