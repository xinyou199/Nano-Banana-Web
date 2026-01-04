using System.Security.Cryptography;
using System.Text;

namespace GoogleAI.Services
{
    /// <summary>
    /// 微信支付签名测试辅助类
    /// 用于验证签名功能是否正常工作
    /// </summary>
    public class WeChatPayTestHelper
    {
        /// <summary>
        /// 测试签名功能
        /// </summary>
        /// <param name="privateKeyPath">私钥文件路径</param>
        /// <returns>测试结果</returns>
        public static TestResult TestSignature(string privateKeyPath)
        {
            var result = new TestResult { IsSuccess = false };
            
            try
            {
                // 1. 检查私钥文件是否存在
                if (!File.Exists(privateKeyPath))
                {
                    result.ErrorMessage = $"私钥文件不存在: {privateKeyPath}";
                    return result;
                }

                result.PrivateKeyExists = true;

                // 2. 读取私钥内容
                var privateKey = File.ReadAllText(privateKeyPath);
                result.PrivateKeyLength = privateKey.Length;

                // 3. 验证私钥格式
                if (!privateKey.Contains("-----BEGIN PRIVATE KEY-----") && 
                    !privateKey.Contains("-----BEGIN RSA PRIVATE KEY-----"))
                {
                    result.ErrorMessage = "私钥格式不正确，缺少PEM格式的标记";
                    return result;
                }

                result.PrivateKeyFormatValid = true;

                // 4. 尝试加载私钥
                using var rsa = RSA.Create();
                
                var privateKeyClean = privateKey
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();
                
                var privateKeyBytes = Convert.FromBase64String(privateKeyClean);
                
                // 尝试使用 ImportPkcs8PrivateKey 导入 PKCS#8 格式
                try
                {
                    rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                }
                catch
                {
                    // 如果失败，尝试使用 ImportRSAPrivateKey 导入 PKCS#1 格式
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                }
                
                result.PrivateKeyLoadSuccess = true;

                // 5. 测试签名功能
                var testMessage = $"1766738407\n50ae0320cc8e423cb0a63a6fb1d78965\nPOST\n/v3/pay/transactions/native\n\n";
                var messageBytes = Encoding.UTF8.GetBytes(testMessage);
                var signatureBytes = rsa.SignData(messageBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var signature = Convert.ToBase64String(signatureBytes);
                
                result.SignatureGenerated = true;
                result.SignatureLength = signature.Length;
                result.Signature = signature;
                result.IsSuccess = true;

                // 6. 测试验证签名
                var isValid = rsa.VerifyData(messageBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                result.SignatureVerified = isValid;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"测试失败: {ex.Message}";
                result.Exception = ex.ToString();
            }

            return result;
        }

        /// <summary>
        /// 打印测试结果
        /// </summary>
        public static void PrintTestResult(TestResult result)
        {
            Console.WriteLine("=== 微信支付签名测试结果 ===");
            Console.WriteLine($"测试状态: {(result.IsSuccess ? "✅ 成功" : "❌ 失败")}");
            Console.WriteLine($"私钥文件存在: {result.PrivateKeyExists}");
            Console.WriteLine($"私钥长度: {result.PrivateKeyLength} 字符");
            Console.WriteLine($"私钥格式有效: {result.PrivateKeyFormatValid}");
            Console.WriteLine($"私钥加载成功: {result.PrivateKeyLoadSuccess}");
            Console.WriteLine($"签名生成: {(result.SignatureGenerated ? "✅ 成功" : "❌ 失败")}");
            Console.WriteLine($"签名长度: {result.SignatureLength} 字符");
            Console.WriteLine($"签名验证: {(result.SignatureVerified ? "✅ 通过" : "❌ 失败")}");
            
            if (!string.IsNullOrEmpty(result.Signature))
            {
                Console.WriteLine($"\n签名示例（前50字符）: {result.Signature.Substring(0, Math.Min(50, result.Signature.Length))}...");
            }
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                Console.WriteLine($"\n❌ 错误信息: {result.ErrorMessage}");
            }
            
            Console.WriteLine("========================");
        }
    }

    public class TestResult
    {
        public bool IsSuccess { get; set; }
        public bool PrivateKeyExists { get; set; }
        public int PrivateKeyLength { get; set; }
        public bool PrivateKeyFormatValid { get; set; }
        public bool PrivateKeyLoadSuccess { get; set; }
        public bool SignatureGenerated { get; set; }
        public string? Signature { get; set; }
        public int SignatureLength { get; set; }
        public bool SignatureVerified { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Exception { get; set; }
    }
}
