namespace EscapeRoomFinder.Models.DTOs
{
    public class MapViewModel
    {
        public List<MapVenueDto> Venues { get; set; } = new();
        public double CenterLat { get; set; }
        public double CenterLng { get; set; }
        public int ZoomLevel { get; set; } = 10;
        public string? SearchQuery { get; set; }
        public string? City { get; set; }
        public string? Theme { get; set; }
    }

    public class MapVenueDto
    {
        public int VenueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
        public decimal? Rating { get; set; }
        public int ReviewCount { get; set; }
        public int RoomCount { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsPremium { get; set; }
        public List<string> Themes { get; set; } = new();
        public string? DifficultyRange { get; set; }
    }
}
