using Microsoft.AspNetCore.Mvc;
using GoogleAI.Models;
using GoogleAI.Services;
using GoogleAI.Repositories;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUserRepository _userRepository;
        private readonly IVerificationService _verificationService;

        public AuthController(IAuthService authService, IUserRepository userRepository, IVerificationService verificationService)
        {
            _authService = authService;
            _userRepository = userRepository;
            _verificationService = verificationService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "用户名和密码不能为空" });
            }

            var response = await _authService.AuthenticateAsync(request);
            
            if (!response.Success)
            {
                return Unauthorized(response);
            }

            return Ok(response);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { success = false, message = string.Join(", ", errors) });
            }

            // 验证邮箱验证码
            var isValidCode = await _verificationService.VerifyCodeAsync(request.Email, request.VerificationCode);
            if (!isValidCode)
            {
                return BadRequest(new { success = false, message = "验证码错误或已过期" });
            }

            var response = await _authService.RegisterAsync(request);
            
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("send-verification-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { success = false, message = string.Join(", ", errors) });
            }

            var response = await _verificationService.SendVerificationCodeAsync(request.Email);
            
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // 获取当前用户ID
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                // 清除用户的当前令牌
                await _userRepository.UpdateUserTokenAsync(userId, null);
            }
            
            return Ok(new { success = true, message = "登出成功" });
        }
    }
}