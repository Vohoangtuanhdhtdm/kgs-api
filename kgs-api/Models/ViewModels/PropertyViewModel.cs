using System.ComponentModel.DataAnnotations;

namespace kgs_api.Models.ViewModels
{
    public class PropertyViewModel
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<IFormFile>? Images { get; set; }

        [Required]
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
    }
}
