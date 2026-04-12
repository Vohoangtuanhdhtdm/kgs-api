using kgs_api.Interfaces;
using kgs_api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace kgs_api.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string CreateToken(ApplicationUser user)
        {
            var secretKey = _config["AppSettings:TokenKey"];

            if (string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("TokenKey is not configured.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

//Claims là những mẩu thông tin được đóng gói bên trong thẻ JWT. Ở đây bạn đang nhét vào 3 thông tin:

//Sub(Subject): Tên đăng nhập (UserName) của người dùng.

//Jti (JWT ID): Một mã ID ngẫu nhiên (Guid) cho chính cái token này. Giúp hệ thống chống lại các cuộc tấn công phát lại (Replay Attacks).

//NameIdentifier: ID gốc của user trong cơ sở dữ liệu.