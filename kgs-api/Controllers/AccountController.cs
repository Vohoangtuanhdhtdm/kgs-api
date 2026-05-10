using kgs_api.Data;
using kgs_api.Interfaces;
using kgs_api.Models;
using kgs_api.Models.DTOs;
using kgs_api.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace kgs_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly KgsDbContext _context;
        private readonly UserManager<ApplicationUser> _userManger;
        private readonly ITokenService _tokenService;
        private readonly RoleManager<IdentityRole> _roleManager;
       

        public AccountController(
            KgsDbContext context,
            ITokenService tokenService,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManger
         )
        {
            _context = context;
            _userManger = userManger;
            _tokenService = tokenService;
            _roleManager = roleManager;
        }


        #region Đăng Nhập 

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManger.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return Unauthorized("Invalid email or password.");
            }

            var isPasswordCorrect = await _userManger.CheckPasswordAsync(user, model.Password);

            if (!isPasswordCorrect) return Unauthorized("Mật khẩu không đúng.");

            var roles = await _userManger.GetRolesAsync(user);

            // Generate JWT token
            var token = _tokenService.CreateToken(user, roles);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Lưu refresh token vào database
            await _userManger.SetAuthenticationTokenAsync(user, "KGS", "RefreshToken", refreshToken);

            return Ok(new
            {
                message = "Login successful",
                username = user.UserName,
                roles,
                token,
                refreshToken
            });
        }
        #endregion

        #region Đăng Ký 
        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser = await _userManger.FindByEmailAsync(model.Email);

            if (existingUser != null)
            {
                return BadRequest("Email is already registered.");
            }

            if (!string.IsNullOrEmpty(model.Role) && model.Role.Equals(Utility.Helper.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Lỗi bảo mật: Không được phép tự đăng ký quyền Quản trị viên.");
            }

            string roleToAssign = Utility.Helper.User;

            if (!string.IsNullOrEmpty(model.Role) && (model.Role == Utility.Helper.Member || model.Role == Utility.Helper.User))
            {
                roleToAssign = model.Role;
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name
            };

            var result = await _userManger.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Registration failed" });
            }

            await _userManger.AddToRoleAsync(user, roleToAssign);


            // Optionally, assign a default role to the user here
            return Ok(new { message = "Registration successful", userName = user.Name });

        }

        #endregion

        #region Cấp Token Mới Sử Dụng Refresh Token
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenModel tokenModel, string email)
        {
            if (tokenModel is null) return BadRequest("Invalid client request");

            var user = await _userManger.FindByEmailAsync(email);
            if (user == null) return BadRequest("Invalid client request");

            var savedRefreshToken = await _userManger.GetAuthenticationTokenAsync(user, "KGS", "RefreshToken");

            if (savedRefreshToken != tokenModel.RefreshToken)
            {
                return Unauthorized("Refresh token không hợp lệ hoặc đã bị thu hồi.");
            }

            // Nếu hợp lệ -> Đúc Access Token mới & Refresh Token mới
            var roles = await _userManger.GetRolesAsync(user);
            var newAccessToken = _tokenService.CreateToken(user, roles);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            // 4. Lưu Refresh Token mới đè lên cái cũ
            await _userManger.SetAuthenticationTokenAsync(user, "KGS", "RefreshToken", newRefreshToken);

            return Ok(new
            {
                token = newAccessToken,
                refreshToken = newRefreshToken
            });
        }
        #endregion
    }
}
