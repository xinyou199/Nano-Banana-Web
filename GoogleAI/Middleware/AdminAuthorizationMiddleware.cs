using GoogleAI.Repositories;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

namespace GoogleAI.Middleware
{
    public class AdminAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;

        public AdminAuthorizationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
        {
            // 检查请求是否针对管理端点
            if (context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) &&
                !context.Request.Path.StartsWithSegments("/admin/login", StringComparison.OrdinalIgnoreCase))
            {
                // 检查用户是否已通过JWT认证
                if (context.User.Identity.IsAuthenticated)
                {
                    // 获取用户ID并检查是否为管理员
                    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        var user = await userRepository.GetByIdAsync(userId);
                        if (user == null || !user.IsAdmin)
                        {
                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsJsonAsync(new { success = false, message = "需要管理员权限" });
                            return;
                        }
                    }
                    else
                    {
                        // 无效的用户信息，重定向到登录页面
                        context.Response.Redirect("/Admin/Login");
                        return;
                    }
                }
                else
                {
                    // 对于页面请求，让AdminController的Index方法处理
                    // 对于API请求，返回401
                    if (context.Request.Path.StartsWithSegments("/admin/api", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { success = false, message = "未授权" });
                        return;
                    }
                    // 页面请求继续，由AdminController处理认证逻辑
                }
            }

            // 继续处理请求
            await _next(context);
        }
    }
}