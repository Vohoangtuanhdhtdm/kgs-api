using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static kgs_api.Models.Enums.UserAsset;

namespace kgs_api.Models
{
    public class UserAsset
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty; // Map với ClaimTypes.NameIdentifier

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        // Geocoding Data
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }

        public AssetType Type { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Private;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedValue { get; set; } // Private value

        public DateTime? AcquisitionDate { get; set; }
        public string? Notes { get; set; } // Private notes
        public string? ThumbnailUrl { get; set; } // Tích hợp Cloudinary

        // Trỏ tới tin đăng công khai (nếu có)
        public int? LinkedPropertyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
