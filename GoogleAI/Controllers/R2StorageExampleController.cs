using Microsoft.AspNetCore.Mvc;
using CMSTaskApp.Store;
using Microsoft.Extensions.Logging;

namespace GoogleAI.Controllers
{
    /// <summary>
    /// R2存储服务使用示例控制器
    /// </summary>
    public class R2StorageExampleController : Controller
    {
        private readonly IR2StorageService _r2StorageService;
        private readonly ILogger<R2StorageExampleController> _logger;

        public R2StorageExampleController(
            IR2StorageService r2StorageService,
            ILogger<R2StorageExampleController> logger)
        {
            _r2StorageService = r2StorageService;
            _logger = logger;
        }

        /// <summary>
        /// 从文件流上传到R2存储
        /// </summary>
        [HttpPost]
        [Route("api/r2/upload-stream")]
        public async Task<IActionResult> UploadFromStream(IFormFile file, string zone = "anime")
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("请选择要上传的文件");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _r2StorageService.UploadFromStreamAsync(
                    stream, 
                    file.FileName, 
                    zone);

                if (string.IsNullOrEmpty(result))
                {
                    return StatusCode(500, "上传失败");
                }

                return Json(new { success = true, url = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传文件到R2存储时发生错误");
                return StatusCode(500, "上传过程中发生错误");
            }
        }

        /// <summary>
        /// 从URL上传到R2存储
        /// </summary>
        [HttpPost]
        [Route("api/r2/upload-url")]
        public async Task<IActionResult> UploadFromUrl([FromBody] UploadUrlRequest request, string zone = "anime")
        {
            if (string.IsNullOrEmpty(request.ImageUrl))
            {
                return BadRequest("请提供图片URL");
            }

            try
            {
                var result = await _r2StorageService.UploadFromUrlAsync(
                    request.ImageUrl, 
                    zone);

                if (string.IsNullOrEmpty(result))
                {
                    return StatusCode(500, "上传失败");
                }

                return Json(new { success = true, url = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从URL上传到R2存储时发生错误");
                return StatusCode(500, "上传过程中发生错误");
            }
        }
    }

    /// <summary>
    /// 上传URL请求模型
    /// </summary>
    public class UploadUrlRequest
    {
        public string ImageUrl { get; set; }
    }
}