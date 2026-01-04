using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GoogleAI.Repositories;
using System.Security.Claims;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HistoryController : ControllerBase
    {
        private readonly IDrawingHistoryRepository _historyRepository;

        public HistoryController(IDrawingHistoryRepository historyRepository)
        {
            _historyRepository = historyRepository;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory([FromQuery] int pageSize = 20, [FromQuery] int page = 1)
        {
            try
            {
                var userId = GetUserId();
                var offset = (page - 1) * pageSize;
                var history = await _historyRepository.GetUserHistoryAsync(userId, pageSize, offset);
                var totalCount = await _historyRepository.GetUserHistoryCountAsync(userId);
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                return Ok(new { 
                    success = true, 
                    data = history,
                    pagination = new {
                        currentPage = page,
                        pageSize = pageSize,
                        totalCount = totalCount,
                        totalPages = totalPages,
                        hasNextPage = page < totalPages,
                        hasPreviousPage = page > 1
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"获取历史记录失败: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHistory(int id)
        {
            try
            {
                var result = await _historyRepository.DeleteAsync(id);
                if (result)
                {
                    return Ok(new { success = true, message = "删除成功" });
                }
                return NotFound(new { success = false, message = "记录不存在" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"删除失败: {ex.Message}" });
            }
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearHistory()
        {
            try
            {
                var userId = GetUserId();
                var result = await _historyRepository.ClearUserHistoryAsync(userId);
                return Ok(new { success = true, message = "清空成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"清空失败: {ex.Message}" });
            }
        }
    }
}
