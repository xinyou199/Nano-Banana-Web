using System.Text;
using System.Text.Json;
using GoogleAI.Models;
using GoogleAI.Repositories;
using Microsoft.Extensions.Options;

namespace GoogleAI.Services
{
    public class WeChatMiniProgramService : IWeChatMiniProgramService
    {
        private readonly HttpClient _httpClient;
        private readonly IWeChatPayService _weChatPayService;
        private readonly WeChatMiniProgramSettings _settings;
        private readonly IAuthService _authService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<WeChatMiniProgramService> _logger;
        
        // 内存存储扫码会话（生产环境建议使用Redis）
        private static readonly Dictionary<string, QrScanSession> _sessions = new();
        private static readonly object _lock = new();

        public WeChatMiniProgramService(
            HttpClient httpClient,
            IWeChatPayService weChatPayService,
            IOptions<WeChatMiniProgramSettings> settings,
            IAuthService authService,
            IUserRepository userRepository,
            ILogger<WeChatMiniProgramService> logger)
        {
            _httpClient = httpClient;
            _weChatPayService = weChatPayService;
            _settings = settings.Value;
            _authService = authService;
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// 通过code获取微信用户信息
        /// </summary>
        public async Task<WeChatUserInfo> GetUserInfoAsync(string code)
        {
            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={_settings.AppId}&secret={_settings.AppSecret}&js_code={code}&grant_type=authorization_code";
            
            try
            {
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                
                if (result.TryGetProperty("errcode", out var errcode) && errcode.GetInt32() != 0)
                {
                    var errMsg = result.TryGetProperty("errmsg", out var errmsg) ? errmsg.GetString() : "Unknown error";
                    throw new InvalidOperationException($"微信API错误: {errMsg} (Code: {errcode.GetInt32()})");
                }
                
                return new WeChatUserInfo
                {
                    OpenId = result.TryGetProperty("openid", out var openid) ? openid.GetString() ?? "" : "",
                    SessionKey = result.TryGetProperty("session_key", out var sessionKey) ? sessionKey.GetString() ?? "" : "",
                    UnionId = result.TryGetProperty("unionid", out var unionId) ? unionId.GetString() ?? "" : ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取微信用户信息失败");
                throw;
            }
        }

        /// <summary>
        /// 解密微信加密数据
        /// </summary>
        public async Task<string> DecryptUserDataAsync(string encryptedData, string iv, string sessionKey)
        {
            try
            {
                var keyBytes = Convert.FromBase64String(sessionKey);
                var ivBytes = Convert.FromBase64String(iv);
                var encryptedBytes = Convert.FromBase64String(encryptedData);
                
                using var aes = System.Security.Cryptography.Aes.Create();
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                
                using var decryptor = aes.CreateDecryptor();
                var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解密微信数据失败");
                throw;
            }
        }

        /// <summary>
        /// 创建扫码会话
        /// </summary>
        public async Task<QrScanSession> CreateQrSessionAsync()
        {
            var session = new QrScanSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddMinutes(_settings.SessionTimeoutMinutes)
            };
            
            lock (_lock)
            {
                _sessions[session.SessionId] = session;
            }
            
            _logger.LogInformation($"创建扫码会话: {session.SessionId}");
            return session;
        }

        /// <summary>
        /// 获取扫码会话
        /// </summary>
        public Task<QrScanSession?> GetQrSessionAsync(string sessionId)
        {
            lock (_lock)
            {
                _sessions.TryGetValue(sessionId, out var session);
                return Task.FromResult(session);
            }
        }

        /// <summary>
        /// 更新扫码会话
        /// </summary>
        public Task<bool> UpdateQrSessionAsync(QrScanSession session)
        {
            lock (_lock)
            {
                _logger.LogInformation("更新会话, SessionId: {SessionId}, Status: {Status}, UserId: {UserId}, HasToken: {HasToken}",
                    session.SessionId, session.Status, session.UserId, !string.IsNullOrEmpty(session.Token));

                if (_sessions.ContainsKey(session.SessionId))
                {
                    _sessions[session.SessionId] = session;
                    _logger.LogInformation("会话更新成功");
                    return Task.FromResult(true);
                }

                _logger.LogWarning("会话不存在, SessionId: {SessionId}", session.SessionId);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 处理扫码请求
        /// </summary>
        public async Task<QrScanSession?> HandleScanAsync(string sessionId, WeChatScanLoginRequest request)
        {
            try
            {
                // 1. 获取会话
                var session = await GetQrSessionAsync(sessionId);
                if (session == null)
                {
                    throw new InvalidOperationException("会话不存在或已过期");
                }
                
                // 2. 验证会话状态
                if (session.Status != "pending" && session.Status != "scanned")
                {
                    throw new InvalidOperationException($"会话状态异常: {session.Status}");
                }
                
                // 3. 通过code获取微信用户信息
                var wechatUserInfo = await GetUserInfoAsync(request.Code);
                
                // 4. 更新会话状态
                session.Status = "scanned";
                session.ScannedAt = DateTime.UtcNow;
                session.OpenId = wechatUserInfo.OpenId;
                session.SessionKey = wechatUserInfo.SessionKey;
                session.NickName = request.NickName;
                session.AvatarUrl = request.AvatarUrl;
                session.EncryptedData = request.EncryptedData;
                session.Iv = request.Iv;
                
                await UpdateQrSessionAsync(session);
                
                _logger.LogInformation($"会话 {sessionId} 已扫描，OpenId: {wechatUserInfo.OpenId}");
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理扫码请求失败");
                throw;
            }
        }

        /// <summary>
        /// 处理授权
        /// </summary>
        public async Task<QrScanSession?> AuthorizeAsync(string sessionId)
        {
            try
            {
                // 1. 获取会话
                var session = await GetQrSessionAsync(sessionId);
                if (session == null)
                {
                    throw new InvalidOperationException("会话不存在或已过期");
                }
                
                // 2. 验证会话状态
                if (session.Status != "scanned")
                {
                    throw new InvalidOperationException($"会话状态异常: {session.Status}");
                }
                
                // 3. 解密用户数据（如果提供）
                if (!string.IsNullOrEmpty(session.EncryptedData) && !string.IsNullOrEmpty(session.Iv) && !string.IsNullOrEmpty(session.SessionKey))
                {
                    try
                    {
                        var decryptedData = await DecryptUserDataAsync(session.EncryptedData, session.Iv, session.SessionKey);
                        var userData = JsonSerializer.Deserialize<JsonElement>(decryptedData);
                        
                        if (userData.TryGetProperty("nickName", out var nickName))
                        {
                            session.NickName = nickName.GetString() ?? session.NickName;
                        }
                        if (userData.TryGetProperty("avatarUrl", out var avatarUrl))
                        {
                            session.AvatarUrl = avatarUrl.GetString() ?? session.AvatarUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "解密用户数据失败，使用默认数据");
                    }
                }
                
                // 4. 更新会话状态
                session.Status = "authorized";
                session.AuthorizedAt = DateTime.UtcNow;
                await UpdateQrSessionAsync(session);
                
                _logger.LogInformation($"会话 {sessionId} 已授权");
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理授权失败");
                throw;
            }
        }

        /// <summary>
        /// 登录或注册用户
        /// </summary>
        public async Task<LoginResponse> LoginOrRegisterAsync(QrScanSession session)
        {
            try
            {
                // 1. 检查OpenId是否已存在
                var user = await _userRepository.GetByOpenIdAsync(session.OpenId!);
                bool isNewUser = user == null;
                
                if (user == null)
                {
                    // 2. 创建新用户
                    var username = await GenerateUniqueUsernameAsync(session.NickName ?? "微信用户");
                    user = new User
                    {
                        Username = username,
                        PasswordHash = _authService.HashPassword(Guid.NewGuid().ToString("N")), // 随机密码
                        Email = null,
                        IsActive = true,
                        IsAdmin = false,
                        Points = 100, // 默认积分
                        CreatedAt = DateTime.Now,
                        OpenId = session.OpenId,
                        UnionId = session.UnionId,
                        NickName = session.NickName,
                        AvatarUrl = session.AvatarUrl,
                        LoginType = "WeChat"
                    };
                    
                    var userId = await _userRepository.CreateAsync(user);
                    user.Id = userId;
                    
                    _logger.LogInformation($"新用户注册成功: {username} (OpenId: {session.OpenId})");
                }
                else
                {
                    // 3. 更新现有用户信息
                    user.NickName = session.NickName;
                    user.AvatarUrl = session.AvatarUrl;
                    await _userRepository.UpdateAsync(user);
                    
                    _logger.LogInformation($"用户登录成功: {user.Username} (OpenId: {session.OpenId})");
                }
                
                // 4. 生成JWT令牌
                var token = _authService.GenerateJwtToken(user);
                
                // 5. 更新会话
                session.UserId = user.Id;
                session.Token = token;
                session.Status = "completed";
                await UpdateQrSessionAsync(session);
                
                // 6. 返回登录响应
                return new LoginResponse
                {
                    Success = true,
                    Message = isNewUser ? "注册成功" : "登录成功",
                    Token = token,
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        IsAdmin = user.IsAdmin
                    },
                    IsNewUser = isNewUser
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "登录或注册失败");
                return new LoginResponse
                {
                    Success = false,
                    Message = $"登录失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 生成唯一的用户名
        /// </summary>
        private async Task<string> GenerateUniqueUsernameAsync(string baseName)
        {
            var cleanName = System.Text.RegularExpressions.Regex.Replace(baseName ?? "", @"[^\u4e00-\u9fa5a-zA-Z0-9_]", "");
            if (string.IsNullOrEmpty(cleanName))
            {
                cleanName = "微信用户";
            }
            
            var username = cleanName;
            int suffix = 1;
            
            while (true)
            {
                var existing = await _userRepository.GetByUsernameAsync(username);
                if (existing == null)
                {
                    return username;
                }
                
                username = $"{cleanName}{suffix}";
                suffix++;
            }
        }

        /// <summary>
        /// 清理过期会话
        /// </summary>
        public async Task CleanupExpiredSessionsAsync()
        {
            var now = DateTime.UtcNow;
            var expiredSessions = new List<string>();
            
            lock (_lock)
            {
                foreach (var (sessionId, session) in _sessions)
                {
                    if (session.ExpiredAt != null && session.ExpiredAt < now)
                    {
                        if (session.Status == "pending" || session.Status == "scanned")
                        {
                            session.Status = "timeout";
                        }
                        expiredSessions.Add(sessionId);
                    }
                }
            }
            
            if (expiredSessions.Count > 0)
            {
                _logger.LogInformation($"清理了 {expiredSessions.Count} 个过期会话");
            }
            
            await Task.CompletedTask;
        }
    }
}
