namespace GoogleAI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string? CurrentToken { get; set; } // 用于单点登录
        public bool IsAdmin { get; set; } // 管理员标志
        public int Points { get; set; } // 用户积分
        public string? OpenId { get; set; } // 微信OpenId
        public string? UnionId { get; set; } // 微信UnionId
        public string? NickName { get; set; } // 微信昵称
        public string? AvatarUrl { get; set; } // 微信头像
        public string? LoginType { get; set; } // 登录类型: Email, WeChat
    }

    public class ModelConfiguration
    {
        public int Id { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int MaxTokens { get; set; }
        public decimal Temperature { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int PointCost { get; set; } // 使用该模型所需的积分
        public string ImageSize { get; set; }
        public bool IsMultimodalSupported { get; set; } // 是否支持多模态
        public bool SupportsStreaming { get; set; } // 是否支持流式输出
        public int ContextWindowSize { get; set; } // 上下文窗口大小
        public int MaxImageSize { get; set; } // 最大图片尺寸（像素）
        public string SupportedImageFormats { get; set; } = "jpg,png,gif,webp"; // 支持的图片格式
    }

    public class DrawingTask
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ModelId { get; set; }
        public string TaskMode { get; set; } = string.Empty;
        public string? Prompt { get; set; }
        public string TaskStatus { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
        public string? ResultImageUrl { get; set; }
        public string? ResultImageUrls { get; set; } // ✅ 新增：JSON数组，存储多张图片URL
        public string? ThumbnailUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ReferenceImages { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? AspectRatio { get; set; }
        public int Progress { get; set; }
        public string? ProgressMessage { get; set; }

        // ✅ 新增字段
        public bool IsR2Uploaded { get; set; }
        public int UrlVersion { get; set; }
        public string? OriginalImageUrl { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        // ✅ 拆分重绘相关字段
        public int? ParentTaskId { get; set; } // 父任务ID（如果是拆分任务）
        public int? SplitIndex { get; set; } // 分块索引（在父任务中的位置）
        public string? BatchGroupId { get; set; } // 批次组ID（用于批量任务）

        // ✅ 仅拆分模式相关字段
        public string? ProcessMode { get; set; } // "split-only" 或 "split-upscale"
        public double? Tolerance { get; set; } // 拆分容差值
    }

    public class DrawingHistory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TaskId { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string? Prompt { get; set; }
        public string? ImageUrl { get; set; }
        public string? ImageUrls { get; set; } // ✅ 新增：JSON数组，存储多张图片URL
        public string? ThumbnailUrl { get; set; }
        public string? TaskMode { get; set; }
        public DateTime CreatedAt { get; set; }

        // ✅ 新增字段
        public bool IsR2Uploaded { get; set; }
        public int UrlVersion { get; set; }
        public string? OriginalImageUrl { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class PointsHistory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? TaskId { get; set; } // 关联的任务ID，可为空
        public int Points { get; set; } // 积分变化数量（正数表示增加，负数表示减少）
        public string Description { get; set; } = string.Empty; // 积分变化说明
        public DateTime CreatedAt { get; set; }
    }

    public class EmailVerification
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string VerificationCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }

    // 积分套餐
    public class PointPackage
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Points { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // 支付订单
    public class PaymentOrder
    {
        public int Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int? PackageId { get; set; }
        public int Points { get; set; }
        public decimal Amount { get; set; }
        public string OrderStatus { get; set; } = "Pending"; // Pending, Paid, Failed, Cancelled, Refunded
        public string? PaymentType { get; set; }
        public string? TransactionId { get; set; }
        public string? PrepayId { get; set; }
        public DateTime? NotifyTime { get; set; }
        public DateTime? PaidTime { get; set; }
        public string? ErrorMsg { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // 支付订单状态枚举
    public enum PaymentOrderStatus
    {
        Pending = 0,    // 待支付
        Paid = 1,       // 已支付
        Failed = 2,     // 支付失败
        Cancelled = 3,  // 已取消
        Refunded = 4    // 已退款
    }

    // 用户签到记录
    public class UserCheckIn
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CheckInDate { get; set; }
        public int Points { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ========== Chat 对话功能相关模型 ==========

    // Chat 会话模型
    public class Chat
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ModelId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    // ChatMessage 消息模型
    public class ChatMessage
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Content { get; set; } = string.Empty;
        public string ImageUrls { get; set; } = "[]"; // JSON array
        public int TokenCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ChatContext 上下文模型
    public class ChatContext
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public string ContextType { get; set; } = string.Empty; // "system", "user_preference", "summary"
        public string Content { get; set; } = string.Empty;
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // ChatImage 图片模型
    public class ChatImage
    {
        public int Id { get; set; }
        public int ChatMessageId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StorageUrl { get; set; } = string.Empty;
        public int FileSize { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public string MimeType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public bool IsProcessed { get; set; }
    }
}
