using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace kgs_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {

        [Authorize(Roles = "Admin")] // Đăng nhập và phải có role Admin mới được truy cập endpoint này
        [HttpGet("hello")]
        public IActionResult GetHello()
        {
            return Ok("Hello from KGS API!");
        }
    }
}
