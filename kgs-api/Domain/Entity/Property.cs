using kgs_api.Domain.Entity.SubEntity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static kgs_api.Domain.Enums;

namespace kgs_api.Domain.Entity
{
    public class Property
    {

        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public ICollection<PropertyImages>? Images { get; set; } = new List<PropertyImages>();

        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } 

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string District { get; set; } = string.Empty;

        [Required]
        public string Ward { get; set; } = string.Empty;

        public string AddressDetail { get; set; } = string.Empty;

 
        public double Area { get; set; }
        public double Frontage { get; set; } 
        public string PropertyType { get; set; } = string.Empty;
        public int Floors { get; set; } 
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public string HouseDirection { get; set; } = string.Empty;
        public string LegalStatus { get; set; } = string.Empty;
        public string FurnitureState { get; set; } = string.Empty;

        public string? Latitude { get; set; }
        public string? Longitude { get; set; }


        // "Pending" (Chờ duyệt), "Approved" (Đã duyệt), "Rejected" (Bị từ chối), "Sold" (Đã bán)
        public PropertyStatus Status { get; set; } = PropertyStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      
        [Required]
        public string UserId { get; set; } = string.Empty; // Lưu ID của người đăng tin

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!; // liên kết với bảng AspNetUsers

    }
}
