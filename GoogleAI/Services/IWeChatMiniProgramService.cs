using GoogleAI.Models;

namespace GoogleAI.Services
{
    public interface IWeChatMiniProgramService
    {
        Task<WeChatUserInfo> GetUserInfoAsync(string code);
        Task<string> DecryptUserDataAsync(string encryptedData, string iv, string sessionKey);
        Task<QrScanSession> CreateQrSessionAsync();
        Task<QrScanSession?> GetQrSessionAsync(string sessionId);
        Task<bool> UpdateQrSessionAsync(QrScanSession session);
        Task<QrScanSession?> HandleScanAsync(string sessionId, WeChatScanLoginRequest request);
        Task<QrScanSession?> AuthorizeAsync(string sessionId);
        Task<LoginResponse> LoginOrRegisterAsync(QrScanSession session);
        Task CleanupExpiredSessionsAsync();
    }
}
