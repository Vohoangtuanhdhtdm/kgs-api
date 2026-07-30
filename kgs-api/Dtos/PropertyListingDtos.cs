using System.ComponentModel.DataAnnotations;
using static kgs_api.Domain.Enums;

namespace kgs_api.Dtos
{
    public sealed record CreateListingFromAssetRequest(
        ListingType Type,
        [Required, MaxLength(200)] string Title,
        [Required] string Description,
        [Range(0.01, (double)decimal.MaxValue)] decimal Price,
        PaymentCycle? RentPaymentCycle,   // bắt buộc nếu Type = Rent, validate ở service
        int Floors,
        int Bedrooms,
        int Bathrooms,
        double Frontage,
        string HouseDirection,
        string LegalStatus,
        string FurnitureState,
        string PropertyType,
        /// <summary>Chọn ảnh từ AssetMedia đã có sẵn để đăng công khai — không upload lại.</summary>
        List<Guid> SelectedAssetMediaIds);

    public sealed record PublicPropertySearchQuery(
        ListingType? Type,
        string? City,
        string? District,
        decimal? PriceMin,
        decimal? PriceMax,
        int? BedroomsMin,
        string? Keyword,
        int Page = 1,
        int PageSize = 20);

    public sealed record PublicPropertySummaryDto(
        int Id, string Slug, string Title, ListingType Type, decimal Price,
        PaymentCycle? RentPaymentCycle, string City, string District,
        int Bedrooms, int Bathrooms, double Area, string? ThumbnailUrl);

    public sealed record PublicPropertyDetailDto(
        int Id, string Slug, string Title, string Description, ListingType Type,
        decimal Price, PaymentCycle? RentPaymentCycle,
        string City, string District, string Ward, string AddressDetail,
        double Area, double Frontage, int Floors, int Bedrooms, int Bathrooms,
        string HouseDirection, string LegalStatus, string FurnitureState, string PropertyType,
        double? Latitude, double? Longitude,
        IReadOnlyList<string> ImageUrls, int ViewCount,
        string OwnerName, string OwnerPhone);   // hiện trực tiếp theo quyết định đã chốt

    public sealed record OwnerListingDto(
        int Id, string? Slug, string Title, ListingType Type, PropertyStatus Status,
        decimal Price, int ViewCount, DateTime CreatedAt, Guid? LinkedAssetId);
}
