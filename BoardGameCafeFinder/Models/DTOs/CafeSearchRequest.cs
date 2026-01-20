using System.ComponentModel.DataAnnotations;

namespace BoardGameCafeFinder.Models.DTOs
{
    /// <summary>
    /// Request model for café search API
    /// </summary>
    public class CafeSearchRequest
    {
        [Required]
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
        public double Longitude { get; set; }

        /// <summary>
        /// Search radius in meters (default: 10km)
        /// </summary>
        [Range(100, 100000, ErrorMessage = "Radius must be between 100m and 100km")]
        public int Radius { get; set; } = 10000;

        /// <summary>
        /// Filter for cafés that are currently open
        /// </summary>
        public bool OpenNow { get; set; } = false;

        /// <summary>
        /// Filter for cafés that have board game library
        /// </summary>
        public bool HasGames { get; set; } = false;

        /// <summary>
        /// Filter by minimum rating (1-5)
        /// </summary>
        [Range(1, 5)]
        public decimal? MinRating { get; set; }

        /// <summary>
        /// Maximum number of results to return
        /// </summary>
        [Range(1, 100)]
        public int Limit { get; set; } = 50;
    }
}
