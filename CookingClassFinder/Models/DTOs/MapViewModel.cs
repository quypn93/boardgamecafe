namespace CookingClassFinder.Models.DTOs
{
    public class MapViewModel
    {
        public List<MapSchoolDto> Schools { get; set; } = new();
        public double CenterLat { get; set; }
        public double CenterLng { get; set; }
        public int ZoomLevel { get; set; } = 10;
        public string? SearchQuery { get; set; }
        public string? City { get; set; }
        public string? CuisineType { get; set; }
    }

    public class MapSchoolDto
    {
        public int SchoolId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public decimal? Rating { get; set; }
        public int ReviewCount { get; set; }
        public int ClassCount { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsPremium { get; set; }
        public List<string> Cuisines { get; set; } = new();
        public string? PriceRange { get; set; }
    }
}
