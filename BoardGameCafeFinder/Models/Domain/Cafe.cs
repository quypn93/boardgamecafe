using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BoardGameCafeFinder.Models.Domain
{
    public class Cafe
    {
        [Key]
        public int CafeId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        // Location Data
        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? State { get; set; }

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = "United States";

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 8)")]
        public double Latitude { get; set; }

        [Required]
        [Column(TypeName = "decimal(11, 8)")]
        public double Longitude { get; set; }

        // Contact Information
        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [MaxLength(500)]
        [Url]
        public string? Website { get; set; }

        // Business Information
        [Column(TypeName = "nvarchar(max)")]
        public string? OpeningHours { get; set; } // JSON format

        [Column(TypeName = "nvarchar(max)")]
        public string? AttributesJson { get; set; } // JSON format (Dictionary<string, List<string>>)

        [MaxLength(100)]
        public string? BggUsername { get; set; }

        [MaxLength(500)]
        [Url]
        public string? LibraryUrl { get; set; }

        [MaxLength(10)]
        public string? PriceRange { get; set; } // $, $$, $$$, $$$$

        // External IDs
        [MaxLength(200)]
        public string? GooglePlaceId { get; set; }

        [MaxLength(200)]
        public string? YelpBusinessId { get; set; }

        [MaxLength(1000)]
        [Url]
        public string? GoogleMapsUrl { get; set; }

        [MaxLength(500)]
        public string? LocalImagePath { get; set; } // Relative path to locally stored image

        // Ratings & Stats
        [Column(TypeName = "decimal(3, 2)")]
        public decimal? AverageRating { get; set; }

        public int TotalReviews { get; set; } = 0;

        // Status
        public bool IsVerified { get; set; } = false;
        public bool IsPremium { get; set; } = false;
        public bool IsActive { get; set; } = true;

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByUserId { get; set; }

        // SEO
        [Required]
        [MaxLength(300)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? MetaDescription { get; set; }

        // Navigation Properties
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<CafeGame> CafeGames { get; set; } = new List<CafeGame>();
        public virtual ICollection<Photo> Photos { get; set; } = new List<Photo>();
        public virtual ICollection<Event> Events { get; set; } = new List<Event>();
        public virtual PremiumListing? PremiumListing { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User? CreatedBy { get; set; }

        // Calculated property (not stored in database)
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public double DistanceKm { get; set; }

        // Helper methods
        public bool IsOpenNow()
        {
            if (string.IsNullOrEmpty(OpeningHours))
                return false;

            try
            {
                var hours = JsonSerializer.Deserialize<List<OpeningHourPeriod>>(OpeningHours);
                if (hours == null || !hours.Any())
                    return false;

                var now = DateTime.UtcNow;
                var dayOfWeek = (int)now.DayOfWeek; // 0 = Sunday, 6 = Saturday

                var todayHours = hours.FirstOrDefault(h => h.DayOfWeek == dayOfWeek);
                if (todayHours == null)
                    return false;

                var currentMinutes = now.Hour * 60 + now.Minute;
                return currentMinutes >= todayHours.OpenMinutes && currentMinutes < todayHours.CloseMinutes;
            }
            catch
            {
                return false;
            }
        }

        public List<OpeningHourPeriod>? GetOpeningHours()
        {
            if (string.IsNullOrEmpty(OpeningHours))
                return null;

            try
            {
                return JsonSerializer.Deserialize<List<OpeningHourPeriod>>(OpeningHours);
            }
            catch
            {
                return null;
            }
        }

        public Dictionary<string, List<string>>? GetAttributes()
        {
            if (string.IsNullOrEmpty(AttributesJson))
                return null;

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(AttributesJson);
            }
            catch
            {
                return null;
            }
        }

        public void SetAttributes(Dictionary<string, List<string>> inputs) 
        {
             AttributesJson = JsonSerializer.Serialize(inputs);
        }
    }

    public class OpeningHourPeriod
    {
        public int DayOfWeek { get; set; } // 0 = Sunday, 6 = Saturday
        public int OpenMinutes { get; set; } // Minutes from midnight (e.g., 540 = 9:00 AM)
        public int CloseMinutes { get; set; } // Minutes from midnight (e.g., 1320 = 10:00 PM)

        public string GetOpenTime()
        {
            var hours = OpenMinutes / 60;
            var minutes = OpenMinutes % 60;
            var period = hours >= 12 ? "PM" : "AM";
            hours = hours > 12 ? hours - 12 : (hours == 0 ? 12 : hours);
            return $"{hours}:{minutes:D2} {period}";
        }

        public string GetCloseTime()
        {
            var hours = CloseMinutes / 60;
            var minutes = CloseMinutes % 60;
            var period = hours >= 12 ? "PM" : "AM";
            hours = hours > 12 ? hours - 12 : (hours == 0 ? 12 : hours);
            return $"{hours}:{minutes:D2} {period}";
        }

        public string GetDayName()
        {
            return DayOfWeek switch
            {
                0 => "Sunday",
                1 => "Monday",
                2 => "Tuesday",
                3 => "Wednesday",
                4 => "Thursday",
                5 => "Friday",
                6 => "Saturday",
                _ => "Unknown"
            };
        }
    }
}
