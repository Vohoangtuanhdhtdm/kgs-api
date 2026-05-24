namespace kgs_api.Models.DTOs
{
    public class PublishAssetRequest
    {
        // 1. Thông tin cơ bản
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }

        // Cần List ảnh thay vì 1 ảnh (giống PropertyViewModel)
        public List<IFormFile>? Images { get; set; }

        // 2. Vị trí
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        // AddressDetail, Latitude, Longitude sẽ lấy từ Asset

        // 3. Thông số kỹ thuật
        public double Area { get; set; }
        public double Frontage { get; set; }
        public int Floors { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public string HouseDirection { get; set; } = string.Empty;
        public string LegalStatus { get; set; } = string.Empty;
        public string FurnitureState { get; set; } = string.Empty;

        // Asset Status (Bán hay Cho thuê)
        public bool IsForRent { get; set; }

    }
}
