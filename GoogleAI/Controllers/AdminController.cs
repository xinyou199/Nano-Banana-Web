using GoogleAI.Models;
using GoogleAI.Repositories;
using GoogleAI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoogleAI.Controllers
{
    public class AdminController : Controller
    {
        private readonly IModelConfigurationRepository _modelRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuthService _authService;

        public AdminController(
            IModelConfigurationRepository modelRepository,
            IUserRepository userRepository,
            IAuthService authService)
        {
            _modelRepository = modelRepository;
            _userRepository = userRepository;
            _authService = authService;
        }

        public async Task<IActionResult> Index()
        {
            // 首先检查JWT认证
            if (User.Identity.IsAuthenticated)
            {
                Console.WriteLine($"用户已通过JWT认证: {User.Identity.Name}");
                var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    var user = await _userRepository.GetByIdAsync(userId);
                    if (user != null && user.IsAdmin)
                    {
                        Console.WriteLine($"用户 {user.Username} 是管理员，允许访问");
                        return View();
                    }
                    else
                    {
                        Console.WriteLine($"用户ID {userId} 不是管理员，重定向到登录页面");
                        // 用户已登录但不是管理员，重定向到登录页面
                        return RedirectToAction("Login", "Admin");
                    }
                }
            }
            else
            {
                Console.WriteLine("用户未通过JWT认证，页面将继续加载，由前端JavaScript处理认证");
            }

            // 如果没有JWT认证，检查是否从cookie获取token（可选）
            // 这里主要是为了让前端JavaScript能够处理认证
            // 页面会正常加载，然后前端的admin.js会检查localStorage中的token
            return View();
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            // 如果用户已经登录并且是管理员，重定向到管理主页
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    var user = _userRepository.GetByIdAsync(userId).Result;
                    if (user != null && user.IsAdmin)
                    {
                        return RedirectToAction("Index");
                    }
                }
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "用户名和密码不能为空" });
            }

            var response = await _authService.AuthenticateAsync(request);
            
            if (!response.Success)
            {
                return Unauthorized(new { success = false, message = response.Message });
            }

            // 检查用户是否为管理员
            if (!response.User.IsAdmin)
            {
                return Unauthorized(new { success = false, message = "需要管理员权限" });
            }

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // 清除用户的当前令牌
            var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                await _userRepository.UpdateUserTokenAsync(userId, null);
            }

            return Ok(new { success = true, message = "登出成功" });
        }
    }
}