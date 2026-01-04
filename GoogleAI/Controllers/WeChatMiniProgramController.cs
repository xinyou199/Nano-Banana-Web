using Microsoft.AspNetCore.Mvc;
using GoogleAI.Models;
using GoogleAI.Services;
using GoogleAI.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WeChatMiniProgramController : ControllerBase
    {
        private readonly IWeChatMiniProgramService _weChatMiniProgramService;
        private readonly ILogger<WeChatMiniProgramController> _logger;
        private readonly IUserRepository _userRepository;

        public WeChatMiniProgramController(
            IWeChatMiniProgramService weChatMiniProgramService,
            ILogger<WeChatMiniProgramController> logger,
            IUserRepository userRepository)
        {
            _weChatMiniProgramService = weChatMiniProgramService;
            _logger = logger;
            _userRepository = userRepository;
        }

        /// <summary>
        /// 获取扫码会话
        /// </summary>
        [HttpPost("qr-session")]
        public async Task<IActionResult> GetQrSession()
        {
            try
            {
                var session = await _weChatMiniProgramService.CreateQrSessionAsync();
                
                return Ok(new GetQrSessionResponse
                {
                    Success = true,
                    Message = "获取扫码会话成功",
                    SessionId = session.SessionId,
                    QrCodeUrl = $"/api/wechatminiprogram/qr-code/{session.SessionId}",
                    AppId = "wxXXXXXXXXXXXXXXXX" // 替换为实际的小程序AppId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取扫码会话失败");
                return BadRequest(new GetQrSessionResponse
                {
                    Success = false,
                    Message = $"获取扫码会话失败: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// 获取二维码图片
        /// </summary>
        [HttpGet("qr-code/{sessionId}")]
        public async Task<IActionResult> GetQrCode(string sessionId)
        {
            try
            {
                // 获取当前网站host
                var host = Request.Headers["Host"].FirstOrDefault() ?? "localhost";
                
                // 构建请求URL  此处调用统一微信小程序二维码获取API 可根据自己实际情况调整
                var apiUrl = $"https://xxx.ccc.com/api/auth/getQRCode?sceneStr={sessionId}|https://{host}/api/wechatminiprogram/authorize";
                
                // 使用HttpClient请求API
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                
                // 解析返回值
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResult = System.Text.Json.JsonDocument.Parse(responseContent);
                
                if (apiResult.RootElement.GetProperty("code").GetInt32() != 200)
                {
                    _logger.LogError("获取二维码API返回错误: {Message}", apiResult.RootElement.GetProperty("message").GetString());
                    return BadRequest(new { success = false, message = "获取二维码失败" });
                }
                
                var qrCodeImageUrl = apiResult.RootElement.GetProperty("data").GetString();
                
                // 下载二维码图片
                var imageResponse = await httpClient.GetAsync(qrCodeImageUrl);
                imageResponse.EnsureSuccessStatusCode();
                
                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                
                return File(imageBytes, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成二维码失败");
                return BadRequest(new { success = false, message = "生成二维码失败" });
            }
        }

        /// <summary>
        /// 轮询登录状态
        /// </summary>
        [HttpGet("login-status/{sessionId}")]
        public async Task<IActionResult> GetLoginStatus(string sessionId)
        {
            try
            {
                _logger.LogInformation("查询登录状态, SessionId: {SessionId}", sessionId);
                var session = await _weChatMiniProgramService.GetQrSessionAsync(sessionId);

                if (session == null)
                {
                    return Ok(new
                    {
                        success = false,  // 注意：小写
                        message = "会话不存在或已过期",
                        status = "timeout"
                    });
                }

                // 检查过期
                if (session.ExpiredAt != null && session.ExpiredAt < DateTime.UtcNow)
                {
                    if (session.Status == "pending" || session.Status == "scanned")
                    {
                        session.Status = "timeout";
                        await _weChatMiniProgramService.UpdateQrSessionAsync(session);
                    }
                }

                var response = new
                {
                    success = true,  // 小写
                    message = "获取登录状态成功",
                    status = session.Status,  // 小写
                    token = session.Token,     // 小写
                    user = (object?)null,
                    isRegistered = false
                };

                if (session.Status == "completed" && session.UserId.HasValue)
                {
                    var user = await _userRepository.GetByIdAsync(session.UserId.Value);
                    if (user != null)
                    {
                        var completedResponse = new
                        {
                            success = true,
                            message = "获取登录状态成功",
                            status = session.Status,
                            token = session.Token,
                            user = new
                            {
                                id = user.Id,
                                username = user.Username,
                                email = user.Email,
                                isAdmin = user.IsAdmin
                            },
                            isRegistered = true
                        };

                        _logger.LogInformation("返回登录状态: Status={Status}, HasToken={HasToken}, UserId={UserId}",
                            session.Status, !string.IsNullOrEmpty(session.Token), session.UserId.Value);

                        return Ok(completedResponse);
                    }
                }

                _logger.LogInformation("返回登录状态: Status={Status}, HasToken={HasToken}",
                    session.Status, !string.IsNullOrEmpty(session.Token));

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取登录状态失败");
                return BadRequest(new
                {
                    success = false,
                    message = $"获取登录状态失败: {ex.Message}"
                });
            }
        }


        /// <summary>
        /// 微信小程序授权（小程序端调用）
        /// </summary>
        [HttpPost("authorize")]
        public async Task<IActionResult> Authorize(WeChatScanLoginRequest request)
        {
            try
            {
                _logger.LogInformation("收到微信小程序回调, SessionId: {SessionId}", request.SessionId);
                Console.WriteLine($"收到微信小程序回调, SessionId: {request.SessionId}");

                if (string.IsNullOrEmpty(request.SessionId))
                {
                    return BadRequest(new { success = false, message = "缺少会话ID" });
                }

                // 更新扫码信息
                var scanResult = await _weChatMiniProgramService.HandleScanAsync(request.SessionId, request);
                _logger.LogInformation("扫码处理完成, SessionId: {SessionId}, Status: {Status}", request.SessionId, scanResult?.Status);

                // 处理授权
                var session = await _weChatMiniProgramService.AuthorizeAsync(request.SessionId);
                _logger.LogInformation("授权处理完成, SessionId: {SessionId}, Status: {Status}", request.SessionId, session?.Status);

                if (session == null)
                {
                    return BadRequest(new { success = false, message = "授权失败" });
                }

                // 自动登录或注册
                var loginResponse = await _weChatMiniProgramService.LoginOrRegisterAsync(session);
                _logger.LogInformation("登录/注册完成, SessionId: {SessionId}, Status: {Status}, UserId: {UserId}", request.SessionId, session?.Status, session?.UserId);

                if (!loginResponse.Success)
                {
                    return BadRequest(loginResponse);
                }

                return Ok(loginResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "授权失败");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 取消授权（小程序端调用）
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelAuthRequest request)
        {
            try
            {
                var session = await _weChatMiniProgramService.GetQrSessionAsync(request.SessionId);
                
                if (session == null)
                {
                    return BadRequest(new { success = false, message = "会话不存在" });
                }

                session.Status = "cancelled";
                await _weChatMiniProgramService.UpdateQrSessionAsync(session);

                return Ok(new { success = true, message = "已取消授权" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消授权失败");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class CancelAuthRequest
    {
        public string SessionId { get; set; } = string.Empty;
    }
}
