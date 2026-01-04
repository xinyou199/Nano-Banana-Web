using GoogleAI.Models;
using GoogleAI.Repositories;

namespace GoogleAI.Services
{
    public interface IVerificationService
    {
        Task<SendVerificationCodeResponse> SendVerificationCodeAsync(string email);
        Task<bool> VerifyCodeAsync(string email, string code);
    }

    public class VerificationService : IVerificationService
    {
        private readonly IEmailVerificationRepository _verificationRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;

        public VerificationService(
            IEmailVerificationRepository verificationRepository,
            IEmailService emailService,
            IUserRepository userRepository)
        {
            _verificationRepository = verificationRepository;
            _emailService = emailService;
            _userRepository = userRepository;
        }

        public async Task<SendVerificationCodeResponse> SendVerificationCodeAsync(string email)
        {
            try
            {
                // 检查邮箱是否已被注册
                var existingUser = await _userRepository.GetByEmailAsync(email);
                if (existingUser != null)
                {
                    return new SendVerificationCodeResponse
                    {
                        Success = false,
                        Message = "该邮箱已被注册"
                    };
                }

                // 清理过期的验证码
                await _verificationRepository.DeleteExpiredAsync();

                // 生成6位随机验证码
                var verificationCode = GenerateRandomCode();

                // 保存验证码到数据库
                var verification = new EmailVerification
                {
                    Email = email,
                    VerificationCode = verificationCode,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddMinutes(10), // 10分钟有效期
                    IsUsed = false
                };

                var id = await _verificationRepository.CreateAsync(verification);
                if (id <= 0)
                {
                    return new SendVerificationCodeResponse
                    {
                        Success = false,
                        Message = "验证码生成失败"
                    };
                }

                // 发送邮件
                var emailSent = await _emailService.SendVerificationCodeAsync(email, verificationCode);
                if (!emailSent)
                {
                    return new SendVerificationCodeResponse
                    {
                        Success = false,
                        Message = "邮件发送失败，请稍后重试"
                    };
                }

                return new SendVerificationCodeResponse
                {
                    Success = true,
                    Message = "验证码已发送到您的邮箱，请查收"
                };
            }
            catch (Exception ex)
            {
                return new SendVerificationCodeResponse
                {
                    Success = false,
                    Message = $"发送验证码失败: {ex.Message}"
                };
            }
        }

        public async Task<bool> VerifyCodeAsync(string email, string code)
        {
            try
            {
                var verification = await _verificationRepository.GetLatestByEmailAsync(email);
                if (verification == null)
                {
                    return false;
                }

                if (verification.VerificationCode != code)
                {
                    return false;
                }

                if (verification.ExpiresAt < DateTime.Now)
                {
                    return false;
                }

                // 标记为已使用
                await _verificationRepository.MarkAsUsedAsync(verification.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateRandomCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}