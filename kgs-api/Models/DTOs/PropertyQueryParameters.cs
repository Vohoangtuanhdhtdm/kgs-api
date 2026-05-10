using Microsoft.AspNetCore.Mvc;

namespace kgs_api.Models.DTOs
{
    public class PropertyQueryParameters
    {

        [FromQuery(Name = "page")]
        public int PageNumber { get; set; } = 1; 

       
       
        [FromQuery(Name = "pageSize")]
        public int PageSize { get; set; } = 10;


        public string? City { get; set; }
        public string? District { get; set; }
        public string? PropertyType { get; set; } // "House" hoặc "Land"

       
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

       
        public double? MinArea { get; set; }
        public double? MaxArea { get; set; }
    }
}
