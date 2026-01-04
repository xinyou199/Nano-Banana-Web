using Microsoft.AspNetCore.Mvc;
using GoogleAI.Repositories;

namespace GoogleAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModelsController : ControllerBase
    {
        private readonly IModelConfigurationRepository _modelRepository;

        public ModelsController(IModelConfigurationRepository modelRepository)
        {
            _modelRepository = modelRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetModels()
        {
            try
            {
                var models = await _modelRepository.GetAllActiveAsync();
                return Ok(new { success = true, data = models });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"获取模型列表失败: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetModel(int id)
        {
            try
            {
                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound(new { success = false, message = "模型不存在" });
                }
                return Ok(new { success = true, data = model });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"获取模型详情失败: {ex.Message}" });
            }
        }
    }
}
