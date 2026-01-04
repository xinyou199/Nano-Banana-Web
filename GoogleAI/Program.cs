using CloudflareR2.NET.Configuration;
using CMSTaskApp.Store;
using GoogleAI.Configuration;
using GoogleAI.Middleware;
using GoogleAI.Models;
using GoogleAI.Repositories;
using GoogleAI.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// 注册HttpClient
builder.Services.AddHttpClient();

// ========================================
// 1. 基础服务
// ========================================
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// ========================================
// 2. 配置绑定
// ========================================
builder.Services.Configure<TaskProcessorSettings>(
    builder.Configuration.GetSection("TaskProcessorSettings"));

builder.Services.Configure<CloudflareR2Options>(
    builder.Configuration.GetSection("CloudflareR2"));

builder.Services.Configure<WeChatMiniProgramSettings>(
    builder.Configuration.GetSection("WeChatMiniProgram"));

// ========================================
// 3. JWT 身份验证
// ========================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JwtBearer";
    options.DefaultChallengeScheme = "JwtBearer";
})
.AddJwtBearer("JwtBearer", options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]))
    };

    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnChallenge = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                return Task.CompletedTask;
            }

            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                context.HandleResponse();
                context.Response.Redirect("/Admin/Login");
                return Task.CompletedTask;
            }

            context.HandleResponse();
            // 修改为重定向到新的统一登录页面
            context.Response.Redirect("/Home/Index");
            return Task.CompletedTask;
        }
    };
});

// ========================================
// 4. 数据库连接和仓储层
// ========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddSingleton<IUserRepository>(new UserRepository(connectionString));
builder.Services.AddSingleton<IHistoryRepository>(new HistoryRepository(connectionString));
builder.Services.AddSingleton<IModelConfigurationRepository>(new ModelConfigurationRepository(connectionString));
builder.Services.AddSingleton<IPointsRepository>(new HistoryRepository(connectionString));
builder.Services.AddSingleton<IDrawingTaskRepository>(new DrawingTaskRepository(connectionString));
builder.Services.AddSingleton<IDrawingHistoryRepository>(new DrawingHistoryRepository(connectionString));
builder.Services.AddSingleton<IEmailVerificationRepository>(new EmailVerificationRepository(connectionString));
builder.Services.AddSingleton<IPointPackageRepository>(new PointPackageRepository(connectionString));
builder.Services.AddSingleton<IPaymentOrderRepository>(new PaymentOrderRepository(connectionString));
builder.Services.AddSingleton<IPointsHistoryRepository>(new PointsHistoryRepository(connectionString));
builder.Services.AddSingleton<ICheckInRepository>(new CheckInRepository(connectionString));
builder.Services.AddSingleton<IChatRepository>(new ChatRepository(connectionString));
builder.Services.AddSingleton<IChatMessageRepository>(new ChatMessageRepository(connectionString));
builder.Services.AddSingleton<IChatContextRepository>(new ChatContextRepository(connectionString));
builder.Services.AddSingleton<IChatImageRepository>(new ChatImageRepository(connectionString));

// ========================================
// 5. 业务服务
// ========================================
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IEmailService, SimpleEmailService>();
builder.Services.AddSingleton<IVerificationService, VerificationService>();
builder.Services.AddSingleton<IR2StorageService, R2StorageService>();
builder.Services.AddSingleton<IWeChatPayService, WeChatPayService>();
builder.Services.AddSingleton<IWeChatMiniProgramService, WeChatMiniProgramService>();
builder.Services.AddSingleton<IImageSplitService, ImageSplitService>();
builder.Services.AddSingleton<ITokenCountService, TokenCountService>();
builder.Services.AddSingleton<IContextManagementService, ContextManagementService>();

// 为 ChatService 注册 HttpClient
builder.Services.AddHttpClient<ChatService>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        MaxConnectionsPerServer = 10,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

builder.Services.AddSingleton<IChatService>(sp => sp.GetRequiredService<ChatService>());

// ========================================
// 6. 任务处理相关服务（关键部分）
// ========================================

// 6.1 注册任务队列服务（自动解析依赖）
builder.Services.AddSingleton<ITaskQueueService, TaskQueueService>();

// 6.2 注册图片处理队列
builder.Services.AddSingleton<IImageProcessingQueue, ImageProcessingQueue>();

// 6.3 配置 HttpClient（用于 TaskProcessorService）
builder.Services.AddHttpClient<TaskProcessorService>()
    .ConfigureHttpClient((sp, client) =>
    {
        var settings = sp.GetRequiredService<IOptions<TaskProcessorSettings>>().Value;
        client.Timeout = TimeSpan.FromMinutes(settings.TaskTimeoutMinutes);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        MaxConnectionsPerServer = 10,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

// 6.4 注册后台服务（按依赖顺序）
builder.Services.AddHostedService<TaskProcessorService>();      // 任务处理器
builder.Services.AddHostedService<ImageProcessingService>();    // 图片处理器
builder.Services.AddHostedService<StuckTaskCleanupService>();   // 清理服务

// ========================================
// 7. 构建应用
// ========================================
var app = builder.Build();

// 初始化 ClientUsage 静态类
ClientUsage.Initialize(app.Services);

// ========================================
// 8. 启动验证和日志
// ========================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var settings = app.Services.GetRequiredService<IOptions<TaskProcessorSettings>>().Value;
logger.LogInformation(
    "✅ 任务处理器配置已加载:\n" +
    $"  - 最大并发数: {settings.MaxConcurrentTasks}\n" +
    $"  - 队列容量: {settings.QueueCapacity}\n" +
    $"  - 任务超时: {settings.TaskTimeoutMinutes} 分钟\n" +
    $"  - 图片处理并发: {settings.ImageProcessingConcurrency}\n" +
    $"  - 卡住任务阈值: {settings.StuckTaskThresholdMinutes} 分钟");

// ========================================
// 9. 中间件管道配置
// ========================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 自定义中间件 - 暂时禁用 SingleSignOnMiddleware，JWT 认证已足够
// app.UseMiddleware<SingleSignOnMiddleware>();
app.UseMiddleware<AdminAuthorizationMiddleware>();

// ========================================
// 10. 路由配置
// ========================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{action=Index}/{id?}",
    defaults: new { controller = "Admin" });

app.Run();
