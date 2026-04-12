using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace kgs_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ValuationController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public ValuationController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpPost("predict")]
        public async Task<IActionResult> PredictPrice([FromBody] object formData)
        {
            try
            {
                // Lấy thông tin người đang yêu cầu định giá (Để sau này lưu DB)
                // var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Đóng gói dữ liệu và gửi sang Server AI (Port 8000)
                var response = await _httpClient.PostAsJsonAsync("http://localhost:8000/predict", formData);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(500, "Lỗi khi xử lý mô hình AI.");
                }

                // Đọc kết quả trả về từ Python
                var result = await response.Content.ReadFromJsonAsync<object>();

                // (Tương lai) Viết code lưu result và userId xuống Database (PostgreSQL) ở đây...

                // Trả kết quả về cho React Frontend
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }
    }
}
