using Dapper;
using GoogleAI.Models;
using MySql.Data.MySqlClient;

namespace GoogleAI.Repositories
{
    public interface IPaymentOrderRepository
    {
        Task<PaymentOrder?> GetByIdAsync(int id);
        Task<PaymentOrder?> GetByOrderNoAsync(string orderNo);
        Task<PaymentOrder?> GetByPrepayIdAsync(string prepayId);
        Task<IEnumerable<PaymentOrder>> GetByUserIdAsync(int userId);
        Task<IEnumerable<PaymentOrder>> GetByUserIdWithPaginationAsync(int userId, int page, int pageSize);
        Task<int> CreateAsync(PaymentOrder order);
        Task UpdateAsync(PaymentOrder order);
        Task<bool> UpdateOrderStatusAsync(string orderNo, string status, string? transactionId = null, string? errorMsg = null);
        Task<int> CountByUserIdAsync(int userId);
    }

    public class PaymentOrderRepository : IPaymentOrderRepository
    {
        private readonly string _connectionString;

        public PaymentOrderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<PaymentOrder?> GetByIdAsync(int id)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PaymentOrders WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<PaymentOrder>(sql, new { Id = id });
        }

        public async Task<PaymentOrder?> GetByOrderNoAsync(string orderNo)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PaymentOrders WHERE OrderNo = @OrderNo";
            return await connection.QueryFirstOrDefaultAsync<PaymentOrder>(sql, new { OrderNo = orderNo });
        }

        public async Task<PaymentOrder?> GetByPrepayIdAsync(string prepayId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PaymentOrders WHERE PrepayId = @PrepayId";
            return await connection.QueryFirstOrDefaultAsync<PaymentOrder>(sql, new { PrepayId = prepayId });
        }

        public async Task<IEnumerable<PaymentOrder>> GetByUserIdAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT * FROM PaymentOrders WHERE UserId = @UserId ORDER BY CreatedAt DESC";
            return await connection.QueryAsync<PaymentOrder>(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<PaymentOrder>> GetByUserIdWithPaginationAsync(int userId, int page, int pageSize)
        {
            using var connection = new MySqlConnection(_connectionString);
            var offset = (page - 1) * pageSize;
            var sql = @"SELECT * FROM PaymentOrders WHERE UserId = @UserId 
                       ORDER BY CreatedAt DESC LIMIT @Limit OFFSET @Offset";
            return await connection.QueryAsync<PaymentOrder>(sql, new { UserId = userId, Limit = pageSize, Offset = offset });
        }

        public async Task<int> CreateAsync(PaymentOrder order)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"INSERT INTO PaymentOrders (OrderNo, UserId, PackageId, Points, Amount, OrderStatus, PaymentType) 
                       VALUES (@OrderNo, @UserId, @PackageId, @Points, @Amount, @OrderStatus, @PaymentType);
                       SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, order);
        }

        public async Task UpdateAsync(PaymentOrder order)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE PaymentOrders SET OrderStatus=@OrderStatus, PaymentType=@PaymentType, 
                       TransactionId=@TransactionId, PrepayId=@PrepayId, NotifyTime=@NotifyTime, 
                       PaidTime=@PaidTime, ErrorMsg=@ErrorMsg WHERE Id=@Id";
            await connection.ExecuteAsync(sql, order);
        }

        public async Task<bool> UpdateOrderStatusAsync(string orderNo, string status, string? transactionId = null, string? errorMsg = null)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = @"UPDATE PaymentOrders SET OrderStatus = @OrderStatus, TransactionId = @TransactionId, 
                       ErrorMsg = @ErrorMsg, UpdatedAt = NOW()";
            
            if (status == "Paid")
            {
                sql += ", PaidTime = NOW(), NotifyTime = NOW()";
            }
            
            sql += " WHERE OrderNo = @OrderNo";
            
            var result = await connection.ExecuteAsync(sql, new
            {
                OrderStatus = status,
                TransactionId = transactionId,
                ErrorMsg = errorMsg,
                OrderNo = orderNo
            });
            return result > 0;
        }

        public async Task<int> CountByUserIdAsync(int userId)
        {
            using var connection = new MySqlConnection(_connectionString);
            var sql = "SELECT COUNT(*) FROM PaymentOrders WHERE UserId = @UserId";
            return await connection.QuerySingleAsync<int>(sql, new { UserId = userId });
        }
    }
}
