using GoogleAI.Models;
using GoogleAI.Repositories;
using GoogleAI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GoogleAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IModelConfigurationRepository _modelRepository;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IChatService chatService,
            IModelConfigurationRepository modelRepository,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _modelRepository = modelRepository;
            _logger = logger;
        }

        /// <summary>
        /// 创建新对话
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var chat = await _chatService.CreateChatAsync(userId, request.ModelId, request.Title);
                return Ok(chat);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating chat: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 获取对话列表
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetChats([FromQuery] int pageSize = 20, [FromQuery] int pageNumber = 1)
        {
            try
            {
                var userId = GetUserId();
                var chats = await _chatService.GetUserChatsAsync(userId);
                return Ok(chats);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting chats: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 获取对话详情
        /// </summary>
        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChat(int chatId)
        {
            try
            {
                var chat = await _chatService.GetChatAsync(chatId);
                return Ok(chat);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting chat: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 发送消息（非流式）
        /// </summary>
        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = GetUserId();
                var message = await _chatService.SendMessageAsync(userId, request);
                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending message: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 发送消息（流式输出 - SSE）
        /// </summary>
        [HttpPost("message-stream")]
        public async Task SendMessageStream([FromBody] SendMessageRequest request)
        {
            try
            {
                var userId = GetUserId();
                Response.ContentType = "text/event-stream";
                await foreach (var @event in _chatService.SendMessageStreamAsync(userId, request))
                {
                    var json = JsonSerializer.Serialize(@event);
                    await Response.WriteAsync($"data: {json}\n\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in stream: {ex.Message}");
                var errorEvent = new StreamMessageEvent
                {
                    Type = "error",
                    Error = ex.Message
                };
                var json = JsonSerializer.Serialize(errorEvent);
                await Response.WriteAsync($"data: {json}\n\n");
            }
        }

        /// <summary>
        /// 删除对话
        /// </summary>
        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteChat(int chatId)
        {
            try
            {
                var result = await _chatService.DeleteChatAsync(chatId);
                return Ok(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting chat: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// 获取可用模型列表
        /// </summary>
        [HttpGet("models")]
        public async Task<IActionResult> GetAvailableModels()
        {
            try
            {
                var models = await _modelRepository.GetAllActiveAsync();
                var response = models.Select(m => new AIModelInfoResponse
                {
                    Id = m.Id,
                    ModelName = m.ModelName,
                    Description = m.Description,
                    IsMultimodalSupported = m.IsMultimodalSupported,
                    SupportsStreaming = m.SupportsStreaming,
                    ContextWindowSize = m.ContextWindowSize,
                    MaxImageSize = m.MaxImageSize,
                    SupportedImageFormats = m.SupportedImageFormats.Split(',').ToList(),
                    PointCost = m.PointCost
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting models: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }
    }
}
