using Microsoft.AspNetCore.Mvc;
using GoogleAI.Services;

namespace GoogleAI.Controllers
{
    /// <summary>
    /// 微信支付测试控制器
    /// 用于测试和调试微信支付配置
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class WeChatPayTestController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public WeChatPayTestController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// 测试签名功能
        /// </summary>
        [HttpGet("test-signature")]
        public IActionResult TestSignature()
        {
            var privateKeyPath = _configuration["WeChatPay:PrivateKeyPath"];
            
            if (string.IsNullOrEmpty(privateKeyPath))
            {
                return BadRequest(new { success = false, message = "未配置私钥路径" });
            }

            var result = WeChatPayTestHelper.TestSignature(privateKeyPath);
            
            return Ok(new
            {
                success = result.IsSuccess,
                message = result.IsSuccess ? "签名测试成功" : result.ErrorMessage,
                data = new
                {
                    privateKeyExists = result.PrivateKeyExists,
                    privateKeyLength = result.PrivateKeyLength,
                    privateKeyFormatValid = result.PrivateKeyFormatValid,
                    privateKeyLoadSuccess = result.PrivateKeyLoadSuccess,
                    signatureGenerated = result.SignatureGenerated,
                    signatureVerified = result.SignatureVerified,
                    signatureLength = result.SignatureLength,
                    signature = result.Signature?.Substring(0, Math.Min(100, result.Signature.Length)) + "..."
                }
            });
        }

        /// <summary>
        /// 获取当前配置信息（脱敏）
        /// </summary>
        [HttpGet("config-info")]
        public IActionResult GetConfigInfo()
        {
            var appId = _configuration["WeChatPay:AppId"];
            var merchantId = _configuration["WeChatPay:MerchantId"];
            var apiV3Key = _configuration["WeChatPay:ApiV3Key"];
            var serialNo = _configuration["WeChatPay:SerialNo"];
            var privateKeyPath = _configuration["WeChatPay:PrivateKeyPath"];
            var notifyUrl = _configuration["WeChatPay:NotifyUrl"];

            return Ok(new
            {
                success = true,
                data = new
                {
                    appId = appId,
                    merchantId = merchantId,
                    apiV3Key = MaskString(apiV3Key, 4, 4),
                    serialNo = serialNo,
                    serialNoLength = serialNo?.Length,
                    privateKeyPath = privateKeyPath,
                    privateKeyExists = System.IO.File.Exists(privateKeyPath),
                    notifyUrl = notifyUrl
                }
            });
        }

        /// <summary>
        /// 脱敏字符串
        /// </summary>
        private string MaskString(string? input, int keepStart, int keepEnd)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }

            if (input.Length <= keepStart + keepEnd)
            {
                return input;
            }

            var start = input.Substring(0, keepStart);
            var end = input.Substring(input.Length - keepEnd);
            var masked = new string('*', Math.Max(0, input.Length - keepStart - keepEnd));

            return $"{start}{masked}{end}";
        }
    }
}
