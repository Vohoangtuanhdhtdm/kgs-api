using kgs_api.Data;
using kgs_api.Interfaces;
using kgs_api.Models;
using kgs_api.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static kgs_api.Models.Enums.UserAsset;

namespace kgs_api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AssetsController : ControllerBase
    {
        private readonly KgsDbContext _context;
        private readonly IPhotoService _photoService;

        public AssetsController(KgsDbContext context, IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        // Helper method lấy UserId từ JWT
        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }

        #region // GET: api/assets
        [HttpGet]
        public async Task<IActionResult> GetMyAssets()
        {
            var userId = GetCurrentUserId();
            var assets = await _context.UserAssets
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(); // Tối ưu: Có thể thêm Select() để map sang DTO

            return Ok(assets);
        }
        #endregion

        #region // GET: api/assets/{id} - Lấy chi tiết 1 tài sản (Dùng khi mở Form Edit hoặc trang chi tiết)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAssetById(Guid id)
        {
            var userId = GetCurrentUserId();
            var asset = await _context.UserAssets
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản." });

            return Ok(asset);
        }
        #endregion

        #region POST: api/assets - Thêm mới tài sản để quản lý (Private Portfolio)
        [HttpPost]
        public async Task<IActionResult> CreateAsset([FromForm] CreateAssetRequest request)
        {
            var userId = GetCurrentUserId();
            string? uploadedImageUrl = null;

            // Xử lý Upload ảnh lên Cloudinary nếu có file
            if (request.Thumbnail != null && request.Thumbnail.Length > 0)
            {
                try
                {
                    // Hàm UploadAsync này tùy thuộc vào cách bạn viết service. 
                    // Nó sẽ đẩy IFormFile lên Cloudinary và trả về URL an toàn (SecureUrl).
                    var uploadResult = await _photoService.AddPhotoAsync(request.Thumbnail);
                    if (uploadResult.Error != null)
                    {
                        return BadRequest(new { message = "Lỗi khi tải ảnh lên Cloudinary: " + uploadResult.Error.Message });
                    }

                    uploadedImageUrl = uploadResult.SecureUrl.AbsoluteUri;
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Lỗi server khi xử lý hình ảnh.", details = ex.Message });
                }
            }

            var newAsset = new UserAsset
            {
                UserId = userId,
                Name = request.Name,
                Address = request.Address,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Type = request.Type,
                EstimatedValue = request.EstimatedValue,
                AcquisitionDate = request.AcquisitionDate,
                Notes = request.Notes,
                ThumbnailUrl = uploadedImageUrl,
                Status = AssetStatus.Private, // Mặc định luôn là Private khi mới tạo
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserAssets.Add(newAsset);
            await _context.SaveChangesAsync();

            // Trả về 201 Created cùng với URL để lấy resource vừa tạo
            return CreatedAtAction(nameof(GetAssetById), new { id = newAsset.Id }, newAsset);
        }
        #endregion

        #region PUT: api/assets/{id} - Cập nhật toàn bộ thông tin tài sản (Dùng cho Form Edit)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsset(Guid id, [FromForm] UpdateAssetRequest request)
        {
            var userId = GetCurrentUserId();
            var asset = await _context.UserAssets
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản hoặc bạn không có quyền sửa." });

            string? uploadedImageUrl = null;

            
            // Xử lý Upload ảnh Mới lên Cloudinary nếu có file
            if (request.Thumbnail != null && request.Thumbnail.Length > 0)
            {
                try
                {
                    if (asset.ThumbnailUrl != null && asset.ThumbnailUrl.Any())
                    {
                        await _photoService.DeletePhotoAsync(asset.ThumbnailUrl);
                    }

                    var uploadResult = await _photoService.AddPhotoAsync(request.Thumbnail);
                    if (uploadResult.Error != null)
                    {
                        return BadRequest(new { message = "Lỗi khi tải ảnh lên Cloudinary: " + uploadResult.Error.Message });
                    }

                    uploadedImageUrl = uploadResult.SecureUrl.AbsoluteUri;
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Lỗi server khi xử lý hình ảnh.", details = ex.Message });
                }
            }

            // Cập nhật các trường
            asset.Name = request.Name;
            asset.Address = request.Address;
            asset.Latitude = request.Latitude;
            asset.Longitude = request.Longitude;
            asset.Type = request.Type;
            asset.EstimatedValue = request.EstimatedValue;
            asset.AcquisitionDate = request.AcquisitionDate;
            asset.Notes = request.Notes;
            asset.ThumbnailUrl = uploadedImageUrl ?? asset.ThumbnailUrl;

            asset.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật tài sản thành công!", asset });
        }
        #endregion

        #region PUT: api/assets/{id}/status - API siêu nhẹ chuyên dụng cho việc đổi trạng thái từ Dropdown ngoài Grid/Card

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateAssetStatus(Guid id, [FromBody] UpdateAssetStatusRequest request)
        {
            var userId = GetCurrentUserId();
            var asset = await _context.UserAssets
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản." });

            asset.Status = request.Status;
            asset.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật trạng thái.", currentStatus = asset.Status });
        }
        #endregion

        #region POST: api/assets/{id}/publish - Công khai Đăng tin tài sản ra thị trường (Public Listing)
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishAsset(Guid id, [FromBody] PublishAssetRequest request)
        {
            var userId = GetCurrentUserId();

            var asset = await _context.UserAssets
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản hoặc bạn không có quyền truy cập." });

            if (asset.LinkedPropertyId.HasValue)
                return BadRequest(new { message = "Tài sản này đã được đăng tin công khai." });

            var newProperty = new Property
            {
                UserId = userId, // Map đúng tên trường của bạn
                Title = request.PublicTitle ?? asset.Name,
                AddressDetail = asset.Address, // Đưa địa chỉ gộp vào AddressDetail

                // Vì City, District, Ward là [Required], bạn có thể lấy từ request form 
                // hoặc để giá trị mặc định nếu ở Asset chưa tách các trường này ra
                City = request.City ?? "Chưa xác định",
                District = request.District ?? "Chưa xác định",
                Ward = request.Ward ?? "Chưa xác định",

                Price = request.ListingPrice,
                Latitude = asset.Latitude,
                Longitude = asset.Longitude,
                PropertyType = asset.Type.ToString(), // Chuyển Enum thành string
                Status = "Pending", // Trạng thái mặc định khi đăng tin mới
                CreatedAt = DateTime.UtcNow
            };

            _context.Properties.Add(newProperty);

            // Lưu lại Property trước để Entity Framework sinh ra ID (kiểu int) cho newProperty
            await _context.SaveChangesAsync();

            // 3. Cập nhật lại ID int vào Asset
            asset.LinkedPropertyId = newProperty.Id; // Lúc này newProperty.Id đã có giá trị int hợp lệ
            asset.Status = request.IsForRent ? AssetStatus.ForRent : AssetStatus.ForSale;
            asset.UpdatedAt = DateTime.UtcNow;

            // 4. Lưu lại cập nhật của Asset
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Đăng tin thành công!",
                propertyId = newProperty.Id
            });
        }
        #endregion

        #region DELETE: api/assets/{id} - Xóa tài sản khỏi Portfolio
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsset(Guid id)
        {
            var userId = GetCurrentUserId();
            var asset = await _context.UserAssets
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản để xóa." });

            // Lưu ý: Nếu có ThumbnailUrl trên Cloudinary, bạn có thể gọi
            // _cloudinaryService.DestroyAsync(publicId) ở đây giống như cách bạn đã làm ở Context 2.

            _context.UserAssets.Remove(asset);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa tài sản khỏi danh mục." });
        }
        #endregion

    }
}
