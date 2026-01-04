namespace GoogleAI.Models
{
    // 微信小程序扫码登录请求
    public class WeChatScanLoginRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NickName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string EncryptedData { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
    }

    // 微信登录状态响应
    public class WeChatLoginStatusResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Status { get; set; } // 'scanned', 'authorized', 'cancelled', 'timeout'
        public string? Token { get; set; }
        public UserInfo? User { get; set; }
        public bool IsRegistered { get; set; }
    }

    // 获取扫码会话响应
    public class GetQrSessionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? QrCodeUrl { get; set; }
        public string? AppId { get; set; }
    }

    // 微信用户信息
    public class WeChatUserInfo
    {
        public string OpenId { get; set; } = string.Empty;
        public string UnionId { get; set; } = string.Empty;
        public string SessionKey { get; set; } = string.Empty;
        public string? NickName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Gender { get; set; }
        public string? Country { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Language { get; set; }
    }

    // 扫码会话
    public class QrScanSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending, scanned, authorized, cancelled, timeout, completed
        public DateTime CreatedAt { get; set; }
        public DateTime? ScannedAt { get; set; }
        public DateTime? AuthorizedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public string? OpenId { get; set; }
        public string? SessionKey { get; set; }
        public string? NickName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? EncryptedData { get; set; }
        public string? Iv { get; set; }
        public int? UserId { get; set; }
        public string? Token { get; set; }
        public string UnionId { get; internal set; }
    }

    // 微信小程序配置
    public class WeChatMiniProgramSettings
    {
        public string AppId { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
        public int SessionTimeoutMinutes { get; set; } = 5;
    }
}
