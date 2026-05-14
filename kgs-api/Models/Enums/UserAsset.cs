namespace kgs_api.Models.Enums
{
    public class UserAsset
    {
        public enum AssetType
        {
            Apartment,
            House,
            Land,
            Commercial,
            Other
        }

        public enum AssetStatus
        {
            Private,    // Badge xám
            ForRent,    // Badge xanh lá
            ForSale,    // Badge cam
            Rented,     // Badge xanh dương
            Sold        // Badge tím
        }
    }
}
