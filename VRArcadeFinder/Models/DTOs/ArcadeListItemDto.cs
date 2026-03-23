namespace VRArcadeFinder.Models.DTOs
{
    /// <summary>
    /// Lightweight DTO for arcade listing on Home page (optimized for performance)
    /// </summary>
    public class ArcadeListItemDto
    {
        public int ArcadeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string Slug { get; set; } = string.Empty;

        // Image
        public string? LocalImagePath { get; set; }

        // Location (for distance calculation)
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Rating & Reviews (calculated in SQL)
        public decimal? AverageRating { get; set; }
        public int ReviewCount { get; set; }

        // Games count (calculated in SQL)
        public int GamesCount { get; set; }

        // VR-specific
        public string? VRPlatforms { get; set; }
        public int? TotalVRStations { get; set; }

        // Links
        public string? Website { get; set; }
        public string? GoogleMapsUrl { get; set; }

        // Calculated at runtime (not from DB)
        public double DistanceKm { get; set; }
    }
}
