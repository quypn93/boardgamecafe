namespace EscapeRoomFinder.Models.ViewModels
{
    public class CityBlogItem
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Slug => City.ToLower().Replace(" ", "-").Replace("'", "");
        public int VenueCount { get; set; }
        public int TotalRooms { get; set; }
        public decimal? AverageRating { get; set; }
        public string? SampleImageUrl { get; set; }
    }

    public class CityGuideViewModel
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public List<EscapeRoomFinder.Models.Domain.EscapeRoomVenue> Venues { get; set; } = new();
        public int TotalRooms { get; set; }
        public decimal? AverageRating { get; set; }
        public List<CityBlogItem> RelatedCities { get; set; } = new();
    }
}
