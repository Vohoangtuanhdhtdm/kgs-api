using kgs_api.Data;
using kgs_api.Interfaces;
using kgs_api.Models;
using kgs_api.Models.DTOs;
using kgs_api.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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

        #region PUT: api/assets/{id} - Cập nhật & Kiểm duyệt Giá
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAsset(Guid id, [FromForm] UpdateAssetRequest request)
        {
            var userId = GetCurrentUserId();
            var asset = await _context.UserAssets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản." });

            double lat = 0, lng = 0;
            if (!string.IsNullOrEmpty(request.Latitude))
                double.TryParse(request.Latitude.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out lat);
            if (!string.IsNullOrEmpty(request.Longitude))
                double.TryParse(request.Longitude.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out lng);

            string? uploadedImageUrl = null;
            if (request.Thumbnail != null && request.Thumbnail.Length > 0)
            {
                if (!string.IsNullOrEmpty(asset.ThumbnailUrl))
                    await _photoService.DeletePhotoAsync(asset.ThumbnailUrl);

                var uploadResult = await _photoService.AddPhotoAsync(request.Thumbnail);
                if (uploadResult.Error == null)
                    uploadedImageUrl = uploadResult.SecureUrl.AbsoluteUri;
            }

            bool isPriceChanged = false;
            if (asset.LinkedPropertyId.HasValue)
            {
                var publicProperty = await _context.Properties.FindAsync(asset.LinkedPropertyId.Value);
                if (publicProperty != null)
                {
                    if (request.EstimatedValue.HasValue && request.EstimatedValue.Value != publicProperty.Price)
                    {
                        isPriceChanged = true;
                        publicProperty.Price = request.EstimatedValue.Value;

                        // ĐÁNH TRẠNG THÁI VỀ PENDING VÌ ĐỔI GIÁ
                        publicProperty.Status = Helper.StatusPending;
                    }

                    publicProperty.Title = request.Name;
                    publicProperty.AddressDetail = request.Address;
                    if (lat != 0) publicProperty.Latitude = lat.ToString(CultureInfo.InvariantCulture);
                    if (lng != 0) publicProperty.Longitude = lng.ToString(CultureInfo.InvariantCulture);

                    if (uploadedImageUrl != null)
                    {
                        publicProperty.Img ??= new List<string>();
                        if (!publicProperty.Img.Contains(uploadedImageUrl))
                            publicProperty.Img.Add(uploadedImageUrl);
                    }
                }
            }

            asset.Name = request.Name;
            asset.Address = request.Address;
            if (lat != 0) asset.Latitude = lat.ToString(CultureInfo.InvariantCulture);
            if (lng != 0) asset.Longitude = lng.ToString(CultureInfo.InvariantCulture);
            asset.Type = request.Type;
            asset.EstimatedValue = request.EstimatedValue;
            asset.AcquisitionDate = request.AcquisitionDate;
            asset.Notes = request.Notes;
            if (uploadedImageUrl != null) asset.ThumbnailUrl = uploadedImageUrl;
            asset.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            string msg = isPriceChanged
                ? "Đã cập nhật! Do bạn thay đổi Giá bán, tin công khai đang được tạm ẩn chờ Admin duyệt lại."
                : "Cập nhật tài sản thành công!";

            return Ok(new { message = msg, asset, isPriceChanged });
        }
        #endregion

        #region PUT: api/assets/{id}/status - Cập nhật trạng thái và Đồng bộ Chợ BĐS
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateAssetStatus(Guid id, [FromBody] UpdateAssetStatusRequest request)
        {
            var userId = GetCurrentUserId();

            // 1. Tìm tài sản
            var asset = await _context.UserAssets
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản." });

            // 2. Kiểm tra Logic: Chưa đăng tin thì không được đổi sang trạng thái mua bán
            if (!asset.LinkedPropertyId.HasValue && request.Status != AssetStatus.Private)
            {
                return BadRequest(new { message = "Tài sản chưa được đăng tin công khai. Vui lòng dùng chức năng Đăng tin." });
            }

            // 3. Cập nhật trạng thái cho Asset
            asset.Status = request.Status;
            asset.UpdatedAt = DateTime.UtcNow;

            // 4. ĐỒNG BỘ SANG BẢNG PROPERTY (Nếu tài sản này đã được đăng lên Chợ)
            if (asset.LinkedPropertyId.HasValue)
            {
                var publicProperty = await _context.Properties.FindAsync(asset.LinkedPropertyId.Value);

                if (publicProperty != null)
                {
                    switch (request.Status)
                    {
                        case AssetStatus.Private:
                            // Rút tin về: Đổi status Property thành "Rejected" hoặc thêm 1 status "Hidden" vào Helper của bạn
                            publicProperty.Status = Helper.AssetStatusHidden;
                            break;

                        case AssetStatus.Sold:
                        case AssetStatus.Rented:
                            // Đã chốt giao dịch: Gỡ khỏi chợ hoặc đánh dấu Đã bán
                            publicProperty.Status = Helper.AssetStatusSold; // Bạn có thể thêm Helper.StatusSold ở BE
                            break;

                        case AssetStatus.ForRent:
                        case AssetStatus.ForSale:
                            // Đăng lại tin: Đưa về trạng thái chờ duyệt hoặc hiển thị lại
                            publicProperty.Status = Helper.StatusPending;
                            break;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật trạng thái và đồng bộ hệ thống.", currentStatus = asset.Status });
        }
        #endregion

        #region POST: api/assets/{id}/publish - Công khai Đăng tin tài sản ra thị trường (Public Listing)
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishAsset(Guid id, [FromForm] PublishAssetRequest request)
        {
            var userId = GetCurrentUserId();

            var asset = await _context.UserAssets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (asset == null)
                return NotFound(new { message = "Không tìm thấy tài sản." });

            if (asset.LinkedPropertyId.HasValue)
                return BadRequest(new { message = "Tài sản này đã được đăng tin công khai." });

            // 1. Xử lý Upload Ảnh (Giống hệt PropertiesController)
            var imageUrls = new List<string>();
            if (request.Images != null && request.Images.Any())
            {
                var uploadTasks = request.Images.Select(file => _photoService.AddPhotoAsync(file));
                var uploadResults = await Task.WhenAll(uploadTasks);

                foreach (var result in uploadResults)
                {
                    if (result.Error != null)
                        return BadRequest(new { message = $"Lỗi khi tải ảnh: {result.Error.Message}" });
                    imageUrls.Add(result.SecureUrl.AbsoluteUri);
                }
            }

            // 2. Map dữ liệu
            var newProperty = new Property
            {
                UserId = userId,
                Title = request.Title,
                Description = request.Description,
                Price = request.Price,
                Img = imageUrls, // Gán list ảnh vừa upload

                // Vị trí (Kết hợp giữa Form và Asset)
                City = request.City,
                District = request.District,
                Ward = request.Ward,
                AddressDetail = asset.Address, // Lấy từ Asset
                Latitude = asset.Latitude,     // Lấy từ Asset
                Longitude = asset.Longitude,   // Lấy từ Asset

                // Thông số kỹ thuật (Từ Form)
                Area = request.Area,
                Frontage = request.Frontage,
                Floors = request.Floors,
                Bedrooms = request.Bedrooms,
                Bathrooms = request.Bathrooms,
                HouseDirection = request.HouseDirection,
                LegalStatus = request.LegalStatus,
                FurnitureState = request.FurnitureState,

                PropertyType = asset.Type.ToString(), // Lấy từ Asset
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Properties.Add(newProperty);
            await _context.SaveChangesAsync(); 

            // 3. Liên kết ngược lại với Asset
            asset.LinkedPropertyId = newProperty.Id;
            asset.Status = request.IsForRent ? AssetStatus.ForRent : AssetStatus.ForSale;
            asset.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng tin thành công, đang chờ duyệt!" });
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
