using Microsoft.AspNetCore.Mvc;
using GoogleAI.Services;

namespace GoogleAI.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAuthService _authService;

        public HomeController(IAuthService authService)
        {
            _authService = authService;
        }

        private bool IsAuthenticated()
        {
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
            {
                Request.Cookies.TryGetValue("token", out string cookieToken);
                token = cookieToken;
            }

            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                return _authService.ValidateToken(token);
            }
            catch
            {
                return false;
            }
        }

        public IActionResult Index()
        {           
            // 如果已经登录，直接跳转到绘图页面
            if (IsAuthenticated())
            {
                return RedirectToAction(nameof(Drawing));
            }
            // 新的统一登录页面将在Index.cshtml中提供两种登录方式
            return View();
        }

        public IActionResult WeChatLogin()
        {
            // 如果已经登录，直接跳转到绘图页面
            if (IsAuthenticated())
            {
                return RedirectToAction(nameof(Drawing));
            }

            // 微信扫码登录页面
            return View();
        }

        public IActionResult Drawing()
        {
            // 绘图页面 - 添加服务端认证
            if (!IsAuthenticated())
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        public IActionResult History()
        {
            // 历史记录页面 - 添加服务端认证
            if (!IsAuthenticated())
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        public IActionResult Points()
        {
            // 积分充值页面 - 添加服务端认证
            if (!IsAuthenticated())
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        public IActionResult Profile()
        {
            // 用户信息页面 - 添加服务端认证
            if (!IsAuthenticated())
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        public IActionResult Chat()
        {
            // AI对话页面 - 添加服务端认证
            if (!IsAuthenticated())
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }
    }
}
