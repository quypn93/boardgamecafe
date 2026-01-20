namespace BoardGameCafeFinder.Models.DTOs
{
    /// <summary>
    /// View model for the Map page
    /// </summary>
    public class MapViewModel
    {
        public string GoogleMapsApiKey { get; set; } = string.Empty;

        // Default center (USA center)
        public double DefaultLatitude { get; set; } = 39.8283;
        public double DefaultLongitude { get; set; } = -98.5795;
        public int DefaultZoom { get; set; } = 4;

        // Optional: Initial search location
        public string? InitialCity { get; set; }
        public double? InitialLatitude { get; set; }
        public double? InitialLongitude { get; set; }
    }
}
