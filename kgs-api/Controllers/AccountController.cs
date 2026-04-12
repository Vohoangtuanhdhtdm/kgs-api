using kgs_api.Data;
using kgs_api.Interfaces;
using kgs_api.Models;
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

            // BƯỚC 2: Dùng UserManager để kiểm tra mật khẩu thay vì SignInManager
            var isPasswordCorrect = await _userManger.CheckPasswordAsync(user, model.Password);

            if (!isPasswordCorrect) return Unauthorized("Mật khẩu không đúng.");


            // Generate JWT token
            var token = _tokenService.CreateToken(user);

            return Ok(new
            {
                message = "Login successful",
                username = user.UserName,
                token
            });
        }

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
            // Optionally, assign a default role to the user here
            return Ok(new { message = "Registration successful", userName = user.Name });

        }
    }
}
