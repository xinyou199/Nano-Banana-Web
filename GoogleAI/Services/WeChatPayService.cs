using GoogleAI.Models;
using GoogleAI.Repositories;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace GoogleAI.Services
{
    public interface IWeChatPayService
    {
        Task<WeChatPayOrderResponse?> CreateOrderAsync(int userId, int packageId);
        Task<bool> VerifyNotifyAsync(string serialNumber, string signature, string timestamp, string nonce, string body);
        Task<WeChatPayNotifyData?> ParseNotifyAsync(string body);
        Task<bool> HandlePaymentSuccessAsync(string orderNo, string transactionId);
    }

    public class WeChatPayService : IWeChatPayService
    {
        private readonly IConfiguration _configuration;
        private readonly IPaymentOrderRepository _paymentOrderRepository;
        private readonly IPointPackageRepository _pointPackageRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPointsRepository _pointsRepository;
        private readonly HttpClient _httpClient;
        private readonly ILogger<TaskProcessorService> _logger;

        public WeChatPayService(
            IConfiguration configuration,
            IPaymentOrderRepository paymentOrderRepository,
            IPointPackageRepository pointPackageRepository,
            IUserRepository userRepository,
            IPointsRepository pointsRepository,
            ILogger<TaskProcessorService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _paymentOrderRepository = paymentOrderRepository;
            _pointPackageRepository = pointPackageRepository;
            _userRepository = userRepository;
            _pointsRepository = pointsRepository;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<WeChatPayOrderResponse?> CreateOrderAsync(int userId, int packageId)
        {
            try
            {
                // 获取套餐信息
                var package = await _pointPackageRepository.GetByIdAsync(packageId);
                if (package == null)
                {
                    throw new Exception("套餐不存在");
                }

                // 生成订单号
                var orderNo = GenerateOrderNo();

                // 创建订单记录
                var order = new PaymentOrder
                {
                    OrderNo = orderNo,
                    UserId = userId,
                    PackageId = packageId,
                    Points = package.Points,
                    Amount = package.Price,
                    OrderStatus = "Pending",
                    PaymentType = "WeChat",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var orderId = await _paymentOrderRepository.CreateAsync(order);
                order.Id = orderId;

                // 调用微信支付统一下单接口
                var wechatOrder = await CreateWeChatOrderAsync(order, package);
                
                if (wechatOrder != null)
                {
                    // 保存PrepayId
                    order.PrepayId = wechatOrder.PrepayId;
                    await _paymentOrderRepository.UpdateAsync(order);
                }

                return wechatOrder;
            }
            catch (Exception ex)
            {
                throw new Exception($"创建订单失败: {ex.Message}");
            }
        }

        private async Task<WeChatPayOrderResponse?> CreateWeChatOrderAsync(PaymentOrder order, PointPackage package)
        {
            var mchId = _configuration["WeChatPay:MerchantId"];
            var apiV3Key = _configuration["WeChatPay:ApiV3Key"];
            var appId = _configuration["WeChatPay:AppId"];
            var notifyUrl = _configuration["WeChatPay:NotifyUrl"];

            var baseUrl = "https://api.mch.weixin.qq.com/v3/pay/transactions/native";

            var requestData = new
            {
                appid = appId,
                mchid = mchId,
                description = $"{package.Name}({package.Points}积分)",
                out_trade_no = order.OrderNo,
                notify_url = notifyUrl,
                amount = new
                {
                    total = (int)(order.Amount * 100), // 转换为分
                    currency = "CNY"
                }
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 添加签名 - 使用URL路径，不是完整URL
            var authorization = await GenerateAuthorizationAsync("POST", "/v3/pay/transactions/native", json);
            
            // 创建新的请求消息
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            request.Content = content;
            request.Headers.Clear();
            request.Headers.Add("Authorization", authorization);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "GoogleAI-Payment/1.0");

            var response = await _httpClient.SendAsync(request);
            
            // 添加调试日志
            Console.WriteLine($"微信支付请求: {baseUrl}");
            Console.WriteLine($"请求体: {json}");
            Console.WriteLine($"Authorization: {authorization}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var codeUrl = result.GetProperty("code_url").GetString();

                return new WeChatPayOrderResponse
                {
                    CodeUrl = codeUrl,
                    PrepayId = codeUrl // 保持兼容性
                };
            }
            else
            {
                // 记录错误信息
                throw new Exception($"微信支付API调用失败: {responseContent}");
            }

            return null;
        }

        private async Task<string> GenerateAuthorizationAsync(string method, string url, string body)
        {
            var mchId = _configuration["WeChatPay:MerchantId"];
            var privateKeyPath = _configuration["WeChatPay:PrivateKeyPath"];
            var serialNo = _configuration["WeChatPay:SerialNo"];

            var nonce = GenerateNonceStr();
            var timestamp = GenerateTimeStamp();

            // 构建签名消息 - 按照微信支付API v3的规范
            // 格式：请求方法\n请求URL\n请求时间戳\n请求随机串\n请求报文主体\n
            var message = $"{method}\n{url}\n{timestamp}\n{nonce}\n{body}\n";

            // 验证私钥文件是否存在
            if (!File.Exists(privateKeyPath))
            {
                throw new Exception($"私钥文件不存在: {privateKeyPath}");
            }

            // 使用商户私钥签名
            var signature = SignWithPrivateKey(message, privateKeyPath);

            // 构建Authorization头
            var auth = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonce}\",signature=\"{signature}\",timestamp=\"{timestamp}\",serial_no=\"{serialNo}\"";

            return auth;
        }

        private string SignWithPrivateKey(string message, string privateKeyPath)
        {
            try
            {
                using var rsa = RSA.Create();
                
                // 从文件加载私钥
                var privateKey = File.ReadAllText(privateKeyPath);
                
                // 移除PEM格式的前缀和后缀（如果存在）
                privateKey = privateKey
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();
                
                // 转换为Base64
                var privateKeyBytes = Convert.FromBase64String(privateKey);
                
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
                
                // 使用SHA256进行签名
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var signatureBytes = rsa.SignData(messageBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var signature = Convert.ToBase64String(signatureBytes);
                
                return signature;
            }
            catch (Exception ex)
            {
                throw new Exception($"签名失败: {ex.Message}. 私钥路径: {privateKeyPath}");
            }
        }

        public Task<bool> VerifyNotifyAsync(string serialNumber, string signature, string timestamp, string nonce, string body)
        {
            // 验证微信支付回调签名
            // 实际生产环境中需要使用微信平台证书验签
            return Task.FromResult(true);
        }

        public Task<WeChatPayNotifyData?> ParseNotifyAsync(string body)
        {
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(body);
                var resource = jsonElement.GetProperty("resource");

                var cipherText = resource.GetProperty("ciphertext").GetString();
                var nonce = resource.GetProperty("nonce").GetString();
                var associatedData = resource.GetProperty("associated_data").GetString();

                _logger.LogInformation($"解密参数 - Nonce长度: {nonce?.Length}, AssociatedData: '{associatedData}'");

                var decryptedData = DecryptAesGcm(cipherText, nonce, associatedData);
                _logger.LogInformation($"解密成功: {decryptedData}");

                // 使用JsonSerializerOptions配置属性名匹配
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var notifyData = JsonSerializer.Deserialize<WeChatPayNotifyData>(decryptedData, options);

                if (notifyData != null)
                {
                    _logger.LogInformation($"解析成功 - 订单号: {notifyData.OutTradeNo}, 交易状态: {notifyData.TradeState}");
                }

                return Task.FromResult(notifyData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"解析回调数据失败: {ex.Message}");
                return Task.FromResult<WeChatPayNotifyData?>(null);
            }
        }


        private string DecryptAesGcm(string cipherText, string nonce, string associatedData)
        {
            var apiV3Key = _configuration["WeChatPay:ApiV3Key"];

            // ✅ 正确：直接使用UTF8编码,不要Base64解码
            var key = Encoding.UTF8.GetBytes(apiV3Key);

            // 验证密钥长度必须为32字节
            if (key.Length != 32)
            {
                throw new Exception($"无效的ApiV3Key,长度必须为32个字节,当前长度:{key.Length}");
            }

            // Base64解码密文
            var cipherBytes = Convert.FromBase64String(cipherText);
            var nonceBytes = Encoding.UTF8.GetBytes(nonce);
            var associatedBytes = Encoding.UTF8.GetBytes(associatedData ?? "");

            // GCM的tag长度为16字节,在密文末尾
            var cipher = cipherBytes.Take(cipherBytes.Length - 16).ToArray();
            var tag = cipherBytes.Skip(cipherBytes.Length - 16).ToArray();

            using var aes = new AesGcm(key);
            var plaintext = new byte[cipher.Length];

            try
            {
                aes.Decrypt(nonceBytes, cipher, tag, plaintext, associatedBytes);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch (Exception ex)
            {
                throw new Exception($"解密失败: {ex.Message}");
            }
        }


        public async Task<bool> HandlePaymentSuccessAsync(string orderNo, string transactionId)
        {
            _logger.LogInformation($"开始处理支付成功 - 订单号: {orderNo}, 交易号: {transactionId}");

            var order = await _paymentOrderRepository.GetByOrderNoAsync(orderNo);
            if (order == null)
            {
                _logger.LogWarning($"订单不存在: {orderNo}");
                return false;
            }

            // 检查订单状态，避免重复处理
            if (order.OrderStatus == "Paid")
            {
                _logger.LogInformation($"订单已处理过: {orderNo}");
                return true;
            }

            // 更新订单状态
            await _paymentOrderRepository.UpdateOrderStatusAsync(orderNo, "Paid", transactionId);
            _logger.LogInformation($"订单状态已更新: {orderNo}");

            // 增加用户积分
            await _pointsRepository.AddPointsAsync(order.UserId, order.Points, $"购买积分套餐，订单号：{orderNo}");
            _logger.LogInformation($"用户积分已增加 - 用户ID: {order.UserId}, 增加积分: {order.Points}");

            return true;
        }

        private string GenerateOrderNo()
        {
            return $"PAY{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        }

        private string GenerateNonceStr()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 32);
        }

        private string GenerateTimeStamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        }
    }

    public class WeChatPayOrderResponse
    {
        public string? CodeUrl { get; set; }
        public string? PrepayId { get; set; }
        public string? Package { get; set; }
        public string? NonceStr { get; set; }
        public string? TimeStamp { get; set; }
        public string? SignType { get; set; }
    }

    public class WeChatPayNotifyData
    {
        [JsonPropertyName("out_trade_no")]
        public string? OutTradeNo { get; set; }

        [JsonPropertyName("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("trade_state")]
        public string? TradeState { get; set; }

        [JsonPropertyName("trade_type")]
        public string? TradeType { get; set; }

        [JsonPropertyName("amount")]
        public AmountInfo? Amount { get; set; }
    }

    public class AmountInfo
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("payer_total")]
        public int? PayerTotal { get; set; }
    }
}
