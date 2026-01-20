namespace BoardGameCafeFinder.Models.DTOs
{
    /// <summary>
    /// DTO for café search results (optimized for map display)
    /// </summary>
    public class CafeSearchResultDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }

        // Location
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Distance from search point (in meters)
        public double Distance { get; set; }

        // Ratings
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }

        // Status
        public bool IsOpenNow { get; set; }
        public bool IsPremium { get; set; }
        public bool IsVerified { get; set; }

        // Features
        public int TotalGames { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }

        // SEO
        public string Slug { get; set; } = string.Empty;

        // For UI display
        public string? ThumbnailUrl { get; set; }
        public string? PriceRange { get; set; }

        public List<ReviewSummaryDto> LatestReviews { get; set; } = new List<ReviewSummaryDto>();

        // Helper properties
        public string DistanceDisplay => GetDistanceDisplay();
        public string RatingDisplay => AverageRating.HasValue ? $"{AverageRating:F1} ⭐" : "No ratings";

        private string GetDistanceDisplay()
        {
            if (Distance < 1000)
                return $"{Distance:F0}m";

            return $"{(Distance / 1000):F1}km";
        }
    }
}
