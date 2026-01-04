using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoogleAI.Models;
using GoogleAI.Services;
using GoogleAI.Repositories;
using System.Security.Claims;
using Dapper;

namespace GoogleAI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IHistoryRepository _historyRepository;
        private readonly IPaymentOrderRepository _paymentOrderRepository;
        private readonly IPointsHistoryRepository _pointsHistoryRepository;
        private readonly ICheckInRepository _checkInRepository;
        private readonly IHistoryRepository _pointsRepository;
        private readonly IConfiguration _configuration;

        public UserProfileController(
            IUserRepository userRepository, 
            IHistoryRepository historyRepository,
            IPaymentOrderRepository paymentOrderRepository,
            IPointsHistoryRepository pointsHistoryRepository,
            ICheckInRepository checkInRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _historyRepository = historyRepository;
            _paymentOrderRepository = paymentOrderRepository;
            _pointsHistoryRepository = pointsHistoryRepository;
            _checkInRepository = checkInRepository;
            _pointsRepository = historyRepository;
            _configuration = configuration;
        }

        [HttpGet("info")]
        public async Task<IActionResult> GetUserInfo()
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            var user = await _userRepository.GetByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { success = false, message = "用户不存在" });
            }

            return Ok(new {
                success = true,
                data = new {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    createdAt = user.CreatedAt,
                    points = user.Points
                }
            });
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateUserInfo([FromBody] UpdateUserInfoRequest request)
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new { success = false, message = "用户不存在" });
            }

            // 更新邮箱（如果提供了）
            if (!string.IsNullOrEmpty(request.Email))
            {
                user.Email = request.Email;
            }

            await _userRepository.UpdateAsync(user);

            return Ok(new {
                success = true,
                message = "用户信息更新成功",
                data = new {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email
                }
            });
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.OldPassword) || string.IsNullOrEmpty(request.NewPassword))
            {
                return BadRequest(new { success = false, message = "旧密码和新密码不能为空" });
            }

            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new { success = false, message = "用户不存在" });
            }

            // 验证旧密码
            var authService = HttpContext.RequestServices.GetService<IAuthService>();
            if (!authService.VerifyPassword(request.OldPassword, user.PasswordHash))
            {
                return BadRequest(new { success = false, message = "旧密码不正确" });
            }

            // 更新密码
            user.PasswordHash = authService.HashPassword(request.NewPassword);
            await _userRepository.UpdateAsync(user);

            return Ok(new { success = true, message = "密码修改成功" });
        }

        [HttpGet("points-history")]
        public async Task<IActionResult> GetPointsHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            
            var pointsHistory = await _pointsHistoryRepository.GetByUserIdAsync(userId, page, pageSize);
            var total = await _pointsHistoryRepository.CountByUserIdAsync(userId);

            var historyData = pointsHistory.Select(h => new {
                id = h.Id,
                taskId = h.TaskId,
                points = h.Points,
                description = h.Description,
                createdAt = h.CreatedAt,
                type = h.Points > 0 ? "increase" : "decrease"
            }).ToList();

            return Ok(new {
                success = true,
                data = historyData,
                pagination = new {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                }
            });
        }

        [HttpGet("recharge-records")]
        public async Task<IActionResult> GetRechargeRecords([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            
            var orders = await _paymentOrderRepository.GetByUserIdWithPaginationAsync(userId, page, pageSize);
            var total = await _paymentOrderRepository.CountByUserIdAsync(userId);

            var rechargeRecords = orders.Select(o => new {
                id = o.Id,
                orderNo = o.OrderNo,
                points = o.Points,
                amount = o.Amount,
                orderStatus = o.OrderStatus,
                paymentType = o.PaymentType,
                transactionId = o.TransactionId,
                paidTime = o.PaidTime,
                createdAt = o.CreatedAt,
                statusText = GetOrderStatusText(o.OrderStatus)
            }).ToList();

            return Ok(new {
                success = true,
                data = rechargeRecords,
                pagination = new {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                }
            });
        }

        private string GetOrderStatusText(string orderStatus)
        {
            return orderStatus switch
            {
                "Pending" => "待支付",
                "Paid" => "已支付",
                "Failed" => "支付失败",
                "Cancelled" => "已取消",
                "Refunded" => "已退款",
                _ => "未知状态"
            };
        }

        [HttpGet("check-in-status")]
        public async Task<IActionResult> GetCheckInStatus()
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");

            var today = DateTime.Today;
            var todayCheckIn = await _checkInRepository.GetUserCheckInAsync(userId, today);
            var lastCheckIn = await _checkInRepository.GetLastCheckInAsync(userId);
            var consecutiveDays = await _checkInRepository.GetConsecutiveDaysAsync(userId);

            var dailyPoints = _configuration.GetValue<int>("CheckInSettings:DailyPoints", 10);

            return Ok(new {
                success = true,
                data = new {
                    canCheckIn = todayCheckIn == null,
                    hasCheckedIn = todayCheckIn != null,
                    checkInDate = todayCheckIn?.CheckInDate,
                    consecutiveDays = consecutiveDays,
                    dailyPoints = dailyPoints,
                    lastCheckInDate = lastCheckIn?.CheckInDate
                }
            });
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> CheckIn()
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "用户不存在" });
            }

            var today = DateTime.Today;
            var todayCheckIn = await _checkInRepository.GetUserCheckInAsync(userId, today);

            if (todayCheckIn != null)
            {
                return BadRequest(new { success = false, message = "今日已签到，请明天再来" });
            }

            var dailyPoints = _configuration.GetValue<int>("CheckInSettings:DailyPoints", 10);

            // 创建签到记录
            var checkIn = new Models.UserCheckIn
            {
                UserId = userId,
                CheckInDate = today,
                Points = dailyPoints,
                CreatedAt = DateTime.Now
            };

            await _checkInRepository.CreateAsync(checkIn);

            // 增加用户积分
            user.Points += dailyPoints;
            await _userRepository.UpdateAsync(user);

            // 记录积分变动
            var pointsHistory = new Models.PointsHistory
            {
                UserId = userId,
                TaskId = null,
                Points = dailyPoints,
                Description = "每日签到奖励",
                CreatedAt = DateTime.Now
            };

            // 保存积分历史记录
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new MySql.Data.MySqlClient.MySqlConnection(connectionString);
            var sql = @"INSERT INTO PointsHistory (UserId, TaskId, Points, Description, CreatedAt)
                       VALUES (@UserId, @TaskId, @Points, @Description, @CreatedAt)";
            await connection.ExecuteAsync(sql, pointsHistory);

            var consecutiveDays = await _checkInRepository.GetConsecutiveDaysAsync(userId);

            return Ok(new {
                success = true,
                message = $"签到成功！获得 {dailyPoints} 积分",
                data = new {
                    pointsEarned = dailyPoints,
                    currentPoints = user.Points,
                    consecutiveDays = consecutiveDays
                }
            });
        }
    }

    public class UpdateUserInfoRequest
    {
        public string? Email { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}