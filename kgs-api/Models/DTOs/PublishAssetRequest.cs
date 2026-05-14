namespace kgs_api.Models.DTOs
{
    public class PublishAssetRequest
    {
        public decimal ListingPrice { get; set; }
        public string? PublicTitle { get; set; }
        public bool IsForRent { get; set; }

 
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }

    }
}
