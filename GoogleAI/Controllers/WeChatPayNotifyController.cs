using Microsoft.AspNetCore.Mvc;
using GoogleAI.Services;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WeChatPayNotifyController : ControllerBase
    {
        private readonly IWeChatPayService _weChatPayService;
        private readonly ILogger<WeChatPayNotifyController> _logger;

        public WeChatPayNotifyController(
            IWeChatPayService weChatPayService,
            ILogger<WeChatPayNotifyController> logger)
        {
            _weChatPayService = weChatPayService;
            _logger = logger;
        }

        [HttpPost("payment")]
        public async Task<IActionResult> PaymentNotify()
        {
            try
            {
                // 获取微信支付回调的HTTP头信息
                Request.Headers.TryGetValue("Wechatpay-Serial", out var serialNumber);
                Request.Headers.TryGetValue("Wechatpay-Signature", out var signature);
                Request.Headers.TryGetValue("Wechatpay-Timestamp", out var timestamp);
                Request.Headers.TryGetValue("Wechatpay-Nonce", out var nonce);

                // 读取请求体
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                _logger.LogInformation($"收到微信支付回调: {body}");

                // 验证签名
                var isValid = await _weChatPayService.VerifyNotifyAsync(
                    serialNumber,
                    signature,
                    timestamp,
                    nonce,
                    body
                );

                if (!isValid)
                {
                    _logger.LogWarning("微信支付回调签名验证失败");
                    return BadRequest(new { code = "FAIL", message = "签名验证失败" });
                }

                // 解析回调数据
                var notifyData = await _weChatPayService.ParseNotifyAsync(body);
                if (notifyData == null)
                {
                    _logger.LogWarning("微信支付回调数据解析失败");
                    return BadRequest(new { code = "FAIL", message = "数据解析失败" });
                }

                // 处理支付成功
                if (notifyData.TradeState == "SUCCESS")
                {
                    var success = await _weChatPayService.HandlePaymentSuccessAsync(
                        notifyData.OutTradeNo,
                        notifyData.TransactionId
                    );

                    if (!success)
                    {
                        _logger.LogWarning($"处理支付成功失败: 订单号={notifyData.OutTradeNo}");
                        return BadRequest(new { code = "FAIL", message = "处理支付失败" });
                    }

                    _logger.LogInformation($"支付成功处理完成: 订单号={notifyData.OutTradeNo}");
                }

                // 返回成功响应
                return Ok(new { code = "SUCCESS", message = "成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理微信支付回调时发生错误");
                return StatusCode(500, new { code = "FAIL", message = "服务器错误" });
            }
        }
    }
}
