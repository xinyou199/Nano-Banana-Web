using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace GoogleAI.Services
{
    public interface IEmailService
    {
        Task<bool> SendVerificationCodeAsync(string email, string verificationCode);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> SendVerificationCodeAsync(string email, string verificationCode)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var host = smtpSettings["Host"];
                var port = int.Parse(smtpSettings["Port"] ?? "587");
                var enableSsl = bool.Parse(smtpSettings["EnableSsl"] ?? "true");
                var userName = smtpSettings["UserName"];
                var password = smtpSettings["Password"];
                var fromEmail = smtpSettings["FromEmail"] ?? userName;

                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                {
                    // 如果没有配置SMTP，使用控制台输出（开发环境）
                    Console.WriteLine($"模拟发送邮件到 {email}: 验证码是 {verificationCode}");
                    return true;
                }

                // 针对网易邮箱的特殊处理
                if (host.Contains("yeah.net") || host.Contains("163.com") || host.Contains("126.com"))
                {
                    // 网易邮箱配置优化
                    using var client = new SmtpClient(host, port)
                    {
                        EnableSsl = true,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(userName, password),
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        Timeout = 30000
                    };
                    
                    // 网易邮箱需要From地址与认证地址一致
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(userName, "灵绘智影AI创作平台", Encoding.UTF8),
                        Subject = "邮箱验证码",
                        SubjectEncoding = Encoding.UTF8,
                        Body = GenerateVerificationEmailBody(verificationCode),
                        BodyEncoding = Encoding.UTF8,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(new MailAddress(email));
                    
                    try
                    {
                        await client.SendMailAsync(mailMessage);
                        Console.WriteLine($"网易邮箱发送成功: {email}");
                        return true;
                    }
                    catch (SmtpException smtpEx)
                    {
                        Console.WriteLine($"网易邮箱发送失败: {smtpEx.StatusCode} - {smtpEx.Message}");
                        // 如果465端口失败，尝试使用25端口
                        if (port == 465)
                        {
                            return await SendWithAlternatePort(email, verificationCode, userName, host, 25);
                        }
                        throw;
                    }
                }
                else
                {
                    // 其他邮箱服务器的通用配置
                    using var client = new SmtpClient(host, port)
                    {
                        EnableSsl = enableSsl,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(userName, password),
                        Timeout = 30000
                    };

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail, "灵绘智影AI创作平台"),
                        Subject = "邮箱验证码",
                        SubjectEncoding = Encoding.UTF8,
                        Body = GenerateVerificationEmailBody(verificationCode),
                        BodyEncoding = Encoding.UTF8,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(email);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送邮件失败: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendWithAlternatePort(string email, string verificationCode, string userName, string host, int altPort)
        {
            try
            {
                Console.WriteLine($"尝试使用备用端口 {altPort} 发送邮件");
                using var client = new SmtpClient(host, altPort)
                {
                    EnableSsl = false, // 25端口通常不需要SSL
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(userName, _configuration["SmtpSettings:Password"]),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(userName, "灵绘智影AI创作平台", Encoding.UTF8),
                    Subject = "邮箱验证码",
                    SubjectEncoding = Encoding.UTF8,
                    Body = GenerateVerificationEmailBody(verificationCode),
                    BodyEncoding = Encoding.UTF8,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(email));

                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"备用端口发送成功: {altPort}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"备用端口发送失败: {ex.Message}");
                return false;
            }
        }

        private string GenerateVerificationEmailBody(string verificationCode)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>邮箱验证码</title>
</head>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 10px; text-align: center;'>
        <h2 style='color: #333; margin-bottom: 20px;'>
            <i class='fas fa-envelope' style='color: #007bff;'></i> 邮箱验证码
        </h2>
        <p style='color: #666; font-size: 16px; line-height: 1.6; margin-bottom: 30px;'>
            您正在注册 灵绘智影AI创作平台 账户，请使用以下验证码完成邮箱验证：
        </p>
        <div style='background-color: #007bff; color: white; font-size: 32px; font-weight: bold; 
                    padding: 20px; border-radius: 8px; display: inline-block; margin: 20px 0; 
                    letter-spacing: 5px; text-decoration: none;'>
            {verificationCode}
        </div>
        <p style='color: #666; font-size: 14px; margin-top: 30px;'>
            验证码有效期为10分钟，请及时使用。<br>
            如果这不是您的操作，请忽略此邮件。
        </p>
    </div>
</body>
</html>";
        }
    }
}