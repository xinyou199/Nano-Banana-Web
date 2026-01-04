using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using CloudflareR2.NET;

namespace GoogleAI.Services
{
    /// <summary>
    /// 图片拆分服务
    /// </summary>
    public interface IImageSplitService
    {
        Task<List<SplitBlockResult>> SplitImageAsync(string imageUrl, int rows, int cols, List<int> selectedBlocks);
        Task<List<SplitBlockResult>> SplitImageAsync2(string imageUrl, int rows, int cols, List<int> selectedBlocks, double insetPercentage = 0.02);
    }

    public class ImageSplitService : IImageSplitService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageSplitService> _logger;
        private readonly ICloudflareR2Client? _r2Client;

        public ImageSplitService(
            IWebHostEnvironment environment,
            ILogger<ImageSplitService> logger,
            ICloudflareR2Client? r2Client = null)
        {
            _environment = environment;
            _logger = logger;
            _r2Client = r2Client;
        }


        public async Task<List<SplitBlockResult>> SplitImageAsync2(
        string imageUrl,
        int rows,
        int cols,
        List<int> selectedBlocks,
        double insetPercentage = 0.02) // 内缩比例，默认2%
        {
            _logger.LogInformation($"开始拆分图片: {imageUrl}, 拆分为 {rows}x{cols}");

            string localImagePath = await GetLocalImagePathAsync(imageUrl);
            if (!File.Exists(localImagePath))
            {
                throw new FileNotFoundException($"图片文件不存在: {localImagePath}");
            }

            using var image = await Image.LoadAsync(localImagePath);
            _logger.LogInformation($"图片尺寸: {image.Width}x{image.Height}");

            int blockWidth = image.Width / cols;
            int blockHeight = image.Height / rows;

            var tempFolder = Path.Combine(_environment.WebRootPath, "uploads", "temp", "split");
            Directory.CreateDirectory(tempFolder);

            var blocks = new List<SplitBlockResult>();
            int index = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (!selectedBlocks.Contains(index))
                    {
                        index++;
                        continue;
                    }

                    try
                    {
                        // 计算基础裁剪区域
                        int baseX = col * blockWidth;
                        int baseY = row * blockHeight;
                        int baseWidth = blockWidth;
                        int baseHeight = blockHeight;

                        // 根据位置确定需要内缩的边
                        bool insetLeft = col > 0;           // 不是第一列
                        bool insetRight = col < cols - 1;    // 不是最后一列
                        bool insetTop = row > 0;             // 不是第一行
                        bool insetBottom = row < rows - 1;   // 不是最后一行

                        // 应用内缩并保持长宽比
                        var cropRect = CalculateInsetRectangle(
                            baseX, baseY, baseWidth, baseHeight,
                            insetLeft, insetRight, insetTop, insetBottom,
                            insetPercentage
                        );

                        // 裁剪分块
                        using var blockImage = image.Clone(ctx => ctx.Crop(cropRect));

                        // 生成文件名并保存
                        string filename = $"block_{row}_{col}_{Guid.NewGuid()}.png";
                        string filePath = Path.Combine(tempFolder, filename);
                        await blockImage.SaveAsPngAsync(filePath);

                        blocks.Add(new SplitBlockResult
                        {
                            Index = index,
                            Row = row,
                            Col = col,
                            LocalPath = filePath,
                            RelativeUrl = $"/uploads/temp/split/{filename}"
                        });

                        _logger.LogInformation(
                            $"成功拆分分块 {index} (row:{row}, col:{col}), " +
                            $"内缩: L:{insetLeft} R:{insetRight} T:{insetTop} B:{insetBottom}"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"拆分分块 {index} 失败");
                        throw;
                    }

                    index++;
                }
            }

            _logger.LogInformation($"图片拆分完成，共生成 {blocks.Count} 个分块");
            return blocks;
        }

        /// <summary>
        /// 计算内缩后的矩形区域，保持原始长宽比
        /// </summary>
        private Rectangle CalculateInsetRectangle(
            int baseX, int baseY, int baseWidth, int baseHeight,
            bool insetLeft, bool insetRight, bool insetTop, bool insetBottom,
            double insetPercentage)
        {
            // 计算内缩像素数
            int insetX = (int)(baseWidth * insetPercentage);
            int insetY = (int)(baseHeight * insetPercentage);

            // 应用内缩
            int x = baseX + (insetLeft ? insetX : 0);
            int y = baseY + (insetTop ? insetY : 0);
            int width = baseWidth - (insetLeft ? insetX : 0) - (insetRight ? insetX : 0);
            int height = baseHeight - (insetTop ? insetY : 0) - (insetBottom ? insetY : 0);

            // 保持长宽比
            double originalRatio = (double)baseWidth / baseHeight;
            double currentRatio = (double)width / height;

            if (Math.Abs(currentRatio - originalRatio) > 0.001)
            {
                // 根据内缩的边数决定如何调整
                int insetCount = (insetLeft ? 1 : 0) + (insetRight ? 1 : 0) +
                                (insetTop ? 1 : 0) + (insetBottom ? 1 : 0);

                if (currentRatio > originalRatio)
                {
                    // 当前太宽，需要进一步缩小宽度
                    int targetWidth = (int)(height * originalRatio);
                    int widthDiff = width - targetWidth;

                    if (insetLeft && insetRight)
                    {
                        // 左右都内缩，平均分配
                        x += widthDiff / 2;
                        width = targetWidth;
                    }
                    else if (insetLeft)
                    {
                        // 只内缩左侧
                        x += widthDiff;
                        width = targetWidth;
                    }
                    else if (insetRight)
                    {
                        // 只内缩右侧
                        width = targetWidth;
                    }
                }
                else
                {
                    // 当前太高，需要进一步缩小高度
                    int targetHeight = (int)(width / originalRatio);
                    int heightDiff = height - targetHeight;

                    if (insetTop && insetBottom)
                    {
                        // 上下都内缩，平均分配
                        y += heightDiff / 2;
                        height = targetHeight;
                    }
                    else if (insetTop)
                    {
                        // 只内缩上侧
                        y += heightDiff;
                        height = targetHeight;
                    }
                    else if (insetBottom)
                    {
                        // 只内缩下侧
                        height = targetHeight;
                    }
                }
            }

            return new Rectangle(x, y, width, height);
        }


        public async Task<List<SplitBlockResult>> SplitImageAsync(
            string imageUrl,
            int rows,
            int cols,
            List<int> selectedBlocks)
        {
            _logger.LogInformation($"开始拆分图片: {imageUrl}, 拆分为 {rows}x{cols}, 选中块数: {selectedBlocks.Count}");

            // 1. 获取本地图片路径
            string localImagePath = await GetLocalImagePathAsync(imageUrl);

            if (!File.Exists(localImagePath))
            {
                throw new FileNotFoundException($"图片文件不存在: {localImagePath}");
            }

            // 2. 加载图片
            using var image = await Image.LoadAsync(localImagePath);
            _logger.LogInformation($"图片尺寸: {image.Width}x{image.Height}");

            // 3. 计算每个分块尺寸
            int blockWidth = image.Width / cols;
            int blockHeight = image.Height / rows;

            // 4. 创建临时目录
            var tempFolder = Path.Combine(_environment.WebRootPath, "uploads", "temp", "split");
            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }

            var blocks = new List<SplitBlockResult>();
            int index = 0;

            // 5. 遍历所有分块
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    // 只处理选中的分块
                    if (!selectedBlocks.Contains(index))
                    {
                        index++;
                        continue;
                    }

                    try
                    {
                        // 计算裁剪区域
                        var cropRect = new Rectangle(
                            col * blockWidth,
                            row * blockHeight,
                            blockWidth,
                            blockHeight
                        );

                        // 裁剪分块
                        using var blockImage = image.Clone(ctx => ctx.Crop(cropRect));

                        // 生成文件名
                        string filename = $"block_{row}_{col}_{Guid.NewGuid()}.png";
                        string filePath = Path.Combine(tempFolder, filename);

                        // 保存到本地
                        await blockImage.SaveAsPngAsync(filePath);

                        blocks.Add(new SplitBlockResult
                        {
                            Index = index,
                            Row = row,
                            Col = col,
                            LocalPath = filePath,
                            RelativeUrl = $"/uploads/temp/split/{filename}"
                        });

                        _logger.LogInformation($"成功拆分分块 {index} (row:{row}, col:{col})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"拆分分块 {index} 失败");
                        throw;
                    }

                    index++;
                }
            }

            _logger.LogInformation($"图片拆分完成，共生成 {blocks.Count} 个分块");
            return blocks;
        }

        /// <summary>
        /// 获取本地图片路径（处理R2和本地路径）
        /// </summary>
        private async Task<string> GetLocalImagePathAsync(string imageUrl)
        {
            // 情况1: 已经是本地绝对路径
            if (File.Exists(imageUrl))
            {
                _logger.LogInformation($"使用本地绝对路径: {imageUrl}");
                return imageUrl;
            }

            // 情况2: 相对路径（如 /uploads/generated/xxx.png）
            if (imageUrl.StartsWith("/"))
            {
                var localPath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(localPath))
                {
                    _logger.LogInformation($"使用本地相对路径: {localPath}");
                    return localPath;
                }
            }

            // 情况3: R2完整URL（如 https://xxx.r2.cloudflarestorage.com/xxx.png）
            if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
            {
                _logger.LogInformation($"检测到R2 URL，开始下载: {imageUrl}");

                // 下载到临时目录
                var tempFolder = Path.Combine(_environment.WebRootPath, "uploads", "temp", "download");
                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                var fileName = $"download_{Guid.NewGuid()}.png";
                var downloadPath = Path.Combine(tempFolder, fileName);

                using var httpClient = new HttpClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(downloadPath, imageBytes);

                _logger.LogInformation($"R2图片下载成功: {downloadPath}");
                return downloadPath;
            }

            throw new FileNotFoundException($"无法定位图片文件: {imageUrl}");
        }
    }

    /// <summary>
    /// 拆分分块结果
    /// </summary>
    public class SplitBlockResult
    {
        public int Index { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public string LocalPath { get; set; } = string.Empty;
        public string RelativeUrl { get; set; } = string.Empty;
    }
}
