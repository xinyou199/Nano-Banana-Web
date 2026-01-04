using GoogleAI.Models;
using GoogleAI.Repositories;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GoogleAI.Services
{
    public interface IChatService
    {
        Task<ChatResponse> CreateChatAsync(int userId, int modelId, string title);
        Task<ChatResponse> GetChatAsync(int chatId);
        Task<List<ChatResponse>> GetUserChatsAsync(int userId);
        Task<ChatMessage> SendMessageAsync(int userId, SendMessageRequest request);
        Task<bool> DeleteChatAsync(int chatId);
        IAsyncEnumerable<StreamMessageEvent> SendMessageStreamAsync(int userId, SendMessageRequest request);
    }

    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;
        private readonly IChatMessageRepository _messageRepository;
        private readonly IModelConfigurationRepository _modelRepository;
        private readonly IContextManagementService _contextService;
        private readonly ITokenCountService _tokenService;
        private readonly ILogger<ChatService> _logger;
        private readonly HttpClient _httpClient;

        public ChatService(
            IChatRepository chatRepository,
            IChatMessageRepository messageRepository,
            IModelConfigurationRepository modelRepository,
            IContextManagementService contextService,
            ITokenCountService tokenService,
            ILogger<ChatService> logger,
            HttpClient httpClient)
        {
            _chatRepository = chatRepository;
            _messageRepository = messageRepository;
            _modelRepository = modelRepository;
            _contextService = contextService;
            _tokenService = tokenService;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<ChatResponse> CreateChatAsync(int userId, int modelId, string title)
        {
            try
            {
                var chat = await _chatRepository.CreateChatAsync(userId, modelId, title);
                var model = await _modelRepository.GetByIdAsync(modelId);

                return new ChatResponse
                {
                    Id = chat.Id,
                    Title = chat.Title,
                    ModelId = chat.ModelId,
                    ModelName = model?.ModelName ?? "Unknown",
                    CreatedAt = chat.CreatedAt,
                    UpdatedAt = chat.UpdatedAt,
                    Messages = new()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating chat: {ex.Message}");
                throw;
            }
        }

        public async Task<ChatResponse> GetChatAsync(int chatId)
        {
            try
            {
                var chat = await _chatRepository.GetChatByIdAsync(chatId);
                if (chat == null)
                    throw new Exception("Chat not found");

                var messages = await _messageRepository.GetChatMessagesAsync(chatId);
                var model = await _modelRepository.GetByIdAsync(chat.ModelId);

                return new ChatResponse
                {
                    Id = chat.Id,
                    Title = chat.Title,
                    ModelId = chat.ModelId,
                    ModelName = model?.ModelName ?? "Unknown",
                    CreatedAt = chat.CreatedAt,
                    UpdatedAt = chat.UpdatedAt,
                    Messages = messages.Select(m => new ChatMessageResponse
                    {
                        Id = m.Id,
                        ChatId = m.ChatId,
                        Role = m.Role,
                        Content = m.Content,
                        ImageUrls = JsonSerializer.Deserialize<List<string>>(m.ImageUrls) ?? new(),
                        CreatedAt = m.CreatedAt
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting chat: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ChatResponse>> GetUserChatsAsync(int userId)
        {
            try
            {
                var chats = await _chatRepository.GetUserChatsAsync(userId);
                var responses = new List<ChatResponse>();

                foreach (var chat in chats)
                {
                    var model = await _modelRepository.GetByIdAsync(chat.ModelId);
                    responses.Add(new ChatResponse
                    {
                        Id = chat.Id,
                        Title = chat.Title,
                        ModelId = chat.ModelId,
                        ModelName = model?.ModelName ?? "Unknown",
                        CreatedAt = chat.CreatedAt,
                        UpdatedAt = chat.UpdatedAt,
                        Messages = new()
                    });
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user chats: {ex.Message}");
                throw;
            }
        }

        public async Task<ChatMessage> SendMessageAsync(int userId, SendMessageRequest request)
        {
            try
            {
                // 保存用户消息
                var userMessage = new ChatMessage
                {
                    ChatId = request.ChatId,
                    UserId = userId,
                    Role = "user",
                    Content = request.Content,
                    ImageUrls = JsonSerializer.Serialize(request.ImageUrls),
                    TokenCount = _tokenService.EstimateTokenCount(request.Content),
                    CreatedAt = DateTime.Now
                };

                await _messageRepository.AddMessageAsync(userMessage);

                // 构建上下文
                var contextPrompt = request.UseContext
                    ? await _contextService.BuildContextPromptAsync(request.ChatId, request.ContextWindowSize)
                    : string.Empty;

                // 这里应该调用AI模型，但由于没有实现AIModelService，暂时返回模拟响应
                var aiResponse = "这是一个模拟的AI响应。实际应用中应该调用真实的AI模型API。";

                // 保存AI响应
                var assistantMessage = new ChatMessage
                {
                    ChatId = request.ChatId,
                    UserId = userId,
                    Role = "assistant",
                    Content = aiResponse,
                    ImageUrls = "[]",
                    TokenCount = _tokenService.EstimateTokenCount(aiResponse),
                    CreatedAt = DateTime.Now
                };

                await _messageRepository.AddMessageAsync(assistantMessage);

                return assistantMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending message: {ex.Message}");
                throw;
            }
        }

        public async IAsyncEnumerable<StreamMessageEvent> SendMessageStreamAsync(int userId, SendMessageRequest request)
        {
            // 保存用户消息
            var userMessage = new ChatMessage
            {
                ChatId = request.ChatId,
                UserId = userId,
                Role = "user",
                Content = request.Content,
                ImageUrls = JsonSerializer.Serialize(request.ImageUrls),
                TokenCount = _tokenService.EstimateTokenCount(request.Content),
                CreatedAt = DateTime.Now
            };

            await _messageRepository.AddMessageAsync(userMessage);

            // 构建上下文
            var contextPrompt = request.UseContext
                ? await _contextService.BuildContextPromptAsync(request.ChatId, request.ContextWindowSize)
                : string.Empty;

            // 创建助手消息占位符
            var assistantMessage = new ChatMessage
            {
                ChatId = request.ChatId,
                UserId = userId,
                Role = "assistant",
                Content = string.Empty,
                ImageUrls = "[]",
                CreatedAt = DateTime.Now
            };

            ChatMessage savedMessage = await _messageRepository.AddMessageAsync(assistantMessage);

            yield return new StreamMessageEvent
            {
                Type = "start",
                MessageId = savedMessage.Id
            };

            var fullContent = string.Empty;
            var errorOccurred = false;
            var errorMessage = string.Empty;

            // 获取模型配置
            ModelConfiguration model = null;
            var chat = await _chatRepository.GetChatByIdAsync(request.ChatId);
            if (chat == null)
            {
                errorOccurred = true;
                errorMessage = "对话不存在";
            }
            else
            {
                model = await _modelRepository.GetByIdAsync(chat.ModelId);
                if (model == null)
                {
                    errorOccurred = true;
                    errorMessage = "模型配置不存在";
                }
            }

            if (!errorOccurred && model != null)
            {
                var events = await ProcessStreamAsync(model, contextPrompt, request, savedMessage, fullContent);
                foreach (var evt in events)
                {
                    yield return evt;
                }
            }

            if (errorOccurred)
            {
                yield return new StreamMessageEvent
                {
                    Type = "error",
                    Error = errorMessage,
                    MessageId = savedMessage.Id
                };
            }
            else if (!errorOccurred && model != null)
            {
                yield return new StreamMessageEvent
                {
                    Type = "end",
                    MessageId = savedMessage.Id
                };
            }
        }

        private async Task<List<StreamMessageEvent>> ProcessStreamAsync(
            ModelConfiguration model,
            string contextPrompt,
            SendMessageRequest request,
            ChatMessage savedMessage,
            string fullContent)
        {
            var events = new List<StreamMessageEvent>();
            var accumulatedContent = string.Empty;

            try
            {
                _logger.LogInformation($"[Chat] 开始处理流式响应，模型: {model.ModelName}, API: {model.ApiUrl}");

                // 构建消息列表
                var messages = new List<object>
                {
                    new { role = "user", content = contextPrompt + request.Content }
                };

                // 构建请求体
                var requestBody = new
                {
                    model = model.ModelName.ToLower(),
                    messages = messages,
                    max_tokens = model.MaxTokens,
                    temperature = (double)model.Temperature,
                    stream = true
                };

                var json = JsonSerializer.Serialize(requestBody);
                _logger.LogInformation($"[Chat] 请求体大小: {json.Length} 字节");

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, model.ApiUrl);
                httpRequest.Headers.Add("Authorization", $"Bearer {model.ApiKey}");
                httpRequest.Content = content;

                _logger.LogInformation($"[Chat] 发送请求到: {model.ApiUrl}");

                var response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead);

                _logger.LogInformation($"[Chat] 收到响应状态码: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[Chat] AI API调用失败: {response.StatusCode}, {errorContent}");
                    throw new Exception($"AI API调用失败: {response.StatusCode}, {errorContent}");
                }

                // 处理流式响应
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                var lineCount = 0;
                var contentCount = 0;
                var rawResponseSample = string.Empty;

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    lineCount++;

                    // 保存前几行用于调试
                    if (lineCount <= 3 && rawResponseSample.Length < 500)
                    {
                        rawResponseSample += line + "\n";
                    }

                    if (line.StartsWith("data: "))
                        line = line.Substring(6);

                    if (line == "[DONE]")
                    {
                        _logger.LogInformation($"[Chat] 流式响应结束，共收到 {lineCount} 行，{contentCount} 个内容块");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(line);
                        var root = jsonDoc.RootElement;

                        // 调试：记录响应结构
                        if (lineCount == 1)
                        {
                            _logger.LogInformation($"[Chat] 第一条响应的顶级属性: {string.Join(", ", root.EnumerateObject().Select(p => p.Name))}");
                        }

                        // 尝试多种可能的响应格式
                        bool foundContent = false;

                        // 格式1: OpenAI标准格式 - choices[0].delta.content
                        if (root.TryGetProperty("choices", out var choicesEl) &&
                            choicesEl.ValueKind == JsonValueKind.Array &&
                            choicesEl.GetArrayLength() > 0)
                        {
                            var choice = choicesEl[0];
                            if (choice.TryGetProperty("delta", out var deltaEl) &&
                                deltaEl.TryGetProperty("content", out var contentEl))
                            {
                                var chunk = contentEl.GetString();
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    accumulatedContent += chunk;
                                    contentCount++;
                                    _logger.LogDebug($"[Chat] 收到内容块 #{contentCount} (格式1): {chunk}");
                                    events.Add(new StreamMessageEvent
                                    {
                                        Type = "content",
                                        Content = chunk,
                                        MessageId = savedMessage.Id
                                    });
                                    foundContent = true;
                                }
                            }
                        }

                        // 格式2: 直接content字段
                        if (!foundContent && root.TryGetProperty("content", out var directContentEl))
                        {
                            var chunk = directContentEl.GetString();
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                accumulatedContent += chunk;
                                contentCount++;
                                _logger.LogDebug($"[Chat] 收到内容块 #{contentCount} (格式2): {chunk}");
                                events.Add(new StreamMessageEvent
                                {
                                    Type = "content",
                                    Content = chunk,
                                    MessageId = savedMessage.Id
                                });
                                foundContent = true;
                            }
                        }

                        // 格式3: message.content
                        if (!foundContent && root.TryGetProperty("message", out var messageEl) &&
                            messageEl.TryGetProperty("content", out var msgContentEl))
                        {
                            var chunk = msgContentEl.GetString();
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                accumulatedContent += chunk;
                                contentCount++;
                                _logger.LogDebug($"[Chat] 收到内容块 #{contentCount} (格式3): {chunk}");
                                events.Add(new StreamMessageEvent
                                {
                                    Type = "content",
                                    Content = chunk,
                                    MessageId = savedMessage.Id
                                });
                                foundContent = true;
                            }
                        }

                        // 如果没有找到内容，记录原始响应用于调试
                        if (!foundContent && lineCount <= 5)
                        {
                            _logger.LogDebug($"[Chat] 第 {lineCount} 行未找到内容，原始JSON: {line}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogDebug($"[Chat] 解析JSON失败: {ex.Message}, 行内容: {line}");
                    }
                }

                if (lineCount > 0)
                {
                    _logger.LogInformation($"[Chat] 原始响应样本:\n{rawResponseSample}");
                }

                _logger.LogInformation($"[Chat] 累积内容长度: {accumulatedContent.Length}");

                // 更新消息内容
                if (!string.IsNullOrEmpty(accumulatedContent))
                {
                    savedMessage.Content = accumulatedContent;
                    savedMessage.TokenCount = _tokenService.EstimateTokenCount(accumulatedContent);
                    await _messageRepository.UpdateMessageAsync(savedMessage);
                    _logger.LogInformation($"[Chat] 消息已保存，ID: {savedMessage.Id}");
                }
                else
                {
                    _logger.LogWarning($"[Chat] 未收到任何内容");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Chat] 流式响应处理失败: {ex.Message}\n{ex.StackTrace}");
                events.Add(new StreamMessageEvent
                {
                    Type = "error",
                    Error = ex.Message,
                    MessageId = savedMessage.Id
                });
            }

            return events;
        }

        public async Task<bool> DeleteChatAsync(int chatId)
        {
            try
            {
                return await _chatRepository.DeleteChatAsync(chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting chat: {ex.Message}");
                throw;
            }
        }
    }
}
