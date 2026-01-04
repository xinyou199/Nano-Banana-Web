using GoogleAI.Repositories;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;

namespace GoogleAI.Middleware
{
    public class SingleSignOnMiddleware
    {
        private readonly RequestDelegate _next;

        public SingleSignOnMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
        {
            // 检查请求是否需要身份验证
            if (context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/api/auth") &&
                !context.Request.Path.StartsWithSegments("/api/wechatminiprogram"))
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

                // 如果有用户标识和令牌，则验证令牌有效性
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) && !string.IsNullOrEmpty(token))
                {
                    var isTokenValid = await userRepository.IsTokenValidAsync(userId, token);
                    if (!isTokenValid)
                    {
                        // 令牌无效，返回401
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { success = false, message = "登录已过期，请重新登录" });
                        return;
                    }
                }
                else
                {
                    // 没有 Claims 或 token，但 JWT 认证中间件已经处理了认证
                    // 如果没有认证通过，JWT 中间件会自动返回 401
                    // 所以这里不需要额外处理
                }
            }

            // 继续处理请求
            await _next(context);
        }
    }
}