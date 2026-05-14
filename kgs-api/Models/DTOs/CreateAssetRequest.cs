using static kgs_api.Models.Enums.UserAsset;

namespace kgs_api.Models.DTOs
{
    public class CreateAssetRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public AssetType Type { get; set; }
        public decimal? EstimatedValue { get; set; }
        public DateTime? AcquisitionDate { get; set; }
        public string? Notes { get; set; }
        public IFormFile? Thumbnail { get; set; }
    }

    public class UpdateAssetRequest : CreateAssetRequest
    {
        // Kế thừa toàn bộ field từ Create, có thể mở rộng nếu cần
    }

    public class UpdateAssetStatusRequest
    {
        public AssetStatus Status { get; set; }
    }
}
