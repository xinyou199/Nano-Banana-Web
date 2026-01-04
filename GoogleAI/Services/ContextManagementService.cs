using GoogleAI.Models;
using GoogleAI.Repositories;
using System.Text;

namespace GoogleAI.Services
{
    public interface IContextManagementService
    {
        Task<string> BuildContextPromptAsync(int chatId, int contextWindowSize);
        Task<bool> AddSystemContextAsync(int chatId, string context);
        Task<bool> UpdateUserPreferenceAsync(int chatId, string preference);
        Task<string> GenerateConversationSummaryAsync(int chatId);
    }

    public class ContextManagementService : IContextManagementService
    {
        private readonly IChatMessageRepository _messageRepository;
        private readonly IChatContextRepository _contextRepository;
        private readonly ITokenCountService _tokenService;

        public ContextManagementService(
            IChatMessageRepository messageRepository,
            IChatContextRepository contextRepository,
            ITokenCountService tokenService)
        {
            _messageRepository = messageRepository;
            _contextRepository = contextRepository;
            _tokenService = tokenService;
        }

        /// <summary>
        /// 构建对话上下文提示词
        /// </summary>
        public async Task<string> BuildContextPromptAsync(int chatId, int contextWindowSize)
        {
            var sb = new StringBuilder();

            // 获取系统上下文
            var contexts = await _contextRepository.GetContextsAsync(chatId);
            var systemContexts = contexts.Where(c => c.ContextType == "system").ToList();

            if (systemContexts.Any())
            {
                sb.AppendLine("## 系统上下文");
                foreach (var ctx in systemContexts.OrderByDescending(c => c.Priority))
                {
                    sb.AppendLine(ctx.Content);
                }
                sb.AppendLine();
            }

            // 获取用户偏好
            var userPreferences = contexts.Where(c => c.ContextType == "user_preference").ToList();
            if (userPreferences.Any())
            {
                sb.AppendLine("## 用户偏好");
                foreach (var pref in userPreferences)
                {
                    sb.AppendLine(pref.Content);
                }
                sb.AppendLine();
            }

            // 获取最近的对话消息作为上下文
            var recentMessages = await _messageRepository.GetContextMessagesAsync(chatId, contextWindowSize);

            if (recentMessages.Any())
            {
                sb.AppendLine("## 对话历史");
                foreach (var msg in recentMessages)
                {
                    var role = msg.Role == "user" ? "用户" : "助手";
                    sb.AppendLine($"{role}: {msg.Content}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// 添加系统上下文
        /// </summary>
        public async Task<bool> AddSystemContextAsync(int chatId, string context)
        {
            var chatContext = new ChatContext
            {
                ChatId = chatId,
                ContextType = "system",
                Content = context,
                Priority = 10,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var result = await _contextRepository.AddContextAsync(chatContext);
            return result != null;
        }

        /// <summary>
        /// 更新用户偏好
        /// </summary>
        public async Task<bool> UpdateUserPreferenceAsync(int chatId, string preference)
        {
            var contexts = await _contextRepository.GetContextsAsync(chatId);
            var existingPref = contexts.FirstOrDefault(c => c.ContextType == "user_preference");

            if (existingPref != null)
            {
                existingPref.Content = preference;
                existingPref.UpdatedAt = DateTime.Now;
                return await _contextRepository.UpdateContextAsync(existingPref);
            }
            else
            {
                var newPref = new ChatContext
                {
                    ChatId = chatId,
                    ContextType = "user_preference",
                    Content = preference,
                    Priority = 5,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var result = await _contextRepository.AddContextAsync(newPref);
                return result != null;
            }
        }

        /// <summary>
        /// 生成对话摘要
        /// </summary>
        public async Task<string> GenerateConversationSummaryAsync(int chatId)
        {
            var messages = await _messageRepository.GetChatMessagesAsync(chatId, limit: 100);

            if (!messages.Any())
                return "暂无对话内容";

            var sb = new StringBuilder();
            sb.AppendLine("对话摘要：");

            // 简单的摘要生成：列出主要话题
            var userMessages = messages.Where(m => m.Role == "user").ToList();
            if (userMessages.Count > 0)
            {
                sb.AppendLine($"用户提出了 {userMessages.Count} 个问题");
                
                // 列出前3个问题的摘要
                foreach (var msg in userMessages.Take(3))
                {
                    var summary = msg.Content.Length > 50 
                        ? msg.Content.Substring(0, 50) + "..." 
                        : msg.Content;
                    sb.AppendLine($"- {summary}");
                }

                if (userMessages.Count > 3)
                {
                    sb.AppendLine($"- 以及其他 {userMessages.Count - 3} 个问题");
                }
            }

            return sb.ToString();
        }
    }
}
