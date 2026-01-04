using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GoogleAI.Models;
using GoogleAI.Services;
using GoogleAI.Repositories;
using System.Security.Claims;

namespace GoogleAI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PointPurchaseController : ControllerBase
    {
        private readonly IWeChatPayService _weChatPayService;
        private readonly IPointPackageRepository _pointPackageRepository;
        private readonly IPaymentOrderRepository _paymentOrderRepository;

        public PointPurchaseController(
            IWeChatPayService weChatPayService,
            IPointPackageRepository pointPackageRepository,
            IPaymentOrderRepository paymentOrderRepository)
        {
            _weChatPayService = weChatPayService;
            _pointPackageRepository = pointPackageRepository;
            _paymentOrderRepository = paymentOrderRepository;
        }

        // 获取所有可用套餐
        [HttpGet("packages")]
        public async Task<IActionResult> GetPackages()
        {
            var packages = await _pointPackageRepository.GetActivePackagesAsync();
            return Ok(new
            {
                success = true,
                data = packages.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    points = p.Points,
                    price = p.Price,
                    description = p.Description
                })
            });
        }

        // 创建支付订单
        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                    ?? User.FindFirst(ClaimTypes.NameIdentifier);
                var userId = int.Parse(userIdClaim?.Value ?? "0");
                
                if (userId == 0)
                {
                    return Unauthorized(new { success = false, message = "用户未登录" });
                }

                var orderResponse = await _weChatPayService.CreateOrderAsync(userId, request.PackageId);

                if (orderResponse == null)
                {
                    return BadRequest(new { success = false, message = "创建订单失败" });
                }

                return Ok(new
                {
                    success = true,
                    message = "订单创建成功",
                    data = new
                    {
                        prepayId = orderResponse.PrepayId,
                        codeUrl = orderResponse.CodeUrl,
                        package = orderResponse.Package,
                        nonceStr = orderResponse.NonceStr,
                        timeStamp = orderResponse.TimeStamp,
                        signType = orderResponse.SignType
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // 获取用户的支付订单列表
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            
            var orders = await _paymentOrderRepository.GetByUserIdWithPaginationAsync(userId, page, pageSize);
            var total = await _paymentOrderRepository.CountByUserIdAsync(userId);

            return Ok(new
            {
                success = true,
                data = orders.Select(o => new
                {
                    id = o.Id,
                    orderNo = o.OrderNo,
                    packageId = o.PackageId,
                    points = o.Points,
                    amount = o.Amount,
                    orderStatus = o.OrderStatus,
                    paymentType = o.PaymentType,
                    transactionId = o.TransactionId,
                    paidTime = o.PaidTime,
                    createdAt = o.CreatedAt
                }),
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                }
            });
        }

        // 查询订单状态
        [HttpGet("order/{orderNo}")]
        public async Task<IActionResult> GetOrderStatus(string orderNo)
        {
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") 
                ?? User.FindFirst(ClaimTypes.NameIdentifier);
            var userId = int.Parse(userIdClaim?.Value ?? "0");
            var order = await _paymentOrderRepository.GetByOrderNoAsync(orderNo);

            if (order == null || order.UserId != userId)
            {
                return NotFound(new { success = false, message = "订单不存在" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = order.Id,
                    orderNo = order.OrderNo,
                    packageId = order.PackageId,
                    points = order.Points,
                    amount = order.Amount,
                    orderStatus = order.OrderStatus,
                    paymentType = order.PaymentType,
                    transactionId = order.TransactionId,
                    paidTime = order.PaidTime,
                    createdAt = order.CreatedAt
                }
            });
        }
    }

    public class CreateOrderRequest
    {
        public int PackageId { get; set; }
    }
}
