using GoogleAI.Repositories;

namespace GoogleAI.Services
{
    public interface ITokenCountService
    {
        int EstimateTokenCount(string text);
        int EstimateImageTokens(int width, int height);
        int GetModelContextLimit(int modelId);
        bool IsWithinContextLimit(int modelId, int totalTokens);
    }

    public class TokenCountService : ITokenCountService
    {
        private readonly IModelConfigurationRepository _modelRepository;
        private const float TokensPerCharacter = 0.25f; // 粗略估计：1个字符约0.25个token

        public TokenCountService(IModelConfigurationRepository modelRepository)
        {
            _modelRepository = modelRepository;
        }

        /// <summary>
        /// 估计文本的token数量
        /// </summary>
        public int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // 简单估计：中文字符较多，英文字符较少
            // 平均每个字符约0.25个token
            return (int)Math.Ceiling(text.Length * TokensPerCharacter);
        }

        /// <summary>
        /// 估计图片的token数量
        /// 根据OpenAI的定价，图片大小影响token消耗
        /// </summary>
        public int EstimateImageTokens(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return 0;

            // 简化计算：根据图片尺寸估计
            // 小图片（512x512以下）：85 tokens
            // 中等图片（512x512到1024x1024）：170 tokens
            // 大图片（1024x1024以上）：255 tokens

            int maxDimension = Math.Max(width, height);

            if (maxDimension <= 512)
                return 85;
            else if (maxDimension <= 1024)
                return 170;
            else
                return 255;
        }

        /// <summary>
        /// 获取模型的上下文窗口大小
        /// </summary>
        public int GetModelContextLimit(int modelId)
        {
            // 这里应该从数据库获取，但为了简化，返回默认值
            // 实际应该调用 _modelRepository.GetModelByIdAsync(modelId)
            return 4096; // 默认值
        }

        /// <summary>
        /// 检查总token数是否在模型的上下文限制内
        /// </summary>
        public bool IsWithinContextLimit(int modelId, int totalTokens)
        {
            int contextLimit = GetModelContextLimit(modelId);
            // 保留20%的缓冲区用于响应
            return totalTokens < (contextLimit * 0.8);
        }
    }
}
