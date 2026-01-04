using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace GoogleAI.Services
{
    public class SimpleEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public SimpleEmailService(IConfiguration configuration)
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
                var userName = smtpSettings["UserName"];
                var password = smtpSettings["Password"];

                Console.WriteLine($"尝试发送邮件到 {email}");
                Console.WriteLine($"SMTP服务器: {host}:{port}");
                Console.WriteLine($"用户名: {userName}");

                // 网易邮箱的测试配置
                if (host.Contains("yeah.net"))
                {
                    // 尝试使用25端口无SSL连接
                    try
                    {
                        using var client = new SmtpClient("smtp.yeah.net", 25)
                        {
                            EnableSsl = false,
                            UseDefaultCredentials = false,
                            Credentials = new NetworkCredential(userName, password),
                            Timeout = 30000
                        };

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress(userName, "灵绘智影AI创作平台", Encoding.UTF8),
                            Subject = "邮箱验证码",
                            SubjectEncoding = Encoding.UTF8,
                            Body = $"您的验证码是: {verificationCode}",
                            BodyEncoding = Encoding.UTF8,
                            IsBodyHtml = false
                        };

                        mailMessage.To.Add(email);

                        await client.SendMailAsync(mailMessage);
                        Console.WriteLine("25端口发送成功");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"25端口发送失败: {ex.Message}");
                        
                        // 尝试465端口SSL连接
                        try
                        {
                            using var client = new SmtpClient("smtp.yeah.net", 465)
                            {
                                EnableSsl = true,
                                UseDefaultCredentials = false,
                                Credentials = new NetworkCredential(userName, password),
                                Timeout = 30000
                            };

                            var mailMessage = new MailMessage
                            {
                                From = new MailAddress(userName, "灵绘智影AI创作平台", Encoding.UTF8),
                                Subject = "邮箱验证码",
                                SubjectEncoding = Encoding.UTF8,
                                Body = $"您的验证码是: {verificationCode}",
                                BodyEncoding = Encoding.UTF8,
                                IsBodyHtml = false
                            };

                            mailMessage.To.Add(email);

                            await client.SendMailAsync(mailMessage);
                            Console.WriteLine("465端口发送成功");
                            return true;
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine($"465端口发送失败: {ex2.Message}");
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送邮件失败: {ex.Message}");
                Console.WriteLine($"详细信息: {ex}");
                return false;
            }
        }
    }
}