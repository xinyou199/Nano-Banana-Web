using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GoogleAI.Models;
using GoogleAI.Repositories;

namespace GoogleAI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> AuthenticateAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        string GenerateJwtToken(User user);
        string HashPassword(string password);
        bool VerifyPassword(string password, string passwordHash);
        bool ValidateToken(string token);
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IUserRepository userRepository, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "用户名或密码错误"
                };
            }

            var token = GenerateJwtToken(user);

            // 更新用户的当前令牌以实现单点登录
            await _userRepository.UpdateUserTokenAsync(user.Id, token);

            return new LoginResponse
            {
                Success = true,
                Message = "登录成功",
                Token = token,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    IsAdmin = user.IsAdmin
                }
            };
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // 验证密码和确认密码是否一致
                if (request.Password != request.ConfirmPassword)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "两次输入的密码不一致"
                    };
                }

                // 检查用户名是否已存在
                var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
                if (existingUser != null)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "用户名已存在"
                    };
                }

                // 检查邮箱是否已存在
                var existingEmail = await _userRepository.GetByEmailAsync(request.Email);
                if (existingEmail != null)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "邮箱已被注册"
                    };
                }

                // 创建新用户
                var user = new User
                {
                    Username = request.Username,
                    PasswordHash = HashPassword(request.Password),
                    Email = request.Email,
                    IsActive = true,
                    IsAdmin = false,
                    Points = 100, // 默认100积分
                    CreatedAt = DateTime.Now
                };

                var userId = await _userRepository.CreateAsync(user);
                if (userId <= 0)
                {
                    return new RegisterResponse
                    {
                        Success = false,
                        Message = "注册失败，请稍后重试"
                    };
                }

                user.Id = userId;

                // 生成JWT令牌
                var token = GenerateJwtToken(user);

                // 更新用户的当前令牌
                await _userRepository.UpdateUserTokenAsync(user.Id, token);

                return new RegisterResponse
                {
                    Success = true,
                    Message = "注册成功",
                    Token = token,
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = user.Email,
                        IsAdmin = user.IsAdmin
                    }
                };
            }
            catch (Exception ex)
            {
                return new RegisterResponse
                {
                    Success = false,
                    Message = $"注册失败: {ex.Message}"
                };
            }
        }

        public string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(jwtSettings["ExpirationMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string HashPassword(string password)
        {
            // Using BCrypt-like approach with PBKDF2
            using var sha256 = SHA256.Create();
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var hashBytes = hash.GetBytes(32);
            var combined = new byte[salt.Length + hashBytes.Length];
            Array.Copy(salt, 0, combined, 0, salt.Length);
            Array.Copy(hashBytes, 0, combined, salt.Length, hashBytes.Length);
            return Convert.ToBase64String(combined);
        }

        //public bool VerifyPassword(string password, string passwordHash)
        //{
        //    try
        //    {
        //        // 解码存储的密码哈希
        //        var hashBytes = Convert.FromBase64String(passwordHash);
        //        var salt = new byte[16];
        //        Array.Copy(hashBytes, 0, salt, 0, 16);

        //        // 使用相同的盐值计算输入密码的哈希
        //        var hash = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        //        var computedHash = hash.GetBytes(32);

        //        // 比较计算出的哈希与存储的哈希
        //        for (int i = 0; i < 32; i++)
        //        {
        //            if (hashBytes[i + 16] != computedHash[i])
        //                return false;
        //        }
        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

        public bool VerifyPassword(string password, string storedHash)
        {
            // Base64 解码
            var hashBytes = Convert.FromBase64String(storedHash);

            // 提取 salt（16字节）
            var salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            // 提取原有的哈希（32字节）
            var storedPasswordHash = new byte[32];
            Array.Copy(hashBytes, 16, storedPasswordHash, 0, 32);

            // 用相同的参数计算输入密码的哈希
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(32);

            // 比较两个哈希是否一致（使用安全比较）
            return CryptographicOperations.FixedTimeEquals(storedPasswordHash, computedHash);
        }

        public bool ValidateToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return false;

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                tokenHandler.ValidateToken(token, validationParameters, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}