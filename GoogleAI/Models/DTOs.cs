namespace GoogleAI.Models
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public UserInfo? User { get; set; }
        public bool IsNewUser { get; set; } // 是否为新注册用户
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsAdmin { get; set; } // 管理员标志
    }

    public class CreateTaskRequest
    {
        public int ModelId { get; set; }
        public string TaskMode { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public List<string>? ReferenceImages { get; set; }
    }

    public class CreateTaskResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TaskId { get; set; }
    }

    public class TaskStatusResponse
    {
        public int TaskId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ResultImageUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // ========== Chat 对话功能相关DTO ==========

    // 创建对话请求
    public class CreateChatRequest
    {
        public int ModelId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    // 发送消息请求
    public class SendMessageRequest
    {
        public int ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public bool UseContext { get; set; } = true;
        public int ContextWindowSize { get; set; } = 5;
        public bool UseStreaming { get; set; } = true;
    }

    // 对话消息响应
    public class ChatMessageResponse
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    // 对话会话响应
    public class ChatResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ModelId { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ChatMessageResponse> Messages { get; set; } = new();
    }

    // 模型信息响应
    public class AIModelInfoResponse
    {
        public int Id { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsMultimodalSupported { get; set; }
        public bool SupportsStreaming { get; set; }
        public int ContextWindowSize { get; set; }
        public int MaxImageSize { get; set; }
        public List<string> SupportedImageFormats { get; set; } = new();
        public int PointCost { get; set; }
    }

    // 图片上传响应
    public class ImageUploadResponse
    {
        public string Url { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string MimeType { get; set; } = string.Empty;
    }

    // 流式消息事件
    public class StreamMessageEvent
    {
        public string Type { get; set; } = string.Empty; // "start", "content", "end", "error"
        public string Content { get; set; } = string.Empty;
        public int MessageId { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
