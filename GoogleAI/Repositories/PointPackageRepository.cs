using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IPointPackageRepository
    {
        Task<PointPackage?> GetByIdAsync(int id);
        Task<IEnumerable<PointPackage>> GetAllAsync();
        Task<IEnumerable<PointPackage>> GetActivePackagesAsync();
        Task<int> CreateAsync(PointPackage package);
        Task UpdateAsync(PointPackage package);
        Task<bool> DeleteAsync(int id);
    }

    public class PointPackageRepository : IPointPackageRepository
    {
        private readonly string _connectionString;

        public PointPackageRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<PointPackage?> GetByIdAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PointPackages WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<PointPackage>(sql, new { Id = id });
        }

        public async Task<IEnumerable<PointPackage>> GetAllAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PointPackages ORDER BY SortOrder ASC, Id ASC";
            return await connection.QueryAsync<PointPackage>(sql);
        }

        public async Task<IEnumerable<PointPackage>> GetActivePackagesAsync()
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PointPackages WHERE IsActive = 1 ORDER BY SortOrder ASC, Id ASC";
            return await connection.QueryAsync<PointPackage>(sql);
        }

        public async Task<int> CreateAsync(PointPackage package)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO PointPackages (Name, Points, Price, Description, IsActive, SortOrder) 
                       VALUES (@Name, @Points, @Price, @Description, @IsActive, @SortOrder);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                package.Name,
                package.Points,
                package.Price,
                package.Description,
                package.IsActive,
                package.SortOrder
            });
        }

        public async Task UpdateAsync(PointPackage package)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE PointPackages SET Name=@Name, Points=@Points, Price=@Price, 
                       Description=@Description, IsActive=@IsActive, SortOrder=@SortOrder 
                       WHERE Id=@Id";
            await connection.ExecuteAsync(sql, package);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "DELETE FROM PointPackages WHERE Id = @Id";
            var result = await connection.ExecuteAsync(sql, new { Id = id });
            return result > 0;
        }
    }
}
