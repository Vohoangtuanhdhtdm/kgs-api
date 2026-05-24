using kgs_api.Data;
using kgs_api.Interfaces;
using kgs_api.Models;
using kgs_api.Models.DTOs;
using kgs_api.Models.ViewModels;
using kgs_api.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace kgs_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PropertiesController : ControllerBase
    {
        private readonly KgsDbContext _context;
        private readonly IPhotoService _photoService;

        public PropertiesController(KgsDbContext context, IPhotoService photoService)
        {
            _context = context;
            _photoService = photoService;
        }

        #region Đăng tin mới
        [Authorize(Roles = $"{Helper.Member},{Helper.Admin}")]
        [HttpPost]
        public async Task<IActionResult> Property([FromForm] PropertyViewModel model)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (currentUserId == null)
            {
                return Unauthorized("Không nhận diện được người dùng!");
            }

            if (model.Images == null || !model.Images.Any())
            {
                return BadRequest(new { message = "Vui lòng cung cấp ít nhất 1 hình ảnh." });
            }

            var uploadTasks = model.Images.Select(file => _photoService.AddPhotoAsync(file));
            var uploadResults = await Task.WhenAll(uploadTasks);

            var imageUrls = new List<string>();

            foreach (var result in uploadResults)
            {
                if (result.Error != null)
                {
                    return BadRequest(new { message = $"Lỗi khi tải ảnh lên máy chủ: {result.Error.Message}" });
                }
                imageUrls.Add(result.SecureUrl.AbsoluteUri);
            }

            var property = new Property
            {
                Title = model.Title,
                Description = model.Description,
                Img = imageUrls,
                Price = model.Price,
                City = model.City,
                District = model.District,
                Ward = model.Ward,
                AddressDetail = model.AddressDetail,
                Area = model.Area,
                Frontage = model.Frontage,
                PropertyType = model.PropertyType,
                Floors = model.Floors,
                Bedrooms = model.Bedrooms,
                Bathrooms = model.Bathrooms,
                HouseDirection = model.HouseDirection,
                LegalStatus = model.LegalStatus,
                FurnitureState = model.FurnitureState,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                UserId = currentUserId,
                Status = Helper.StatusPending,     
                CreatedAt = DateTime.UtcNow
            };

            _context.Properties.Add(property);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng tin thành công, đang chờ duyệt!", propertyId = property.Id });
        }
        #endregion

        #region Danh sách tin đăng của người dùng 
        [Authorize(Roles = $"{Helper.Member},{Helper.Admin}")]
        [HttpGet("my-listings")]
        public async Task<IActionResult> Properties()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var myProperties = await _context.Properties
                .Where(p => p.UserId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(myProperties);
        }
        #endregion

        #region Danh sách tin đăng chờ duyệt (Admin) 
        [Authorize(Roles = Helper.Admin)]
        [HttpGet("admin/pending-listings")]
        public async Task<IActionResult> GetPendingProperties()
        {
            var properties = await _context.Properties
                .Where(p => p.Status == Helper.StatusPending)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(properties);
        }
        #endregion

        #region Duyệt tin đăng (Admin) 
        [Authorize(Roles = Helper.Admin)]
        [HttpPut("admin/{id}/approve")]
        public async Task<IActionResult> ApproveProperty(int id)
        {
            var property = await _context.Properties.FindAsync(id);

            if (property == null)
            {
                return NotFound("Không tìm thấy tin đăng!");
            }

            property.Status = Helper.StatusApproved;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Tin đăng đã được duyệt!", propertyId = property.Id });
        }
        #endregion

        #region Từ chối tin đăng (Admin)
        [Authorize(Roles = Helper.Admin)]
        [HttpPut("admin/{id}/reject")]
        public async Task<IActionResult> RejectProperty(int id)
        {
            var property = await _context.Properties.FindAsync(id);
            if (property == null) return NotFound("Không tìm thấy tin đăng.");

            property.Status = Helper.StatusRejected;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã từ chối tin đăng.", propertyId = property.Id });
        }
        #endregion

        #region Danh sách tin đăng công khai (cho tất cả người dùng)
        [HttpGet("public-listings")]
        public async Task<IActionResult> GetPublicProperties([FromQuery] PropertyQueryParameters query)
        {
            var propertiesQuery = _context.Properties
                .Where(p => p.Status == Helper.StatusApproved)
                .AsQueryable();

            if (!string.IsNullOrEmpty(query.City))
            {
                propertiesQuery = propertiesQuery.Where(p => p.City.ToLower().Contains(query.City.ToLower()));
            }

            if (!string.IsNullOrEmpty(query.District))
            {
                propertiesQuery = propertiesQuery.Where(p => p.District.ToLower().Contains(query.District.ToLower()));
            }

            if (!string.IsNullOrEmpty(query.PropertyType))
            {
                propertiesQuery = propertiesQuery.Where(p => p.PropertyType == query.PropertyType);
            }

            if (query.MinPrice.HasValue)
            {
                propertiesQuery = propertiesQuery.Where(p => p.Price >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                propertiesQuery = propertiesQuery.Where(p => p.Price <= query.MaxPrice.Value);
            }

            if (query.MinArea.HasValue)
            {
                propertiesQuery = propertiesQuery.Where(p => p.Area >= query.MinArea.Value);
            }

            if (query.MaxArea.HasValue)
            {
                propertiesQuery = propertiesQuery.Where(p => p.Area <= query.MaxArea.Value);
            }

            var totalItems = await propertiesQuery.CountAsync();

            int validPageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
            int validPageSize = query.PageSize > 0 ? query.PageSize : 10;

            var properties = await propertiesQuery
             .OrderByDescending(p => p.CreatedAt)
             .Skip((validPageNumber - 1) * validPageSize)
             .Take(validPageSize)
             .ToListAsync();

            // TRẢ VỀ DỮ LIỆU KÈM THEO SIÊU DỮ LIỆU (Metadata)
            return Ok(new
            {
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)validPageSize),
                CurrentPage = validPageNumber,
                PageSize = validPageSize,
                Data = properties
            });
        }
        #endregion

        #region Chi tiết tin đăng (cho tất cả người dùng)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPropertyById(int id)
        {
            var property = await _context.Properties
                .Include(p => p.User) 
                .FirstOrDefaultAsync(p => p.Id == id);

            if (property == null)
            {
                return NotFound("Không tìm thấy tin đăng bất động sản này.");
            }

            return Ok(property);
        }
        #endregion

        #region Xóa tin đăng

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeletePropertyById(int id)
        {
        
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
           // var isAdmin = User.IsInRole("Admin");

            var property = await _context.Properties
                .FirstOrDefaultAsync(p => p.Id == id);

            if (property == null)
            {
                return NotFound(new { message = "Không tìm thấy tin đăng bất động sản này." });
            }

            
            if (property.UserId != currentUserId)
            {
                return Forbid("Bạn không có quyền xóa tin đăng này."); // Trả về 403 Forbidden
            }

             if (property.Img != null && property.Img.Any())
             {
                foreach (var imageId in property.Img)
                {
                    await _photoService.DeletePhotoAsync(imageId);
                }
             }

            
            _context.Properties.Remove(property);
            await _context.SaveChangesAsync();

            return NoContent();

        }
        #endregion

        #region Cập Nhật Tin Đăng
        [Authorize(Roles = $"{Helper.Member},{Helper.Admin}")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProperty(int id, [FromForm] PropertyViewModel model)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var property = await _context.Properties.FirstOrDefaultAsync(p => p.Id == id && p.UserId == currentUserId);

            if (property == null) return NotFound("Không tìm thấy tin đăng.");

            // 1. Kiểm tra thay đổi giá để đánh rớt Status
            bool isPriceChanged = model.Price != property.Price;
            if (isPriceChanged)
            {
                property.Price = model.Price;
                property.Status = Helper.StatusPending;
            }

            // 2. Xử lý ảnh (nếu có upload mới)
            if (model.Images != null && model.Images.Any())
            {
                // Xóa ảnh cũ trên Cloudinary trước
                if (property.Img != null)
                {
                    foreach (var imgUrl in property.Img)
                    {
                        await _photoService.DeletePhotoAsync(imgUrl);
                    }
                }

                // Upload ảnh mới
                var uploadTasks = model.Images.Select(file => _photoService.AddPhotoAsync(file));
                var uploadResults = await Task.WhenAll(uploadTasks);

                var newImageUrls = new List<string>();
                foreach (var result in uploadResults)
                {
                    if (result.Error != null) return BadRequest($"Lỗi upload ảnh: {result.Error.Message}");
                    newImageUrls.Add(result.SecureUrl.AbsoluteUri);
                }
                property.Img = newImageUrls;
            }

            // 3. Cập nhật các trường thông tin
            property.Title = model.Title;
            property.Description = model.Description;
            property.Area = model.Area;
            property.Frontage = model.Frontage;
            property.PropertyType = model.PropertyType;
            property.Floors = model.Floors;
            property.Bedrooms = model.Bedrooms;
            property.Bathrooms = model.Bathrooms;
            property.HouseDirection = model.HouseDirection;
            property.LegalStatus = model.LegalStatus;
            property.FurnitureState = model.FurnitureState;
            property.City = model.City;
            property.District = model.District;
            property.Ward = model.Ward;
            property.AddressDetail = model.AddressDetail;
            property.Latitude = model.Latitude;
            property.Longitude = model.Longitude;

            // 4. Đồng bộ ngược lại vào UserAsset (Nếu tài sản này có LinkedPropertyId)
            var linkedAsset = await _context.UserAssets.FirstOrDefaultAsync(a => a.LinkedPropertyId == property.Id);
            if (linkedAsset != null)
            {
                linkedAsset.Name = model.Title;
                linkedAsset.Address = $"{model.AddressDetail}, {model.Ward}, {model.District}, {model.City}";
                linkedAsset.EstimatedValue = model.Price;
                // Nếu ảnh được cập nhật, đồng bộ luôn ThumbnailUrl của Asset
                if (property.Img != null && property.Img.Any())
                {
                    linkedAsset.ThumbnailUrl = property.Img.First();
                }
                linkedAsset.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isPriceChanged ? "Đã cập nhật! Tin đăng đang chờ Admin duyệt lại." : "Cập nhật thành công.",
                status = property.Status
            });
        }
        #endregion
    }
}